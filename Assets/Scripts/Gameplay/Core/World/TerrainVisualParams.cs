namespace Hearthglade.Core.World {

    /// <summary>Grass look for one biome; TuftsPerTile 0 means no grass on that biome.</summary>
    public struct GrassStyle {
        public int TuftsPerTile;
        public Float3 Tint;
        public Float3 TipLighten;
    }

    /// <summary>
    /// Everything chunk-visual generation needs besides the terrain cells. A plain copy of the values of
    /// the TerrainVisualSO asset, so the generation can run on a worker thread and be unit tested.
    /// </summary>
    public sealed class TerrainVisualParams {
        public int ChunkSize = 13;
        public float TileSize = 0.3675f;

        /// <summary>How many cells around each texel are averaged into its biome weight (0 = old one-hot-per-block splat). Widens the blend zone between biomes to several blocks instead of ~1.</summary>
        public int BiomeBlendRadius = 2;

        public float BaseTopY = 0.4f;
        public float ElevatedRise = 0.2f;
        public float CliffDepth = 2f;
        public float Overhang = 0.08f;
        public float OverhangDrop = 0.03f;
        public float InnerOverhangScale = 0.5f;
        public bool EnableCliffs = true;

        public bool EnableGrass = true;
        public float GrassHeight = 0.12f;
        public float GrassWidth = 0.08f;
        public float GrassTileMargin = 0.7f;
        public int GrassNoiseSeed = 1337;
        public float GrassNoiseScale = 0.18f;
        public float GrassNoiseThreshold = 0.25f;
        public float GrassNoiseExponent = 1.8f;

        /// <summary>
        /// World-space direction the main camera looks (its Transform.forward). The gameplay camera never
        /// orbits - FollowCamera only translates, its rotation is fixed - so every grass card can be built
        /// once as a single flat quad standing vertically and yawed to face this direction, instead of a
        /// billboard-cross meant to look passable from any angle. Default matches Game.unity's MainCamera
        /// local rotation (-6, 6.13, -10 offset, quaternion (0.2204, 0.2599, -0.0611, 0.9382)).
        /// </summary>
        public Float3 GrassCameraForward = new Float3( 0.4607f, -0.4454f, 0.7678f );

        /// <summary>How many clump variants sit side by side in the grass texture atlas (each tuft picks one).</summary>
        public int GrassVariants = 1;

        /// <summary>Grass is meshed in blocks of about this many tiles per side so the camera can cull it per block.</summary>
        public int GrassCullCellSize = 4;

        /// <summary>Blade base brightness as a fraction of the biome tint (1 = no darkening at the root).</summary>
        public float GrassBaseDarken = 0.78f;

        /// <summary>Blade tip brightness as a multiple of the tip color (1 = exactly the ground color).</summary>
        public float GrassTipBrighten = 1f;

        /// <summary>Indexed by biome id; null or shorter than the biome count means no grass on the rest.</summary>
        public GrassStyle[] GrassByBiome = new GrassStyle[ 0 ];

        public GrassStyle GrassFor( int biome ) {
            return GrassByBiome != null && biome >= 0 && biome < GrassByBiome.Length ? GrassByBiome[ biome ] : default;
        }
    }
}
