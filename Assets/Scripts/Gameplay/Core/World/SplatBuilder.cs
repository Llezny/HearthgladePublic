using System;

namespace Hearthglade.Core.World {

    public sealed class SplatResult {
        /// <summary>Side of the (chunkSize + 2)^2 texture: one extra texel around the chunk for seamless blending.</summary>
        public int TextureSize;

        /// <summary>RGBA bytes, row by row. Channels of set A: grass, dirt, sand, stone.</summary>
        public byte[] SplatA;

        /// <summary>RGBA bytes. Set B holds further biomes: taiga in R, forest in G, swamp in B.</summary>
        public byte[] SplatB;

        /// <summary>chunkSize * chunkSize flags, [y * chunkSize + x]: the chunk's cell is ground.</summary>
        public bool[] CellExists;

        public bool AnyGround;
    }

    /// <summary>Per-chunk biome splat maps: which biome each texel of the terrain shader should blend towards.</summary>
    public static class SplatBuilder {

        /// <param name="blendRadius">
        /// Cells around each texel averaged into its weight (0 = old one-hot-per-block splat, i.e. the blend
        /// zone is whatever the GPU's bilinear filtering does over 1 texel = 1 block). The window passed in
        /// must cover the chunk with a margin of at least 1 + blendRadius (see
        /// <see cref="ChunkVisualComputer.WindowMargin"/>) or texels near the chunk edge silently lose taps and
        /// no longer match the neighbour chunk's blur, which would show as a seam.
        /// </param>
        public static SplatResult Build( TerrainWindow window, int chunkOriginX, int chunkOriginY, int chunkSize, int blendRadius = 0 ) {
            int texSize = chunkSize + 2;
            var pixelsA = new byte[ texSize * texSize * 4 ];
            var pixelsB = new byte[ texSize * texSize * 4 ];
            var cellExists = new bool[ chunkSize * chunkSize ];
            var pixelHasBiome = new bool[ texSize * texSize ];
            int fallbackBiome = TerrainBiome.Grass;
            bool fallbackFound = false;
            bool anyGround = false;
            var weights = new float[ 8 ];

            for( int q = 0; q < texSize; q++ ) {
                for( int p = 0; p < texSize; p++ ) {
                    int worldX = chunkOriginX + p - 1;
                    int worldY = chunkOriginY + q - 1;
                    var cell = window.Get( worldX, worldY );
                    if( !cell.Ground ) {
                        continue;
                    }

                    int idx = q * texSize + p;
                    if( blendRadius <= 0 ) {
                        WriteBiome( cell.Biome, pixelsA, pixelsB, idx );
                    }
                    else {
                        Array.Clear( weights, 0, weights.Length );
                        float total = 0f;
                        // Effective elevation, not the raw bit: a cell flattened by a floor tile
                        // (docs/BUILDING_SYSTEM_PLAN.md section 9) renders at base height even though
                        // Elevated is still set, so comparing Elevated alone would disagree with the
                        // mesh's own height (TerrainMeshBuilder, via TopY) about whether a step exists here.
                        bool cellEffectivelyElevated = cell.Elevated && !cell.Flattened;
                        for( int dq = -blendRadius; dq <= blendRadius; dq++ ) {
                            for( int dp = -blendRadius; dp <= blendRadius; dp++ ) {
                                var neighbor = window.Get( worldX + dp, worldY + dq );
                                // Only blur with cells at the same (effective) height: mixing weights
                                // across an elevation step here would bake the "wrong" biome's color into
                                // this texel even though the mesh (TerrainMeshBuilder.CornerCanBlend) is
                                // correctly refusing to sample across that same step.
                                bool neighborEffectivelyElevated = neighbor.Elevated && !neighbor.Flattened;
                                if( !neighbor.Ground || neighborEffectivelyElevated != cellEffectivelyElevated ) {
                                    continue;
                                }
                                weights[ BiomeChannel( neighbor.Biome ) ] += 1f;
                                total += 1f;
                            }
                        }
                        WriteWeights( weights, total, pixelsA, pixelsB, idx );
                    }
                    pixelHasBiome[ idx ] = true;

                    if( p >= 1 && p <= chunkSize && q >= 1 && q <= chunkSize ) {
                        cellExists[ ( q - 1 ) * chunkSize + ( p - 1 ) ] = true;
                        anyGround = true;
                    }
                    if( !fallbackFound ) {
                        fallbackBiome = cell.Biome;
                        fallbackFound = true;
                    }
                }
            }

            // Texels of missing cells copy the nearest texel that has a biome, so blending at the map edge
            // and next to holes never fades to black.
            for( int q = 0; q < texSize; q++ ) {
                for( int p = 0; p < texSize; p++ ) {
                    int idx = q * texSize + p;
                    if( pixelHasBiome[ idx ] ) {
                        continue;
                    }
                    if( FindNearest( pixelHasBiome, texSize, p, q, out int nearest ) ) {
                        Array.Copy( pixelsA, nearest * 4, pixelsA, idx * 4, 4 );
                        Array.Copy( pixelsB, nearest * 4, pixelsB, idx * 4, 4 );
                    }
                    else {
                        WriteBiome( fallbackBiome, pixelsA, pixelsB, idx );
                    }
                }
            }

            return new SplatResult {
                TextureSize = texSize,
                SplatA = pixelsA,
                SplatB = pixelsB,
                CellExists = cellExists,
                AnyGround = anyGround
            };
        }

