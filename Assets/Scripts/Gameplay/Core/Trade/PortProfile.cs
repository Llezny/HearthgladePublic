using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Trade {

    /// <summary>A good a port sells: how much it keeps in stock, how cheap it is and from which relation level on.</summary>
    public sealed class PortGood {
        public ItemId Item { get; }

        /// <summary>How many the port keeps; it is refilled to this number.</summary>
        public int Stock { get; }

        /// <summary>Local price against the base value: below 1 is cheap (the port has plenty of it).</summary>
        public float PriceMultiplier { get; }

        /// <summary>Signature goods stay locked until the relation reaches this level.</summary>
        public int MinRelationLevel { get; }

        public PortGood( ItemId item, int stock, float priceMultiplier = 1f, int minRelationLevel = 0 ) {
            if( item.IsEmpty ) {
                throw new ArgumentException( "A good needs an item", nameof( item ) );
            }
            Item = item;
            Stock = Math.Max( 0, stock );
            PriceMultiplier = Math.Max( 0.01f, priceMultiplier );
            MinRelationLevel = Math.Max( 0, minRelationLevel );
        }
    }

    /// <summary>
    /// The fixed character of one port, engine free. Prices are local: the port values an item at base value x its multiplier
    /// (cheap for what it offers, dear for what it wants) and sells it for that plus its margin, buys it for that minus the margin.
    /// Buying and selling use one price, so going round in one port can never pay.
    /// </summary>
    public sealed class PortProfile {
        public string Id { get; }

        /// <summary>The port is discovered once the player has completed this many expeditions.</summary>
        public int UnlockAfterExpeditions { get; }

        public IReadOnlyList<PortGood> Offers { get; }

        /// <summary>What the port pays extra for (multiplier above 1).</summary>
        public IReadOnlyDictionary<ItemId, float> Wants { get; }

        /// <summary>Relation points needed for level 1, 2, ... (level 0 starts at 0).</summary>
        public IReadOnlyList<double> LevelThresholds { get; }

        /// <summary>Margin per relation level; the last one counts for every higher level.</summary>
        public IReadOnlyList<float> Margins { get; }

        /// <summary>The stock is refilled every this many completed expeditions.</summary>
        public int RestockEveryExpeditions { get; }

        /// <summary>Each unit the player sells of an item makes the next one worth this share of it (below 1).</summary>
        public float SaturationPerUnit { get; }

        /// <summary>How many sold units of every item the port forgets per refill.</summary>
        public float SaturationRecovery { get; }

        public PortProfile( string id, int unlockAfterExpeditions, IReadOnlyList<PortGood> offers, IReadOnlyDictionary<ItemId, float> wants = null,
            IReadOnlyList<double> levelThresholds = null, IReadOnlyList<float> margins = null,
            int restockEveryExpeditions = 3, float saturationPerUnit = 0.95f, float saturationRecovery = 5f ) {
            if( string.IsNullOrEmpty( id ) ) {
                throw new ArgumentException( "A port needs an id", nameof( id ) );
            }
            Id = id;
            UnlockAfterExpeditions = Math.Max( 0, unlockAfterExpeditions );
            Offers = offers ?? new PortGood[ 0 ];
            Wants = wants ?? new Dictionary<ItemId, float>();
            LevelThresholds = levelThresholds ?? new double[] { 150, 500 };
            Margins = margins != null && margins.Count > 0 ? margins : new[] { 0.25f, 0.15f, 0.10f };
            RestockEveryExpeditions = Math.Max( 1, restockEveryExpeditions );
            SaturationPerUnit = Math.Min( 1f, Math.Max( 0.01f, saturationPerUnit ) );
            SaturationRecovery = Math.Max( 0f, saturationRecovery );
        }

        public int LevelFor( double relationPoints ) {
            int level = 0;
            while( level < LevelThresholds.Count && relationPoints >= LevelThresholds[ level ] ) {
                level++;
            }
            return level;
        }

        public float MarginAt( int level ) {
            return Margins[ Math.Min( Math.Max( 0, level ), Margins.Count - 1 ) ];
        }

        public bool TryGetOffer( ItemId item, out PortGood good ) {
            foreach( var offer in Offers ) {
                if( offer.Item == item ) {
                    good = offer;
                    return true;
                }
            }
            good = null;
            return false;
        }

        /// <summary>The local price of an item against its base value: the offer's multiplier, else the demand, else 1.</summary>
        public float PriceMultiplierOf( ItemId item ) {
            if( TryGetOffer( item, out var offer ) ) {
                return offer.PriceMultiplier;
            }
            return Wants.TryGetValue( item, out float demand ) ? demand : 1f;
        }
    }
}
