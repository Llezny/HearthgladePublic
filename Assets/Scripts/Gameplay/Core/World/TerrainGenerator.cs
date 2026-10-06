using System;

namespace Hearthglade.Core.World {

    public struct NoiseSettings {
        public int Octaves;

        /// <summary>Amplitude multiplier per octave; below 1 the finer octaves add detail on top of the broad shape.</summary>
        public double Persistence;

        /// <summary>Frequency multiplier per octave; above 1 every octave is finer than the previous one.</summary>
        public double Lacunarity;

        /// <summary>Map cells per noise lattice unit of the first octave: the size of the broadest features. Larger is smoother.</summary>
        public double Scale;

        public NoiseSettings( int octaves, double persistence, double lacunarity, double scale = 1.0 ) {
            Octaves = octaves;
            Persistence = persistence;
            Lacunarity = lacunarity;
            Scale = scale;
        }

        internal double Frequency => Scale > 0 ? 1.0 / Scale : 1.0;
    }

    /// <summary>A biome is picked when height, temperature and humidity are each in [From, To).</summary>
    public struct BiomeRule {
        public float HeightFrom, HeightTo;
        public float TemperatureFrom, TemperatureTo;
        public float HumidityFrom, HumidityTo;

        public bool Matches( double height, double temperature, double humidity ) {
            return height >= HeightFrom && height < HeightTo
                && temperature >= TemperatureFrom && temperature < TemperatureTo
                && humidity >= HumidityFrom && humidity < HumidityTo;
        }
    }

    /// <summary>Result of terrain generation for a square map. Cell (x, z) lives at index z * Size + x.</summary>
    public sealed class GeneratedTerrain {
        public const short NoBiome = -1;

        public readonly int Size;
        public readonly double[] Height;
        public readonly double[] Temperature;
        public readonly double[] Humidity;

        /// <summary>Index into the rules list given to the generator, or <see cref="NoBiome"/>.</summary>
        public readonly short[] Biome;

        /// <summary>0 = no patch, otherwise the terrain biome id of the patch plus one.</summary>
        public readonly byte[] Patch;

        public int UnmatchedCount { get; internal set; }

        public GeneratedTerrain( int size ) {
            Size = size;
            int count = size * size;
            Height = new double[ count ];
            Temperature = new double[ count ];
            Humidity = new double[ count ];
            Biome = new short[ count ];
            Patch = new byte[ count ];
        }

        public int Index( int x, int z ) {
            return z * Size + x;
        }
    }

    public static class TerrainGenerator {

        private const int HeightChannel = 0;
        private const int TemperatureChannel = 1;
        private const int HumidityChannel = 2;
        private const int WarpXChannel = 3;
        private const int WarpZChannel = 4;
        private const int JitterTemperatureChannel = 5;
        private const int JitterHumidityChannel = 6;
        private const int PatchChannelBase = 100;

        /// <summary>
        /// Deterministic: the same seed and settings always give the same terrain, and each noise layer uses its
        /// own permutation table derived from the seed. Every layer is equalised (<see cref="LayerEqualizer"/>)
        /// before biomes are chosen, so biome ranges are shares of the map.
        /// </summary>
        /// <param name="shape">Island outline, warping and clean-up. Default (all zero) leaves the terrain as plain noise.</param>
        /// <param name="liquidBiomes">Per biome rule: true for water. Needed for the clean-up; null skips it.</param>
        /// <param name="patches">Cosmetic patches inside biomes, see <see cref="BiomePatch"/>; null = none.</param>
        public static GeneratedTerrain Generate(
            int size, int seed,
            NoiseSettings height, NoiseSettings temperature, NoiseSettings humidity,
            BiomeRule[] biomes,
            WorldShapeSettings shape = default,
            bool[] liquidBiomes = null,
            BiomePatch[] patches = null ) {

            if( size < 0 ) {
                throw new ArgumentOutOfRangeException( nameof( size ) );
            }
            if( biomes == null ) {
                throw new ArgumentNullException( nameof( biomes ) );
            }

            var data = new GeneratedTerrain( size );
            var heightNoise = new GradientNoise( SeedMixer.Derive( seed, HeightChannel ) );
            var temperatureNoise = new GradientNoise( SeedMixer.Derive( seed, TemperatureChannel ) );
            var humidityNoise = new GradientNoise( SeedMixer.Derive( seed, HumidityChannel ) );
            var warpXNoise = new GradientNoise( SeedMixer.Derive( seed, WarpXChannel ) );
            var warpZNoise = new GradientNoise( SeedMixer.Derive( seed, WarpZChannel ) );
            double warpFrequency = 1.0 / Math.Max( 1.0, shape.WarpScale );
            var coverage = shape.IslandRadius > 0 ? new double[ size * size ] : null;

            for( int z = 0; z < size; z++ ) {
                for( int x = 0; x < size; x++ ) {
                    int i = z * size + x;

                    // Domain warping: every layer is read at a displaced position, so coasts and biome borders
                    // wind instead of following the noise lattice.
                    double sx = x, sz = z;
                    if( shape.WarpStrength > 0 ) {
                        sx += warpXNoise.Octaves( x * warpFrequency, z * warpFrequency, 2, 2.0, 0.5 ) * shape.WarpStrength;
                        sz += warpZNoise.Octaves( x * warpFrequency, z * warpFrequency, 2, 2.0, 0.5 ) * shape.WarpStrength;
                    }

                    data.Height[ i ] = Sample( heightNoise, height, sx, sz );
                    if( coverage != null ) {
                        coverage[ i ] = shape.IslandCoverage( sx, sz, size );
                    }
                    data.Temperature[ i ] = Sample( temperatureNoise, temperature, sx, sz );
                    data.Humidity[ i ] = Sample( humidityNoise, humidity, sx, sz );
                }
            }

            LayerEqualizer.Equalize( data.Height );
            LayerEqualizer.Equalize( data.Temperature );
            LayerEqualizer.Equalize( data.Humidity );

            if( coverage != null ) {
                ApplyIsland( data.Height, coverage, shape.InteriorFloor );
            }

            if( shape.TemperatureOffset != 0 || shape.HumidityOffset != 0 ) {
                for( int i = 0; i < data.Temperature.Length; i++ ) {
                    data.Temperature[ i ] = Clamp( data.Temperature[ i ] + shape.TemperatureOffset );
                    data.Humidity[ i ] = Clamp( data.Humidity[ i ] + shape.HumidityOffset );
                }
            }

            if( shape.ClimateJitter > 0 ) {
                JitterClimate( data, seed, shape );
            }

            AssignBiomes( data, biomes );

            if( liquidBiomes != null ) {
                if( shape.KeepMainIslandOnly || shape.MinLakeSize > 0 ) {
                    IslandCleanup.Apply( data, biomes, liquidBiomes, shape );
                }
            }
            if( patches != null && patches.Length > 0 ) {
                ApplyPatches( data, seed, patches );
            }
            return data;
        }

