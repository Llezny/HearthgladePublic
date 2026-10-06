using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;

namespace Hearthglade.Core.Trade {

    /// <summary>The saved form of a <see cref="Contract"/>.</summary>
    [Serializable]
    public class ContractData {
        public int Serial;
        public string Item;
        public int Count;
        public string RewardItem;
        public int RewardCount;
        public double Relation;
        public int ExpiresAt;
    }

    /// <summary>The saved form of a <see cref="ContractBoard"/>.</summary>
    [Serializable]
    public class ContractBoardData {
        public int NextSerial;
        public List<ContractData> Active = new List<ContractData>();
    }

    /// <summary>
    /// An order of a port: bring so many of something it is short of, get a better price than barter pays (the reward is worth about
    /// 1.4 times what is asked) and some trust on top. Open until the player has completed <see cref="ExpiresAt"/> expeditions.
    /// </summary>
    public sealed class Contract {
        public int Serial { get; }
        public ItemId Item { get; }
        public int Count { get; }
        public ItemId RewardItem { get; }
        public int RewardCount { get; }

        /// <summary>The trust the port gives for it, on top of the reward.</summary>
        public double RelationPoints { get; }

        /// <summary>The order lapses when the completed expeditions reach this number.</summary>
        public int ExpiresAt { get; }

        public Contract( int serial, ItemId item, int count, ItemId rewardItem, int rewardCount, double relationPoints, int expiresAt ) {
            Serial = serial;
            Item = item;
            Count = Math.Max( 1, count );
            RewardItem = rewardItem;
            RewardCount = Math.Max( 1, rewardCount );
            RelationPoints = Math.Max( 0.0, relationPoints );
            ExpiresAt = expiresAt;
        }

        public bool IsExpired( int completedExpeditions ) => completedExpeditions >= ExpiresAt;

        public ContractData ToData() {
            return new ContractData {
                Serial = Serial, Item = Item.Value, Count = Count, RewardItem = RewardItem.Value, RewardCount = RewardCount,
                Relation = RelationPoints, ExpiresAt = ExpiresAt
            };
        }

        public static Contract FromData( ContractData data ) {
            return string.IsNullOrEmpty( data?.Item ) || string.IsNullOrEmpty( data.RewardItem )
                ? null
                : new Contract( data.Serial, new ItemId( data.Item ), data.Count, new ItemId( data.RewardItem ), data.RewardCount, data.Relation, data.ExpiresAt );
        }
    }

    /// <summary>Makes the orders of a port from its profile: always the same one for the same game seed, port and serial number.</summary>
    public static class ContractGenerator {

        /// <summary>How many expeditions an order stays open (the shortest, plus up to two more).</summary>
        public const int MinLifetime = 3;

        /// <summary>What the reward is worth against what is asked for.</summary>
        public const float RewardPremium = 1.4f;

        /// <summary>Trust given per point of value asked for.</summary>
        public const float RelationPerValue = 0.5f;

        private const int MaxAsked = 15;
        private const int MaxReward = 10;

        // The most pieces of an offered good one order can give: its stock, at most MaxReward.
        private static int RewardCap( PortGood offer ) => Math.Max( 1, Math.Min( MaxReward, offer.Stock ) );

