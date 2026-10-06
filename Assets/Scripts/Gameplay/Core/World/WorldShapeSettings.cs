using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// The outline and relief of a map on top of the plain noise layers: one island with a coast, winding borders,
    /// no leftover islets or puddles. All zero (the default) means none of it.
    /// </summary>
    public struct WorldShapeSettings {

        /// <summary>
        /// Where the coast lies on average, as a share of the half map width (1 = the middle of an edge, 0 = no island mask).
        /// The corners are further away (1.41), so they are always sea.
        /// </summary>
        public double IslandRadius;

        /// <summary>Half width of the fade to sea around the radius, as a share of the half map width.</summary>
        public double IslandFalloff;

        /// <summary>
        /// Height of the lowest inland ground before the coast fades it out (-1..1). The noise is spread over
        /// [InteriorFloor, 1), so with -0.5 and water below -0.4 about 7% of the inland cells are lakes, and 0 means no lakes.
        /// </summary>
        public double InteriorFloor;

        /// <summary>Map cells per lattice unit of the noise that bends every layer (the size of the bends).</summary>
        public double WarpScale;

        /// <summary>How far, in cells, the warp can move a sample. 0 = no warp.</summary>
        public double WarpStrength;

        /// <summary>
        /// How far (in equalised climate units, -1..1) a fine noise moves temperature and humidity before biomes are picked,
        /// so borders between biomes are ragged instead of smooth contours. 0 = no jitter.
        /// </summary>
        public double ClimateJitter;

        /// <summary>Map cells per lattice unit of the jitter noise (small = fine, ragged borders).</summary>
        public double ClimateJitterScale;

        /// <summary>
        /// Shifts the whole map along the equalised climate layers (-1..1, added after equalising, values pile up at the ends):
        /// positive temperature = warmer, positive humidity = wetter. 0 = the map as the config describes it.
        /// </summary>
        public double TemperatureOffset;
        public double HumidityOffset;

        /// <summary>Only the largest piece of land stays, any other becomes sea.</summary>
        public bool KeepMainIslandOnly;

        /// <summary>Lakes smaller than this many cells (not connected to the sea) are filled in. 0 = keep all.</summary>
        public int MinLakeSize;

        /// <summary>How much of a position is sea: 0 well inside the island, 1 out at sea, smooth in between.</summary>
        public double IslandCoverage( double x, double z, int size ) {
            if( IslandRadius <= 0 ) {
                return 0.0;
            }
            double half = ( size - 1 ) / 2.0;
            if( half <= 0 ) {
                return 0.0;
            }
            double dx = ( x - half ) / half;
            double dz = ( z - half ) / half;
            double distance = Math.Sqrt( dx * dx + dz * dz );
            double falloff = Math.Max( 1e-6, IslandFalloff );
            double t = ( distance - ( IslandRadius - falloff ) ) / ( 2 * falloff );
            t = Math.Max( 0.0, Math.Min( 1.0, t ) );
            return t * t * ( 3 - 2 * t );
        }
    }
}
