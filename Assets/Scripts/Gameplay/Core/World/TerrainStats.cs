using System;

namespace Hearthglade.Core.World {

    /// <summary>Min / max / mean of one terrain layer.</summary>
    public struct LayerStats {
        public double Min, Max, Mean;

        public static LayerStats Of( double[] values ) {
            if( values == null || values.Length == 0 ) {
                return default;
            }
            double min = double.MaxValue, max = double.MinValue, sum = 0;
            foreach( var value in values ) {
                if( value < min ) min = value;
                if( value > max ) max = value;
                sum += value;
            }
            return new LayerStats { Min = min, Max = max, Mean = sum / values.Length };
        }
    }

    /// <summary>What a generated terrain looks like in numbers: how much of the map each biome takes.</summary>
    public sealed class TerrainStats {
        public int CellCount { get; private set; }
        public LayerStats Height { get; private set; }
        public LayerStats Temperature { get; private set; }
        public LayerStats Humidity { get; private set; }

        /// <summary>Share (0..1) of the cells of each biome, indexed like the rule list given to the generator.</summary>
        public double[] BiomeShare { get; private set; }

        /// <summary>Share (0..1) of the cells that matched no biome.</summary>
        public double UnmatchedShare { get; private set; }

        public static TerrainStats Compute( GeneratedTerrain terrain, int biomeCount ) {
            if( terrain == null ) {
                throw new ArgumentNullException( nameof( terrain ) );
            }
            var counts = new int[ biomeCount ];
            int unmatched = 0;
            foreach( var biome in terrain.Biome ) {
                if( biome < 0 || biome >= biomeCount ) {
                    unmatched++;
                }
                else {
                    counts[ biome ]++;
                }
            }

            int cells = terrain.Biome.Length;
            var share = new double[ biomeCount ];
            for( int i = 0; i < biomeCount; i++ ) {
                share[ i ] = cells == 0 ? 0 : ( double ) counts[ i ] / cells;
            }
            return new TerrainStats {
                CellCount = cells,
                Height = LayerStats.Of( terrain.Height ),
                Temperature = LayerStats.Of( terrain.Temperature ),
                Humidity = LayerStats.Of( terrain.Humidity ),
                BiomeShare = share,
                UnmatchedShare = cells == 0 ? 0 : ( double ) unmatched / cells
            };
        }
    }
}
