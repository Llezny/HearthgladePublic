using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Makes the generated land one clean island: every piece of land but the largest sinks into the sea, and lakes
    /// that are too small to matter are filled in. It edits the height of the changed cells and picks their biome again
    /// with the normal rules, so the result stays consistent with the biome ranges (a filled puddle becomes beach).
    /// </summary>
    public static class IslandCleanup {

        private const double DeepSeaHeight = -0.9999;

        public static void Apply( GeneratedTerrain terrain, BiomeRule[] rules, bool[] liquid, WorldShapeSettings shape ) {
            int size = terrain.Size;
            if( size == 0 ) {
                return;
            }

            if( shape.KeepMainIslandOnly ) {
                var land = Label( terrain, liquid, wantWater: false, out var sizes );
                int largest = LargestComponent( sizes );
                for( int i = 0; i < land.Length; i++ ) {
                    if( land[ i ] > 0 && land[ i ] != largest ) {
                        Reassign( terrain, rules, i, DeepSeaHeight );
                    }
                }
            }

            if( shape.MinLakeSize > 0 ) {
                double beachHeight = LowestLandHeight( terrain, liquid );
                var water = Label( terrain, liquid, wantWater: true, out var sizes );
                var touchesBorder = new bool[ sizes.Count + 1 ];
                for( int i = 0; i < water.Length; i++ ) {
                    int x = i % size, z = i / size;
                    if( water[ i ] > 0 && ( x == 0 || z == 0 || x == size - 1 || z == size - 1 ) ) {
                        touchesBorder[ water[ i ] ] = true;
                    }
                }
                for( int i = 0; i < water.Length; i++ ) {
                    int label = water[ i ];
                    if( label > 0 && !touchesBorder[ label ] && sizes[ label - 1 ] < shape.MinLakeSize ) {
                        Reassign( terrain, rules, i, beachHeight );
                    }
                }
            }

            TerrainGenerator.AssignBiomes( terrain, rules );
        }

        internal static bool IsWater( GeneratedTerrain terrain, bool[] liquid, int index ) {
            int biome = terrain.Biome[ index ];
            return biome >= 0 && biome < liquid.Length && liquid[ biome ];
        }

        // Labels 4-connected regions of water (or of land) from 1 up; 0 marks cells of the other kind.
        // sizes[label - 1] is the number of cells of a region.
        public static int[] Label( GeneratedTerrain terrain, bool[] liquid, bool wantWater, out List<int> sizes ) {
            int size = terrain.Size;
            var labels = new int[ size * size ];
            sizes = new List<int>();
            var stack = new Stack<int>();

            for( int start = 0; start < labels.Length; start++ ) {
                if( labels[ start ] != 0 || IsWater( terrain, liquid, start ) != wantWater ) {
                    continue;
                }
                int label = sizes.Count + 1;
                int count = 0;
                labels[ start ] = label;
                stack.Push( start );
                while( stack.Count > 0 ) {
                    int cell = stack.Pop();
                    count++;
                    int x = cell % size, z = cell / size;
                    Visit( x - 1, z );
                    Visit( x + 1, z );
                    Visit( x, z - 1 );
                    Visit( x, z + 1 );
                }
                sizes.Add( count );

                void Visit( int nx, int nz ) {
                    if( nx < 0 || nz < 0 || nx >= size || nz >= size ) {
                        return;
                    }
                    int neighbour = nz * size + nx;
                    if( labels[ neighbour ] == 0 && IsWater( terrain, liquid, neighbour ) == wantWater ) {
                        labels[ neighbour ] = label;
                        stack.Push( neighbour );
                    }
                }
            }
            return labels;
        }

        private static int LargestComponent( List<int> sizes ) {
            int best = 0, bestSize = 0;
            for( int i = 0; i < sizes.Count; i++ ) {
                if( sizes[ i ] > bestSize ) {
                    bestSize = sizes[ i ];
                    best = i + 1;
                }
            }
            return best;
        }

        // The lowest ground that is still land: what a filled lake becomes (usually beach).
        private static double LowestLandHeight( GeneratedTerrain terrain, bool[] liquid ) {
            double lowest = 1.0;
            for( int i = 0; i < terrain.Height.Length; i++ ) {
                if( !IsWater( terrain, liquid, i ) && terrain.Height[ i ] < lowest ) {
                    lowest = terrain.Height[ i ];
                }
            }
            return lowest;
        }

        private static void Reassign( GeneratedTerrain terrain, BiomeRule[] rules, int index, double height ) {
            terrain.Height[ index ] = height;
            terrain.Biome[ index ] = TerrainGenerator.FindBiome( rules, height, terrain.Temperature[ index ], terrain.Humidity[ index ] );
        }
    }
}
