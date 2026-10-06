using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>One thing a chest may hold.</summary>
    public struct LootEntry {
        public string Item;
        public int Min;
        public int Max;

        /// <summary>Relative chance among the entries allowed at the tier; 0 never drops.</summary>
        public float Weight;

        /// <summary>The entry only drops from this tier up, so far-away chests hold better things.</summary>
        public int MinTier;
    }

    public struct LootStack {
        public string Item;
        public int Count;
    }

    public static class LootRoller {

        private const int LootChannel = 950;

        /// <summary>Tier 0 up to <paramref name="tierStep"/> cells from the start, 1 up to twice that, and so on, at most <paramref name="maxTier"/>.</summary>
        public static int TierFor( float startDistance, float tierStep, int maxTier ) {
            if( tierStep <= 0f || maxTier <= 0 ) {
                return 0;
            }
            return Math.Min( maxTier, Math.Max( 0, ( int ) Math.Floor( startDistance / tierStep ) ) );
        }

        /// <summary>
        /// Rolls the contents of one chest: between <paramref name="rollsMin"/> and <paramref name="rollsMax"/> picks by weight from
        /// the entries allowed at the tier, equal items merged into one stack. Depends only on the arguments (seed and the
        /// site's cell), so the same world always has the same loot.
        /// </summary>
        public static void Roll( IReadOnlyList<LootEntry> entries, int rollsMin, int rollsMax, int tier, int seed, int cellX, int cellY, List<LootStack> result ) {
            result.Clear();
            if( entries == null || entries.Count == 0 ) {
                return;
            }
            float total = 0f;
            foreach( var entry in entries ) {
                if( entry.MinTier <= tier && entry.Weight > 0f ) {
                    total += entry.Weight;
                }
            }
            if( total <= 0f ) {
                return;
            }

            var random = new DeterministicRandom( SeedMixer.Derive( SeedMixer.Derive( seed, LootChannel ), cellX, cellY ) );
            int rolls = rollsMin + random.NextInt( Math.Max( rollsMin, rollsMax ) - rollsMin + 1 );
            for( int i = 0; i < rolls; i++ ) {
                float pick = random.NextFloat() * total;
                LootEntry chosen = default;
                bool any = false;
                foreach( var entry in entries ) {
                    if( entry.MinTier > tier || entry.Weight <= 0f ) {
                        continue;
                    }
                    chosen = entry;
                    any = true;
                    pick -= entry.Weight;
                    if( pick < 0f ) {
                        break;
                    }
                }
                if( !any ) {
                    return;
                }
                int min = Math.Max( 1, chosen.Min );
                int count = min + random.NextInt( Math.Max( min, chosen.Max ) - min + 1 );
                int existing = result.FindIndex( s => s.Item == chosen.Item );
                if( existing >= 0 ) {
                    result[ existing ] = new LootStack { Item = chosen.Item, Count = result[ existing ].Count + count };
                } else {
                    result.Add( new LootStack { Item = chosen.Item, Count = count } );
                }
            }
        }
    }
}