        /// <returns>Null when the port has nothing it wants that can be asked for, or nothing to pay with.</returns>
        public static Contract Create( PortProfile profile, IItemCatalog catalog, int level, int serial, int gameSeed, int completedExpeditions, ISet<ItemId> avoid = null ) {
            // Sorted, so that the order of a dictionary never decides anything.
            var wanted = profile.Wants.Keys
                .Where( item => catalog.TryGet( item, out var definition ) && definition.IsTradable )
                .OrderBy( item => item.Value, StringComparer.Ordinal )
                .ToList();
            if( avoid != null && wanted.Any( item => !avoid.Contains( item ) ) ) {
                wanted = wanted.Where( item => !avoid.Contains( item ) ).ToList();
            }
            if( wanted.Count == 0 ) {
                return null;
            }

            var rng = new DeterministicRandom( SeedMixer.Derive( gameSeed, SeedMixer.HashString( profile.Id ), serial ) );
            var item = wanted[ rng.NextInt( wanted.Count ) ];
            var asked = catalog.Get( item );
            float budget = 50f + 35f * level + rng.Range( 0f, 30f );
            int count = Math.Max( 1, Math.Min( MaxAsked, ( int ) Math.Round( budget / asked.BaseValue ) ) );
            double value = count * ( double ) asked.BaseValue;

            // What can pay for at least one piece of what is asked: the most the port can give of it is capped by its stock.
            var rewards = profile.Offers
                .Where( offer => offer.MinRelationLevel <= level && offer.Item != item && catalog.TryGet( offer.Item, out var definition ) && definition.IsTradable
                    && RewardCap( offer ) * catalog.Get( offer.Item ).BaseValue >= RewardPremium * asked.BaseValue )
                .OrderBy( offer => offer.Item.Value, StringComparer.Ordinal )
                .ToList();
            if( rewards.Count == 0 ) {
                return null;
            }
            // Signature goods (those the relation unlocked) are the better prize, half the time when there are any.
            var signature = rewards.Where( offer => offer.MinRelationLevel > 0 ).ToList();
            var pool = signature.Count > 0 && rng.NextFloat() < 0.5f ? signature : rewards;
            var reward = pool[ rng.NextInt( pool.Count ) ];
            int rewardBase = catalog.Get( reward.Item ).BaseValue;
            int rewardCount = ( int ) Math.Round( value * RewardPremium / rewardBase );
            int cap = RewardCap( reward );
            if( rewardCount > cap ) {
                // The port cannot give that much: ask for less, so that the reward stays worth the premium.
                rewardCount = cap;
                count = Math.Max( 1, ( int ) Math.Floor( rewardCount * ( double ) rewardBase / ( RewardPremium * asked.BaseValue ) ) );
                value = count * ( double ) asked.BaseValue;
            }
            rewardCount = Math.Max( 1, rewardCount );

            int expiresAt = completedExpeditions + MinLifetime + rng.NextInt( 3 );
            return new Contract( serial, item, count, reward.Item, rewardCount, value * RelationPerValue, expiresAt );
        }
    }


    /// <summary>The open orders of one port. They lapse with the expeditions, new ones come while there are free places.</summary>
    public sealed class ContractBoard {

        public const int MaxActive = 3;

        private readonly List<Contract> active = new List<Contract>();

        /// <summary>The serial number of the next order made (so that an order never repeats).</summary>
        public int NextSerial { get; private set; }

        public IReadOnlyList<Contract> Active => active;

        /// <summary>One place for an order at the start, one more for every relation level, up to <see cref="MaxActive"/>.</summary>
        public static int PlacesAt( int level ) => Math.Min( MaxActive, 1 + Math.Max( 0, level ) );

        /// <summary>Drops the orders that lapsed and fills the free places; returns how many orders were added.</summary>
        public int Refresh( PortProfile profile, IItemCatalog catalog, int level, int gameSeed, int completedExpeditions ) {
            active.RemoveAll( contract => contract.IsExpired( completedExpeditions ) );
            int added = 0;
            int places = PlacesAt( level );
            while( active.Count < places ) {
                var asking = new HashSet<ItemId>( active.Select( contract => contract.Item ) );
                var contract = ContractGenerator.Create( profile, catalog, level, NextSerial++, gameSeed, completedExpeditions, asking );
                if( contract == null ) {
                    break;
                }
                active.Add( contract );
                added++;
            }
            return added;
        }

        public bool Remove( Contract contract ) => active.Remove( contract );

        public ContractBoardData ToData() {
            var data = new ContractBoardData { NextSerial = NextSerial };
            foreach( var contract in active ) {
                data.Active.Add( contract.ToData() );
            }
            return data;
        }

        public static ContractBoard FromData( ContractBoardData data ) {
            var board = new ContractBoard();
            if( data == null ) {
                return board;
            }
            board.NextSerial = Math.Max( 0, data.NextSerial );
            foreach( var entry in data.Active ?? new List<ContractData>() ) {
                var contract = Contract.FromData( entry );
                if( contract != null ) {
                    board.active.Add( contract );
                }
            }
            return board;
        }
    }
}
