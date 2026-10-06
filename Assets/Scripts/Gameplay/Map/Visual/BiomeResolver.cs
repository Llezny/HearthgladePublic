using Hearthglade.Core.World;
using Hearthglade.Gameplay.Environment.Block.Base;

namespace Hearthglade.Gameplay.Map.Visual
{
    /// <summary>Tells the terrain visuals which look a block asset has: the Biome field of the asset.</summary>
    public static class BiomeResolver
    {
        // The Core terrain grid stores plain ids; they must stay in step with the serialized BiomeId enum.
        // A mismatch turns this constant's division into a compile error.
        private const int BiomeIdsMatchCore =
            1 / ((int)BiomeId.Grass == TerrainBiome.Grass && (int)BiomeId.Dirt == TerrainBiome.Dirt
                 && (int)BiomeId.Sand == TerrainBiome.Sand && (int)BiomeId.Stone == TerrainBiome.Stone
                 && (int)BiomeId.Taiga == TerrainBiome.Taiga && (int)BiomeId.Forest == TerrainBiome.Forest
                 && (int)BiomeId.Swamp == TerrainBiome.Swamp && (int)BiomeId.Tundra == TerrainBiome.Tundra ? 1 : 0);

        public static BiomeId Resolve(BlockSO so)
        {
            return so == null ? BiomeId.Grass : so.Biome;
        }
    }
}
