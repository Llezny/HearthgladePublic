using Hearthglade.Core.World;
using Hearthglade.Gameplay.Environment.Block.Base;

namespace Hearthglade.Gameplay.Map.Generator.PerlinNoise {

    /// <summary>
    /// Bridge between the ScriptableObject noise configuration and the Unity-free generator in
    /// Hearthglade.Core.World. Nothing here mutates the assets.
    /// </summary>
    public static class PerlinNoiseGenerator {

        public static NoiseSettings ToSettings( PerlinNoisePreset preset ) {
            return new NoiseSettings( preset.Octaves, preset.Persistance, preset.Lacunarity, preset.Scale );
        }

        public static BiomeRule[] ToBiomeRules( PerlinNoiseMapConfig config ) {
            var rules = new BiomeRule[ config.Biomes.Count ];
            for( int i = 0; i < rules.Length; i++ ) {
                rules[ i ] = config.Biomes[ i ].ToRule();
            }
            return rules;
        }

        public static BiomePatch[] ToPatches( PerlinNoiseMapConfig config ) {
            var patches = new System.Collections.Generic.List<BiomePatch>();
            for( int i = 0; i < config.Biomes.Count; i++ ) {
                foreach( var patch in config.Biomes[ i ].Patches ) {
                    patches.Add( new BiomePatch { Biome = i, Visual = ( byte ) patch.Look, Coverage = patch.Coverage, Scale = patch.Scale, Salt = patch.Salt } );
                }
            }
            return patches.ToArray();
        }

        /// <summary>Per biome: true when its block is not ground (water), so cleanup treats it as sea.</summary>
        public static bool[] ToLiquidFlags( PerlinNoiseMapConfig config ) {
            var liquid = new bool[ config.Biomes.Count ];
            for( int i = 0; i < liquid.Length; i++ ) {
                var block = config.Biomes[ i ].Block != null ? config.Biomes[ i ].Block.GetComponent<Block>() : null;
                liquid[ i ] = block != null && block.blockSO != null && !block.blockSO.IsGround;
            }
            return liquid;
        }

        public static GeneratedTerrain Generate( PerlinNoiseMapConfig config, int size, int seed ) {
            return TerrainGenerator.Generate(
                size, seed,
                ToSettings( config.HeightNoisePreset ),
                ToSettings( config.TemperatureMapPreset ),
                ToSettings( config.HumidityMapPreset ),
                ToBiomeRules( config ),
                config.ToShape(),
                ToLiquidFlags( config ),
                ToPatches( config ) );
        }
    }
}