        private static void WriteBiome( int biome, byte[] pixelsA, byte[] pixelsB, int pixelIndex ) {
            int o = pixelIndex * 4;
            switch( biome ) {
                case TerrainBiome.Dirt:  pixelsA[ o + 1 ] = 255; break;
                case TerrainBiome.Sand:  pixelsA[ o + 2 ] = 255; break;
                case TerrainBiome.Stone: pixelsA[ o + 3 ] = 255; break;
                case TerrainBiome.Taiga: pixelsB[ o ] = 255; break;
                case TerrainBiome.Forest: pixelsB[ o + 1 ] = 255; break;
                case TerrainBiome.Swamp: pixelsB[ o + 2 ] = 255; break;
                case TerrainBiome.Tundra: pixelsB[ o + 3 ] = 255; break;
                default:                 pixelsA[ o ] = 255; break; // grass, and any unknown biome
            }
        }

        // Index into the 8-wide blur accumulator, matching WriteBiome's channel layout
        // (grass, dirt, sand, stone, taiga, forest, swamp, tundra).
        private static int BiomeChannel( int biome ) {
            switch( biome ) {
                case TerrainBiome.Dirt:   return 1;
                case TerrainBiome.Sand:   return 2;
                case TerrainBiome.Stone:  return 3;
                case TerrainBiome.Taiga:  return 4;
                case TerrainBiome.Forest: return 5;
                case TerrainBiome.Swamp:  return 6;
                case TerrainBiome.Tundra: return 7;
                default:                  return 0; // grass, and any unknown biome
            }
        }

        private static void WriteWeights( float[] weights, float total, byte[] pixelsA, byte[] pixelsB, int pixelIndex ) {
            int o = pixelIndex * 4;
            pixelsA[ o + 0 ] = ToByte( weights[ 0 ], total );
            pixelsA[ o + 1 ] = ToByte( weights[ 1 ], total );
            pixelsA[ o + 2 ] = ToByte( weights[ 2 ], total );
            pixelsA[ o + 3 ] = ToByte( weights[ 3 ], total );
            pixelsB[ o + 0 ] = ToByte( weights[ 4 ], total );
            pixelsB[ o + 1 ] = ToByte( weights[ 5 ], total );
            pixelsB[ o + 2 ] = ToByte( weights[ 6 ], total );
            pixelsB[ o + 3 ] = ToByte( weights[ 7 ], total );
        }

        private static byte ToByte( float weight, float total ) {
            if( total <= 0f ) {
                return 0;
            }
            int value = ( int ) ( weight / total * 255f + 0.5f );
            return ( byte ) Math.Clamp( value, 0, 255 );
        }

        // Ring by ring around the texel, in the same scan order the shader-side art was tuned with.
        private static bool FindNearest( bool[] hasBiome, int size, int i, int j, out int index ) {
            for( int r = 1; r < size; r++ ) {
                for( int dj = -r; dj <= r; dj++ ) {
                    for( int di = -r; di <= r; di++ ) {
                        if( Math.Abs( di ) != r && Math.Abs( dj ) != r ) {
                            continue;
                        }
                        int ni = i + di, nj = j + dj;
                        if( ni < 0 || ni >= size || nj < 0 || nj >= size ) {
                            continue;
                        }
                        if( hasBiome[ nj * size + ni ] ) {
                            index = nj * size + ni;
                            return true;
                        }
                    }
                }
            }
            index = -1;
            return false;
        }
    }
}
