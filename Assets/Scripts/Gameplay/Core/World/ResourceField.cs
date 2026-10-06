using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>One biome's share of the neighbourhood of a cell.</summary>
    public struct BiomeShare {
        public int Biome;
        public float Weight;
    }

    /// <summary>
    /// What the resource rules read about a map besides the cell itself: a density field (groves and clearings),
    /// the distance to the player's start, the vicinity of water, and how the biomes blend around a cell.
    /// Built once per map from the generated terrain; everything is deterministic for a seed.
    /// </summary>
    public sealed class ResourceField {

        /// <summary>A cell counts as near water when a liquid cell lies within this many cells.</summary>
        public const int WaterRadius = 2;

        /// <summary>Biomes blend over a window of this radius around a cell, so a border is a few cells of thinning.</summary>
        public const int MixRadius = 2;

        /// <summary>Start of the player's region: the standable regions smaller than this are not a start.</summary>
        private const int MinStartRegion = 5;

        private const int DensityChannel = 200;

        private const int PatchChannel = 300;

        private readonly GeneratedTerrain terrain;
        private readonly int seed;
        private readonly Dictionary<int, byte[]> patchFields = new Dictionary<int, byte[]>( );
        private readonly float[] density;
        private readonly bool[] nearWater;
        private readonly int biomeCount;
        private readonly int[] windowCounts;
        private readonly bool[] liquidBiomes;
        private bool[] reserved;

        /// <summary>Cells around the player's start in which nothing grows, so the player does not begin inside a tree.</summary>
        public const int StartClearRadius = 3;

        public int Size => terrain.Size;
        public int StartX { get; }
        public int StartZ { get; }

        private ResourceField( GeneratedTerrain terrain, int seed, int biomeCount, bool[] liquidBiomes, float[] density, bool[] nearWater, int startX, int startZ ) {
            this.liquidBiomes = liquidBiomes;
            this.terrain = terrain;
            this.seed = seed;
            this.biomeCount = biomeCount;
            this.density = density;
            this.nearWater = nearWater;
            this.windowCounts = new int[ biomeCount ];
            StartX = startX;
            StartZ = startZ;
        }

        /// <param name="liquidBiomes">Per biome rule: true for water (not ground).</param>
        /// <param name="densityScale">Map cells per lattice unit of the density noise: the size of a grove or a clearing.</param>
        public static ResourceField Build( GeneratedTerrain terrain, int seed, bool[] liquidBiomes, double densityScale ) {
            if( terrain == null ) {
                throw new ArgumentNullException( nameof( terrain ) );
            }
            int size = terrain.Size;
            var density = BuildDensity( size, seed, densityScale );

            bool IsLiquid( int cell ) {
                int biome = terrain.Biome[ cell ];
                return biome != GeneratedTerrain.NoBiome && liquidBiomes != null && liquidBiomes[ biome ];
            }

            var nearWater = new bool[ size * size ];
            for( int z = 0; z < size; z++ ) {
                for( int x = 0; x < size; x++ ) {
                    if( !IsLiquid( z * size + x ) ) {
                        continue;
                    }
                    for( int dz = -WaterRadius; dz <= WaterRadius; dz++ ) {
                        for( int dx = -WaterRadius; dx <= WaterRadius; dx++ ) {
                            int nx = x + dx, nz = z + dz;
                            if( nx >= 0 && nz >= 0 && nx < size && nz < size ) {
                                nearWater[ nz * size + nx ] = true;
                            }
                        }
                    }
                }
            }

            bool Standable( int x, int z ) {
                if( x < 0 || z < 0 || x >= size || z >= size ) {
                    return false;
                }
                int biome = terrain.Biome[ z * size + x ];
                return biome != GeneratedTerrain.NoBiome && !IsLiquid( z * size + x );
            }

            if( !PlayerStartFinder.TryFind( size, Standable, MinStartRegion, out int startX, out int startZ ) ) {
                startX = size / 2;
                startZ = size / 2;
            }
            var field = new ResourceField( terrain, seed, liquidBiomes != null ? liquidBiomes.Length : 0, liquidBiomes, density, nearWater, startX, startZ );
            field.Reserve( startX, startZ, StartClearRadius );
            return field;
        }

        // Equalised like the climate layers, so a density band is a share of the map: "above 0.5" is half of it.
        private static float[] BuildDensity( int size, int seed, double scale ) {
            var noise = new GradientNoise( SeedMixer.Derive( seed, DensityChannel ) );
            double frequency = 1.0 / Math.Max( 1.0, scale );
            var values = new double[ size * size ];
            for( int z = 0; z < size; z++ ) {
                for( int x = 0; x < size; x++ ) {
                    values[ z * size + x ] = noise.Octaves( x * frequency, z * frequency, 2, 2.0, 0.5 );
                }
            }
            LayerEqualizer.Equalize( values );
            var result = new float[ values.Length ];
            for( int i = 0; i < values.Length; i++ ) {
                result[ i ] = ( float ) ( ( values[ i ] + 1.0 ) / 2.0 );
            }
            return result;
        }

        /// <summary>Index of the biome of a cell, or <see cref="GeneratedTerrain.NoBiome"/> outside the map or where none matched.</summary>
        public int BiomeAt( int x, int z ) {
            if( x < 0 || z < 0 || x >= terrain.Size || z >= terrain.Size ) {
                return GeneratedTerrain.NoBiome;
            }
            return terrain.Biome[ z * terrain.Size + x ];
        }

        /// <summary>True for a cell that holds a biome and is not liquid: resources may grow there.</summary>
        public bool IsGround( int x, int z ) {
            if( x < 0 || z < 0 || x >= terrain.Size || z >= terrain.Size ) {
                return false;
            }
            int biome = terrain.Biome[ z * terrain.Size + x ];
            return biome != GeneratedTerrain.NoBiome && !( liquidBiomes != null && biome < liquidBiomes.Length && liquidBiomes[ biome ] );
        }

        /// <summary>Keeps every cell within <paramref name="radius"/> cells (a disc) of the centre free of resources.</summary>
        public void Reserve( int centerX, int centerZ, float radius ) {
            int size = terrain.Size;
            reserved ??= new bool[ size * size ];
            int reach = ( int ) Math.Ceiling( radius );
            for( int z = Math.Max( 0, centerZ - reach ); z <= Math.Min( size - 1, centerZ + reach ); z++ ) {
                for( int x = Math.Max( 0, centerX - reach ); x <= Math.Min( size - 1, centerX + reach ); x++ ) {
                    double dx = x - centerX, dz = z - centerZ;
                    if( dx * dx + dz * dz <= radius * radius ) {
                        reserved[ z * size + x ] = true;
                    }
                }
            }
        }

        /// <summary>
        /// The patch field of one rule at a cell, 0..1 and spread evenly over the map (so "above 0.6" is 40 % of it). Each rule of each
        /// table has its own noise, <see cref="ResourceRule.PatchScale"/> cells per lattice unit, built on first use.
        /// Not thread safe, like the rest of the field.
        /// </summary>
        public float PatchAt( int table, int ruleIndex, in ResourceRule rule, int x, int z ) {
            int key = table * 1024 + ruleIndex;
            if( !patchFields.TryGetValue( key, out var values ) ) {
                values = BuildPatch( terrain.Size, SeedMixer.Derive( seed, PatchChannel + key ), rule.PatchScale );
                patchFields[ key ] = values;
            }
            return values[ z * terrain.Size + x ] / 255f;
        }

        private static byte[] BuildPatch( int size, int patchSeed, double scale ) {
            var noise = new GradientNoise( patchSeed );
            double frequency = 1.0 / Math.Max( 1.0, scale );
            var values = new double[ size * size ];
            for( int z = 0; z < size; z++ ) {
                for( int x = 0; x < size; x++ ) {
                    values[ z * size + x ] = noise.Octaves( x * frequency, z * frequency, 2, 2.0, 0.5 );
                }
            }
            LayerEqualizer.Equalize( values );
            var bytes = new byte[ values.Length ];
            for( int i = 0; i < bytes.Length; i++ ) {
                bytes[ i ] = ( byte ) Math.Round( ( values[ i ] + 1.0 ) / 2.0 * 255.0 );
            }
            return bytes;
        }

        public bool IsReserved( int x, int z ) {
            return reserved != null && x >= 0 && z >= 0 && x < terrain.Size && z < terrain.Size && reserved[ z * terrain.Size + x ];
        }

        public ResourceContext ContextAt( int x, int z ) {
            int i = z * terrain.Size + x;
            double dx = x - StartX, dz = z - StartZ;
            return new ResourceContext {
                Density = density[ i ],
                StartDistance = ( float ) Math.Sqrt( dx * dx + dz * dz ),
                NearWater = nearWater[ i ],
                Temperature = ( float ) terrain.Temperature[ i ],
                Humidity = ( float ) terrain.Humidity[ i ]
            };
        }

        /// <summary>
        /// The biomes around a cell with their share of the window (cells outside the map or without a biome do not
        /// count): inside a biome that is the biome alone with weight 1, on a border each side gets its part.
        /// Not thread safe: it reuses an internal buffer.
        /// </summary>
        public void MixAt( int x, int z, List<BiomeShare> result ) {
            result.Clear();
            int size = terrain.Size;
            Array.Clear( windowCounts, 0, windowCounts.Length );
            int total = 0;
            for( int nz = Math.Max( 0, z - MixRadius ); nz <= Math.Min( size - 1, z + MixRadius ); nz++ ) {
                for( int nx = Math.Max( 0, x - MixRadius ); nx <= Math.Min( size - 1, x + MixRadius ); nx++ ) {
                    int biome = terrain.Biome[ nz * size + nx ];
                    if( biome == GeneratedTerrain.NoBiome || biome >= biomeCount ) {
                        continue;
                    }
                    windowCounts[ biome ]++;
                    total++;
                }
            }
            if( total == 0 ) {
                return;
            }
            for( int biome = 0; biome < biomeCount; biome++ ) {
                if( windowCounts[ biome ] > 0 ) {
                    result.Add( new BiomeShare { Biome = biome, Weight = windowCounts[ biome ] / ( float ) total } );
                }
            }
        }
    }
}
