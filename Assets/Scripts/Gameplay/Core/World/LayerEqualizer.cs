using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Remaps a noise layer so its values are spread evenly over [-1, 1). Perlin noise never gets near +-1 and
    /// its spread changes from seed to seed, so fixed biome thresholds gave lopsided maps (4% or 28% tundra,
    /// depending on the seed). After equalising, a range of width w always covers w/2 of the map, and the
    /// designer's numbers read as shares: "water below -0.8" means the lowest tenth of the cells.
    /// </summary>
    public static class LayerEqualizer {

        /// <summary>
        /// Replaces every value by its rank: the lowest cell gets just above -1, the highest just below +1,
        /// so a half-open biome range [From, 1) covers the top of the layer too. Deterministic; the order of
        /// equal values follows the cell index.
        /// </summary>
        public static void Equalize( double[] values ) {
            if( values == null ) {
                throw new ArgumentNullException( nameof( values ) );
            }
            int count = values.Length;
            if( count == 0 ) {
                return;
            }

            var order = new int[ count ];
            var keys = new double[ count ];
            for( int i = 0; i < count; i++ ) {
                order[ i ] = i;
                keys[ i ] = values[ i ];
            }
            Array.Sort( order, ( a, b ) => {
                int byValue = keys[ a ].CompareTo( keys[ b ] );
                return byValue != 0 ? byValue : a.CompareTo( b );
            } );

            for( int rank = 0; rank < count; rank++ ) {
                values[ order[ rank ] ] = ( rank + 0.5 ) / count * 2.0 - 1.0;
            }
        }
    }
}