        // Fine noise on the climate layers: the biome borders are decided by the layers, so they turn ragged.
        private static void JitterClimate( GeneratedTerrain data, int seed, WorldShapeSettings shape ) {
            var temperatureNoise = new GradientNoise( SeedMixer.Derive( seed, JitterTemperatureChannel ) );
            var humidityNoise = new GradientNoise( SeedMixer.Derive( seed, JitterHumidityChannel ) );
            double frequency = 1.0 / Math.Max( 1.0, shape.ClimateJitterScale );
            for( int z = 0; z < data.Size; z++ ) {
                for( int x = 0; x < data.Size; x++ ) {
                    int i = z * data.Size + x;
                    data.Temperature[ i ] = Clamp( data.Temperature[ i ] + temperatureNoise.Octaves( x * frequency, z * frequency, 2, 2.0, 0.5 ) * shape.ClimateJitter );
                    data.Humidity[ i ] = Clamp( data.Humidity[ i ] + humidityNoise.Octaves( x * frequency, z * frequency, 2, 2.0, 0.5 ) * shape.ClimateJitter );
                }
            }
        }

        // Layers live in [-1, 1) and biome ranges are half open: a jittered value must not leave that interval.
        private static double Clamp( double value ) {
            return Math.Max( -1.0, Math.Min( 0.999999, value ) );
        }

        private static void ApplyPatches( GeneratedTerrain data, int seed, BiomePatch[] patches ) {
            var values = new double[ data.Biome.Length ];
            foreach( var patch in patches ) {
                if( patch.Coverage <= 0 ) {
                    continue;
                }
                var noise = new GradientNoise( SeedMixer.Derive( seed, PatchChannelBase + patch.Salt ) );
                double frequency = 1.0 / Math.Max( 1.0, patch.Scale );
                for( int z = 0; z < data.Size; z++ ) {
                    for( int x = 0; x < data.Size; x++ ) {
                        values[ z * data.Size + x ] = noise.Octaves( x * frequency, z * frequency, 2, 2.0, 0.5 );
                    }
                }
                // Equalised, so a coverage of 0.2 really is a fifth of the map.
                LayerEqualizer.Equalize( values );
                double threshold = 1.0 - 2.0 * Math.Min( 1.0, patch.Coverage );
                for( int i = 0; i < values.Length; i++ ) {
                    if( data.Patch[ i ] == 0 && data.Biome[ i ] == patch.Biome && values[ i ] >= threshold ) {
                        data.Patch[ i ] = ( byte ) ( patch.Visual + 1 );
                    }
                }
            }
        }

        // The (equalised) noise becomes the inland ground, spread over [interiorFloor, 1); the coast then pulls it down
        // to -1 (sea) wherever the island coverage grows. The coast follows the noise: high ground reaches further out.
        private static void ApplyIsland( double[] height, double[] coverage, double interiorFloor ) {
            for( int i = 0; i < height.Length; i++ ) {
                double inland = interiorFloor + ( height[ i ] + 1.0 ) / 2.0 * ( 1.0 - interiorFloor );
                height[ i ] = inland * ( 1.0 - coverage[ i ] ) - coverage[ i ];
            }
        }

        internal static void AssignBiomes( GeneratedTerrain data, BiomeRule[] biomes ) {
            data.UnmatchedCount = 0;
            for( int i = 0; i < data.Biome.Length; i++ ) {
                data.Biome[ i ] = FindBiome( biomes, data.Height[ i ], data.Temperature[ i ], data.Humidity[ i ] );
                if( data.Biome[ i ] == GeneratedTerrain.NoBiome ) {
                    data.UnmatchedCount++;
                }
            }
        }

        private static double Sample( GradientNoise noise, NoiseSettings settings, double x, double z ) {
            double frequency = settings.Frequency;
            return noise.Octaves( x * frequency, z * frequency, settings.Octaves, settings.Lacunarity, settings.Persistence );
        }

        public static short FindBiome( BiomeRule[] biomes, double height, double temperature, double humidity ) {
            for( int i = 0; i < biomes.Length; i++ ) {
                if( biomes[ i ].Matches( height, temperature, humidity ) ) {
                    return ( short ) i;
                }
            }
            return GeneratedTerrain.NoBiome;
        }
    }
}
