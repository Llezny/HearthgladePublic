namespace Hearthglade.Core.World {

    /// <summary>
    /// A cosmetic patch inside a biome: where a separate noise is high, the terrain looks like another biome
    /// (a bare spot in a meadow, a rocky outcrop in a taiga) while the block, its resources and walkability stay.
    /// </summary>
    public struct BiomePatch {

        /// <summary>Index of the biome rule the patches appear in.</summary>
        public int Biome;

        /// <summary>Terrain biome id (<see cref="TerrainBiome"/>) the patch looks like.</summary>
        public byte Visual;

        /// <summary>Share (0..1) of the map's cells covered by the noise peaks, inside or outside the biome.</summary>
        public double Coverage;

        /// <summary>Map cells per lattice unit of the patch noise: the size of a patch.</summary>
        public double Scale;

        /// <summary>Tells apart two patch kinds with the same settings.</summary>
        public int Salt;
    }
}
