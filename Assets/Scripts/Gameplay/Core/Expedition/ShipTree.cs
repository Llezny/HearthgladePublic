using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Expedition {

    /// <summary>What a node of the ship tree does.</summary>
    public enum ShipEffect {
        /// <summary>Share taken off the food and wood an expedition costs.</summary>
        ExpeditionDiscount = 0,
        /// <summary>Extra rows of slots in the backpack (a whole number).</summary>
        BackpackRows = 1,
        /// <summary>Chance that gathering gives one more of the yield.</summary>
        HarvestBonus = 2,
    }

    /// <summary>One step of a node: what it costs and how much it adds to the effect.</summary>
    public sealed class ShipNodeTier {
        public int Points { get; }
        public IReadOnlyList<ItemAmount> Ore { get; }
        public float Magnitude { get; }

        public ShipNodeTier( int points, IReadOnlyList<ItemAmount> ore, float magnitude ) {
            Points = Math.Max( 0, points );
            Ore = ore ?? new ItemAmount[ 0 ];
            Magnitude = magnitude;
        }
    }

    /// <summary>A branch of the ship tree: a few tiers of one effect, bought one after another.</summary>
    public sealed class ShipNode {
        public string Id { get; }
        public ShipEffect Effect { get; }
        public IReadOnlyList<ShipNodeTier> Tiers { get; }

        public ShipNode( string id, ShipEffect effect, IReadOnlyList<ShipNodeTier> tiers ) {
            if( string.IsNullOrEmpty( id ) ) {
                throw new ArgumentException( "A node needs an id", nameof( id ) );
            }
            Id = id;
            Effect = effect;
            Tiers = tiers ?? new ShipNodeTier[ 0 ];
        }
    }

    /// <summary>The saved form of <see cref="ShipTreeState"/>.</summary>
    [Serializable]
    public class ShipTreeData {
        public int Points;
        public Dictionary<string, int> Levels = new Dictionary<string, int>();
    }

    /// <summary>
    /// The expedition points of the player and how far each branch of the ship tree is bought (docs/EXPLORATION_LOOP_PLAN.md, phase 7).
    /// Points come with finished expeditions; a tier costs points and some ore, the ore is paid by the caller.
    /// </summary>
    public sealed class ShipTreeState {

        /// <summary>The cheapest an expedition can get through the discount, whatever is bought.</summary>
        public const float MinCostShare = 0.4f;

        /// <summary>The most the harvest bonus can add up to.</summary>
        public const float MaxHarvestChance = 0.6f;

        /// <summary>Points for an expedition that was finished, and one more for a new depth record.</summary>
        public const int PointsPerExpedition = 1;
        public const int PointsPerRecord = 1;

        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public int Points { get; private set; }

        public void Award( int points ) {
            Points = Math.Max( 0, Points + points );
        }

        public int LevelOf( ShipNode node ) => levels.TryGetValue( node.Id, out int level ) ? Math.Min( level, node.Tiers.Count ) : 0;

        public bool IsMaxed( ShipNode node ) => LevelOf( node ) >= node.Tiers.Count;

        /// <summary>The tier the next purchase of the node would give; null when it is bought out.</summary>
        public ShipNodeTier NextTier( ShipNode node ) => IsMaxed( node ) ? null : node.Tiers[ LevelOf( node ) ];

        public bool CanAffordPoints( ShipNode node ) {
            var tier = NextTier( node );
            return tier != null && Points >= tier.Points;
        }

        /// <summary>Buys the next tier for its points (the ore is the caller's to take first); false when it is bought out or too dear.</summary>
        public bool TryBuy( ShipNode node ) {
            var tier = NextTier( node );
            if( tier == null || Points < tier.Points ) {
                return false;
            }
            Points -= tier.Points;
            levels[ node.Id ] = LevelOf( node ) + 1;
            return true;
        }

        /// <summary>The sum of what the bought tiers add to an effect.</summary>
        public float Total( IEnumerable<ShipNode> nodes, ShipEffect effect ) {
            float total = 0f;
            foreach( var node in nodes ) {
                if( node.Effect != effect ) {
                    continue;
                }
                for( int i = 0; i < LevelOf( node ); i++ ) {
                    total += node.Tiers[ i ].Magnitude;
                }
            }
            return total;
        }

        /// <summary>What an expedition costs against its price (1 = full price).</summary>
        public float CostShare( IEnumerable<ShipNode> nodes ) => Math.Max( MinCostShare, 1f - Total( nodes, ShipEffect.ExpeditionDiscount ) );

        public int ExtraBackpackRows( IEnumerable<ShipNode> nodes ) => ( int ) Math.Round( Total( nodes, ShipEffect.BackpackRows ) );

        public float HarvestChance( IEnumerable<ShipNode> nodes ) => Math.Min( MaxHarvestChance, Total( nodes, ShipEffect.HarvestBonus ) );

        public ShipTreeData ToData() {
            var data = new ShipTreeData { Points = Points };
            foreach( var pair in levels ) {
                data.Levels[ pair.Key ] = pair.Value;
            }
            return data;
        }

        /// <summary>Loads a save; none (an old save) means no points and nothing bought.</summary>
        public void Load( ShipTreeData data ) {
            levels.Clear();
            Points = Math.Max( 0, data?.Points ?? 0 );
            foreach( var pair in data?.Levels ?? new Dictionary<string, int>() ) {
                if( !string.IsNullOrEmpty( pair.Key ) && pair.Value > 0 ) {
                    levels[ pair.Key ] = pair.Value;
                }
            }
        }
    }
}
