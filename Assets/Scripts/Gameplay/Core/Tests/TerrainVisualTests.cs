using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    internal static class TerrainTestHelper {

        public const int ChunkSize = 13;

        public static TerrainCell Ground( int biome = TerrainBiome.Grass, bool elevated = false ) {
            return new TerrainCell { Ground = true, Biome = ( byte ) biome, Elevated = elevated };
        }

        /// <summary>
        /// Builds a grid from ascii rows (row index = y, column = x):
        /// '#' grass, 'e' elevated grass, 's' sand, 'r' stone, 't' taiga, 'W' wall (ground that is a wall),
        /// 'o' non-ground block (e.g. water), anything else = no block.
        /// </summary>
        public static TerrainGrid Grid( int size, params string[] rows ) {
            var grid = new TerrainGrid( size );
            for( int y = 0; y < rows.Length; y++ ) {
                for( int x = 0; x < rows[ y ].Length; x++ ) {
                    switch( rows[ y ][ x ] ) {
                        case '#': grid.Set( x, y, Ground() ); break;
                        case 'e': grid.Set( x, y, Ground( elevated: true ) ); break;
                        case 's': grid.Set( x, y, Ground( TerrainBiome.Sand ) ); break;
                        case 'r': grid.Set( x, y, Ground( TerrainBiome.Stone ) ); break;
                        case 't': grid.Set( x, y, Ground( TerrainBiome.Taiga ) ); break;
                        case 'W': grid.Set( x, y, new TerrainCell { Ground = true, Wall = true } ); break;
                        case 'o': grid.Set( x, y, new TerrainCell { Ground = false } ); break;
                    }
                }
            }
            return grid;
        }

        public static TerrainGrid Filled( int size, TerrainCell cell ) {
            var grid = new TerrainGrid( size );
            for( int y = 0; y < size; y++ ) {
                for( int x = 0; x < size; x++ ) {
                    grid.Set( x, y, cell );
                }
            }
            return grid;
        }

        public static TerrainWindow WindowOf( TerrainGrid grid, int chunkX, int chunkY, int chunkSize = ChunkSize, int margin = 1 ) {
            return grid.CopyWindow(
                ChunkVisualComputer.WindowOrigin( chunkX, chunkSize, margin ),
                ChunkVisualComputer.WindowOrigin( chunkY, chunkSize, margin ),
                ChunkVisualComputer.WindowSize( chunkSize, margin ) );
        }

        public static TerrainVisualParams Params( bool overhang = true, bool grass = false ) {
            return new TerrainVisualParams {
                Overhang = overhang ? 0.08f : 0f,
                EnableGrass = grass,
                GrassNoiseThreshold = 0f,
                GrassByBiome = new[] {
                    new GrassStyle { TuftsPerTile = 3, Tint = new Float3( 0.36f, 0.62f, 0.28f ), TipLighten = new Float3( 0.62f, 0.82f, 0.42f ) }
                }
            };
        }

        public static ChunkVisualData Compute( TerrainGrid grid, TerrainVisualParams p, int chunkX = 0, int chunkY = 0 ) {
            var window = WindowOf( grid, chunkX, chunkY, p.ChunkSize, ChunkVisualComputer.WindowMargin( p ) );
            return ChunkVisualComputer.Compute( window, chunkX, chunkY, p );
        }

        public static void AssertWellFormed( MeshBuffers mesh ) {
            Assert.AreEqual( mesh.Vertices.Count, mesh.Normals.Count );
            Assert.AreEqual( mesh.Vertices.Count, mesh.Uvs.Count );
            Assert.AreEqual( 0, mesh.Triangles.Count % 3 );
            Assert.IsTrue( mesh.Triangles.All( index => index >= 0 && index < mesh.Vertices.Count ) );
            foreach( var normal in mesh.Normals ) {
                var length = Math.Sqrt( normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z );
                Assert.AreEqual( 1.0, length, 1e-4 );
            }
        }
    }

    public class TerrainGridTests {

        [ Test ]
        public void SetAndGet_RoundTripEveryFlag( ) {
            var grid = new TerrainGrid( 4 );
            grid.Set( 1, 2, new TerrainCell { Present = true, Ground = true, Walkable = true, Wall = true, Elevated = true, Biome = 3 } );
            var cell = grid.Get( 1, 2 );
            Assert.IsTrue( cell.Present );
            Assert.IsTrue( cell.Walkable );
            Assert.IsTrue( cell.Ground );
            Assert.IsTrue( cell.Wall );
            Assert.IsTrue( cell.Elevated );
            Assert.AreEqual( 3, cell.Biome );
            Assert.IsFalse( grid.Get( 2, 1 ).Ground );
            Assert.IsFalse( grid.Get( 2, 1 ).Present );
        }

        [ Test ]
        public void PresentAndWalkable_AreIndependentOfGround( ) {
            var grid = new TerrainGrid( 3 );
            grid.Set( 0, 0, new TerrainCell { Present = true, Ground = false, Walkable = false } ); // e.g. water
            grid.Set( 1, 0, new TerrainCell { Present = true, Ground = true, Walkable = true } );
            Assert.IsTrue( grid.Get( 0, 0 ).Present );
            Assert.IsFalse( grid.Get( 0, 0 ).Ground );
            Assert.IsFalse( grid.Get( 0, 0 ).Walkable );
            Assert.IsTrue( grid.Get( 1, 0 ).Walkable );
            Assert.IsFalse( grid.Get( 2, 0 ).Present );
        }

        [ Test ]
        public void OutsideTheGrid_CellsAreEmpty_AndSettingThemThrows( ) {
            var grid = new TerrainGrid( 3 );
            Assert.IsFalse( grid.Get( -1, 0 ).Ground );
            Assert.IsFalse( grid.Get( 0, 3 ).Ground );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => grid.Set( 3, 0, default ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new TerrainGrid( -1 ) );
        }

        [ Test ]
        public void Solid_MeansGroundAndNotWall( ) {
            Assert.IsTrue( new TerrainCell { Ground = true }.Solid );
            Assert.IsFalse( new TerrainCell { Ground = true, Wall = true }.Solid );
            Assert.IsFalse( new TerrainCell { Ground = false }.Solid );
        }

        [ Test ]
        public void TopY_IsBasePlusRiseOnlyForElevatedSolidCells( ) {
            Assert.AreEqual( 0.4f, new TerrainCell { Ground = true }.TopY( 0.4f, 0.2f ).Value, 1e-6 );
            Assert.AreEqual( 0.6f, new TerrainCell { Ground = true, Elevated = true }.TopY( 0.4f, 0.2f ).Value, 1e-6 );
            Assert.IsNull( new TerrainCell { Ground = false }.TopY( 0.4f, 0.2f ) );
            Assert.IsNull( new TerrainCell { Ground = true, Wall = true }.TopY( 0.4f, 0.2f ) );
        }

        [ Test ]
        public void Flattened_SuppressesTheElevatedRise_ButNotWithoutElevated( ) {
            Assert.AreEqual( 0.4f, new TerrainCell { Ground = true, Elevated = true, Flattened = true }.TopY( 0.4f, 0.2f ).Value, 1e-6 );
            Assert.AreEqual( 0.4f, new TerrainCell { Ground = true, Elevated = false, Flattened = true }.TopY( 0.4f, 0.2f ).Value, 1e-6 );
        }

        [ Test ]
        public void SetFlattened_TogglesOnlyThatBit_LeavingEveryOtherFlagAndBiomeUntouched( ) {
            var grid = new TerrainGrid( 3 );
            grid.Set( 1, 1, new TerrainCell { Present = true, Ground = true, Walkable = true, Elevated = true, Biome = 5 } );

            grid.SetFlattened( 1, 1, true );
            var flattened = grid.Get( 1, 1 );
            Assert.IsTrue( flattened.Flattened );
            Assert.IsTrue( flattened.Elevated );
            Assert.IsTrue( flattened.Ground );
            Assert.IsTrue( flattened.Walkable );
            Assert.AreEqual( 5, flattened.Biome );
            Assert.AreEqual( 0.4f, flattened.TopY( 0.4f, 0.2f ).Value, 1e-6 );

            grid.SetFlattened( 1, 1, false );
            var restored = grid.Get( 1, 1 );
            Assert.IsFalse( restored.Flattened );
            Assert.AreEqual( 0.6f, restored.TopY( 0.4f, 0.2f ).Value, 1e-6 );
        }

        [ Test ]
        public void SetFlattened_OutsideTheGrid_Throws( ) {
            var grid = new TerrainGrid( 3 );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => grid.SetFlattened( 3, 0, true ) );
        }

        [ Test ]
        public void Window_IsAddressedWithGlobalCoordinates_AndEmptyOutside( ) {
            var grid = TerrainTestHelper.Grid( 20, "..........", "..........", "..........", "...#......" );
            var window = grid.CopyWindow( 2, 1, 4 );
            Assert.IsTrue( window.Get( 3, 3 ).Ground );
            Assert.IsFalse( window.Get( 2, 1 ).Ground );
            Assert.IsFalse( window.Get( 1, 1 ).Ground, "outside the window" );
            Assert.IsFalse( window.Get( 6, 1 ).Ground, "outside the window" );
        }

        [ Test ]
        public void Window_IsASnapshot_LaterGridChangesDoNotShowThrough( ) {
            var grid = TerrainTestHelper.Grid( 5, "#" );
            var window = grid.CopyWindow( 0, 0, 5 );
            grid.Set( 0, 0, default );
            grid.Set( 1, 0, TerrainTestHelper.Ground() );
            Assert.IsTrue( window.Get( 0, 0 ).Ground );
            Assert.IsFalse( window.Get( 1, 0 ).Ground );
        }
    }

    public class SplatBuilderTests {

        private static SplatResult Splat( TerrainGrid grid, int chunkX = 0, int chunkY = 0, int blendRadius = 0 ) {
            var window = TerrainTestHelper.WindowOf( grid, chunkX, chunkY, TerrainTestHelper.ChunkSize, 1 + blendRadius );
            return SplatBuilder.Build( window, chunkX * TerrainTestHelper.ChunkSize, chunkY * TerrainTestHelper.ChunkSize, TerrainTestHelper.ChunkSize, blendRadius );
        }

        // Texel (cellX, cellY) of a chunk sits at (cellX + 1, cellY + 1) because of the one texel border.
        private static byte[] Texel( byte[] pixels, int size, int x, int y ) {
            int o = ( y * size + x ) * 4;
            return new[] { pixels[ o ], pixels[ o + 1 ], pixels[ o + 2 ], pixels[ o + 3 ] };
        }

        [ Test ]
        public void EmptyChunk_HasNoGround( ) {
            Assert.IsFalse( Splat( new TerrainGrid( 26 ) ).AnyGround );
        }

        [ Test ]
        public void TextureIsChunkSizePlusBorder( ) {
            var splat = Splat( TerrainTestHelper.Grid( 26, "#" ) );
            Assert.AreEqual( 15, splat.TextureSize );
            Assert.AreEqual( 15 * 15 * 4, splat.SplatA.Length );
            Assert.AreEqual( 15 * 15 * 4, splat.SplatB.Length );
            Assert.AreEqual( 13 * 13, splat.CellExists.Length );
        }

        [ TestCase( TerrainBiome.Grass, 0, 255, 0, 0, 0 ) ]
        [ TestCase( TerrainBiome.Dirt, 0, 0, 255, 0, 0 ) ]
        [ TestCase( TerrainBiome.Sand, 0, 0, 0, 255, 0 ) ]
        [ TestCase( TerrainBiome.Stone, 0, 0, 0, 0, 255 ) ]
        public void BiomeChannels_MatchTheShader( int biome, int unused, int r, int g, int b, int a ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 5, 5, TerrainTestHelper.Ground( biome ) );
            var splat = Splat( grid );

            // Only the one ground cell has its own biome; every other texel copies it (nearest fill).
            var texel = Texel( splat.SplatA, splat.TextureSize, 6, 6 );
            // Channels are R = grass, G = dirt, B = sand, A = stone.
            var expected = new byte[ 4 ];
            expected[ biome ] = 255;
            CollectionAssert.AreEqual( expected, texel );
            CollectionAssert.AreEqual( new byte[] { 0, 0, 0, 0 }, Texel( splat.SplatB, splat.TextureSize, 6, 6 ) );
        }

        [ TestCase( TerrainBiome.Taiga, 255, 0, 0, 0 ) ]
        [ TestCase( TerrainBiome.Forest, 0, 255, 0, 0 ) ]
        [ TestCase( TerrainBiome.Swamp, 0, 0, 255, 0 ) ]
        [ TestCase( TerrainBiome.Tundra, 0, 0, 0, 255 ) ]
        public void FurtherBiomes_UseTheChannelsOfTheSecondSplatMap( int biome, int r, int g, int b, int a ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 5, 5, TerrainTestHelper.Ground( biome ) );
            var splat = Splat( grid );
            CollectionAssert.AreEqual( new byte[] { 0, 0, 0, 0 }, Texel( splat.SplatA, splat.TextureSize, 6, 6 ) );
            CollectionAssert.AreEqual( new byte[] { ( byte ) r, ( byte ) g, ( byte ) b, ( byte ) a }, Texel( splat.SplatB, splat.TextureSize, 6, 6 ) );
        }

        [ Test ]
        public void Taiga_UsesTheSecondSplatMap( ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 5, 5, TerrainTestHelper.Ground( TerrainBiome.Taiga ) );
            var splat = Splat( grid );
            CollectionAssert.AreEqual( new byte[] { 0, 0, 0, 0 }, Texel( splat.SplatA, splat.TextureSize, 6, 6 ) );
            CollectionAssert.AreEqual( new byte[] { 255, 0, 0, 0 }, Texel( splat.SplatB, splat.TextureSize, 6, 6 ) );
        }

        [ Test ]
        public void CellExists_MarksOnlyGroundInsideTheChunk( ) {
            var grid = TerrainTestHelper.Grid( 26, "#o#" );
            var splat = Splat( grid );
            Assert.IsTrue( splat.CellExists[ 0 ] );
            Assert.IsFalse( splat.CellExists[ 1 ], "a non-ground block is not a terrain cell" );
            Assert.IsTrue( splat.CellExists[ 2 ] );
            Assert.AreEqual( 2, splat.CellExists.Count( exists => exists ) );
        }

        [ Test ]
        public void GroundInTheNeighbourChunk_FeedsTheBorderTexelsButIsNotThisChunksCell( ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 13, 0, TerrainTestHelper.Ground( TerrainBiome.Sand ) ); // first cell of chunk (1, 0)
            var splat = Splat( grid, 0, 0 );

            Assert.IsFalse( splat.AnyGround );
            Assert.IsTrue( Texel( splat.SplatA, splat.TextureSize, 14, 1 ).SequenceEqual( new byte[] { 0, 0, 255, 0 } ) );
        }

        [ Test ]
        public void MissingCells_TakeTheirNearestNeighboursBiome( ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 0, 0, TerrainTestHelper.Ground( TerrainBiome.Stone ) );
            grid.Set( 12, 12, TerrainTestHelper.Ground( TerrainBiome.Sand ) );
            var splat = Splat( grid );

            // texel of cell (2, 0) is closer to the stone cell, texel of cell (10, 12) to the sand cell
            CollectionAssert.AreEqual( new byte[] { 0, 0, 0, 255 }, Texel( splat.SplatA, splat.TextureSize, 3, 1 ) );
            CollectionAssert.AreEqual( new byte[] { 0, 0, 255, 0 }, Texel( splat.SplatA, splat.TextureSize, 11, 13 ) );
        }

        [ Test ]
        public void NoGroundAnywhere_FallsBackToGrass( ) {
            var splat = Splat( new TerrainGrid( 26 ) );
            Assert.AreEqual( 255, Texel( splat.SplatA, splat.TextureSize, 4, 4 )[ 0 ] );
        }

        [ Test ]
        public void WallCells_StillGetASplatTexel( ) {
            var splat = Splat( TerrainTestHelper.Grid( 26, "W" ) );
            Assert.IsTrue( splat.CellExists[ 0 ] );
        }

        [ Test ]
        public void Blur_DoesNotMixWeightsAcrossAnElevationStep( ) {
            var grid = new TerrainGrid( 26 );
            grid.Set( 5, 5, TerrainTestHelper.Ground( TerrainBiome.Grass, elevated: true ) );
            for( int dy = -1; dy <= 1; dy++ ) {
                for( int dx = -1; dx <= 1; dx++ ) {
                    if( dx == 0 && dy == 0 ) continue;
                    grid.Set( 5 + dx, 5 + dy, TerrainTestHelper.Ground( TerrainBiome.Sand ) ); // not elevated
                }
            }
            var splat = Splat( grid, blendRadius: 1 );

            // The elevated centre cell must stay pure grass: none of its same-radius neighbours share
            // its elevation, so the blur must not mix in their (sand) weight even though they're within
            // radius - otherwise the mesh's per-corner "don't blend across a height step" fix is moot,
            // because the texel it's clamped to sampling was already contaminated by the CPU blur.
            CollectionAssert.AreEqual( new byte[] { 255, 0, 0, 0 }, Texel( splat.SplatA, splat.TextureSize, 6, 6 ) );
        }

        [ Test ]
        public void Blur_MixesWeights_WhenAnElevatedCellWasFlattenedByAFloorTile( ) {
            // Same setup as the test above, except the centre cell got a floor tile placed on it
            // (docs/BUILDING_SYSTEM_PLAN.md section 9): it renders at the same height as its sand
            // neighbours now, so unlike the still-elevated case, blurring across them is correct - the
            // raw Elevated bit alone must not be read as "a height step exists here" any more.
            var grid = new TerrainGrid( 26 );
            grid.Set( 5, 5, TerrainTestHelper.Ground( TerrainBiome.Grass, elevated: true ) );
            grid.SetFlattened( 5, 5, true );
            for( int dy = -1; dy <= 1; dy++ ) {
                for( int dx = -1; dx <= 1; dx++ ) {
                    if( dx == 0 && dy == 0 ) continue;
                    grid.Set( 5 + dx, 5 + dy, TerrainTestHelper.Ground( TerrainBiome.Sand ) ); // not elevated
                }
            }
            var splat = Splat( grid, blendRadius: 1 );

            var texel = Texel( splat.SplatA, splat.TextureSize, 6, 6 );
            Assert.AreNotEqual( 255, texel[ 0 ], "grass weight should be diluted by the now-matching-height sand neighbours" );
            Assert.Greater( texel[ 2 ], 0, "sand weight should be mixed in" );
        }
    }

    public class TerrainTopMeshTests {

        [ Test ]
        public void SingleCell_WithoutOverhang_IsOneQuad( ) {
            var data = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "#" ), TerrainTestHelper.Params( overhang: false ) );
            Assert.AreEqual( 4, data.Top.VertexCount );
            Assert.AreEqual( 6, data.Top.Triangles.Count );
            TerrainTestHelper.AssertWellFormed( data.Top );
        }

        [ Test ]
        public void SingleCell_WithOverhang_GetsALipOnEverySideAndAFillOnEveryCorner( ) {
            var data = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "#" ), TerrainTestHelper.Params( ) );
            // quad 4 + 4 lips * 8 + 4 corners * 17 vertices (rounded corner fan: 1 inner + 4 arc + 3 arc segments * 4 wall);
            // 6 + 4 * 12 + 4 * 27 indices (corner fan: 3 arc triangles + 3 segments * 2 wall triangles = 9 triangles)
            Assert.AreEqual( 4 + 4 * 8 + 4 * 17, data.Top.VertexCount );
            Assert.AreEqual( 6 + 4 * 12 + 4 * 27, data.Top.Triangles.Count );
            TerrainTestHelper.AssertWellFormed( data.Top );
        }

        [ Test ]
        public void TwoNeighbouringCells_HaveNoLipBetweenThem( ) {
            var data = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "##" ), TerrainTestHelper.Params( ) );
            // 2 quads, 6 outer lips, 4 outer corners
            Assert.AreEqual( 2 * 4 + 6 * 8 + 4 * 17, data.Top.VertexCount );
            Assert.AreEqual( 2 * 6 + 6 * 12 + 4 * 27, data.Top.Triangles.Count );
        }

        [ Test ]
        public void FullChunk_HasLipsOnlyAlongItsBorder( ) {
            var grid = TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( ) );
            // cells of chunk (0, 0) only: cut everything else away
            for( int y = 0; y < 26; y++ ) {
                for( int x = 0; x < 26; x++ ) {
                    if( x >= 13 || y >= 13 ) {
                        grid.Set( x, y, default );
                    }
                }
            }
            var data = TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ) );
            Assert.AreEqual( 169 * 4 + 52 * 8 + 4 * 17, data.Top.VertexCount );
            Assert.AreEqual( 169 * 6 + 52 * 12 + 4 * 27, data.Top.Triangles.Count );
            TerrainTestHelper.AssertWellFormed( data.Top );
        }

        [ Test ]
        public void SurfaceHeight_FollowsElevation( ) {
            var p = TerrainTestHelper.Params( overhang: false );
            var flat = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "#" ), p ).Top;
            var raised = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "e" ), p ).Top;
            Assert.IsTrue( flat.Vertices.All( v => Math.Abs( v.Y - p.BaseTopY ) < 1e-6f ) );
            Assert.IsTrue( raised.Vertices.All( v => Math.Abs( v.Y - ( p.BaseTopY + p.ElevatedRise ) ) < 1e-6f ) );
        }

        [ Test ]
        public void Quad_IsCentredOnTheCellAndOneTileWide( ) {
            var p = TerrainTestHelper.Params( overhang: false );
            var grid = new TerrainGrid( 26 );
            grid.Set( 4, 7, TerrainTestHelper.Ground( ) );
            var top = TerrainTestHelper.Compute( grid, p ).Top;

            Assert.AreEqual( 4 * p.TileSize - p.TileSize / 2, top.Vertices.Min( v => v.X ), 1e-5 );
            Assert.AreEqual( 4 * p.TileSize + p.TileSize / 2, top.Vertices.Max( v => v.X ), 1e-5 );
            Assert.AreEqual( 7 * p.TileSize - p.TileSize / 2, top.Vertices.Min( v => v.Z ), 1e-5 );
            Assert.AreEqual( 7 * p.TileSize + p.TileSize / 2, top.Vertices.Max( v => v.Z ), 1e-5 );
        }

        [ Test ]
        public void IsolatedCell_IsASeamCell_AllCornersSampleTheSameTexel( ) {
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "#" ), TerrainTestHelper.Params( overhang: false ) ).Top;
            Assert.AreEqual( 1, top.Uvs.Distinct( ).Count( ) );
            Assert.AreEqual( 1.5f / 15, top.Uvs[ 0 ].X, 1e-6 );
        }

        [ Test ]
        public void InteriorCell_BlendsAcrossFourTexels( ) {
            var rows = new[] { "###", "###", "###" };
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, rows ), TerrainTestHelper.Params( overhang: false ) ).Top;
            // the centre cell (1, 1) is the 5th quad: vertices 16..19
            var centre = top.Uvs.Skip( 4 * 4 ).Take( 4 ).ToArray( );
            Assert.AreEqual( 4, centre.Distinct( ).Count( ) );
        }

        [ Test ]
        public void ElevatedCellSurroundedByDifferentHeightNeighbours_DoesNotBlendAcrossTheStep( ) {
            // The centre cell is an elevated biome-transition lip and every neighbour (incl. diagonals)
            // is at base height: blending across that step would smear color across a visible vertical
            // face, so all four corners must clamp to the cell's own texel.
            var rows = new[] { "###", "#e#", "###" };
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, rows ), TerrainTestHelper.Params( overhang: false ) ).Top;
            // the centre cell (1, 1) is the 5th quad: vertices 16..19
            var centre = top.Uvs.Skip( 4 * 4 ).Take( 4 ).ToArray( );
            Assert.AreEqual( 1, centre.Distinct( ).Count( ) );
        }

        [ Test ]
        public void DiagonalOnlyHeightDifference_DoesNotBlockCornerBlend( ) {
            // The whole 3x3 block is elevated except its bottom-left cell, which only touches the centre
            // cell diagonally (not on any edge). A corner must only care about the two edges it actually
            // sits on, or a distant, unrelated diagonal cell would kill blending along a perfectly
            // matching edge - reads as "blends near one end, hard line by the middle" along that edge.
            var rows = new[] { "#ee", "eee", "eee" };
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, rows ), TerrainTestHelper.Params( overhang: false ) ).Top;
            // the centre cell (1, 1) is the 5th quad: vertices 16..19
            var centre = top.Uvs.Skip( 4 * 4 ).Take( 4 ).ToArray( );
            Assert.AreEqual( 4, centre.Distinct( ).Count( ) );
        }

        [ Test ]
        public void EdgeHeightDifference_ClampsOnlyTheTwoCornersOnThatEdge( ) {
            // Only the south neighbour is at a different height; west, north and every diagonal
            // (including both south-corner diagonals) match the centre cell. Only the SW and SE corners
            // touch the south edge and must clamp; NW and NE sit on unaffected edges and must still blend.
            var rows = new[] { "e#e", "eee", "eee" };
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, rows ), TerrainTestHelper.Params( overhang: false ) ).Top;
            var p = TerrainTestHelper.Params( overhang: false );
            int texSize = p.ChunkSize + 2;
            var ownUV = new Float2( 2.5f / texSize, 2.5f / texSize );
            // the centre cell (1, 1) is the 5th quad: vertices 16..19, in sw, se, nw, ne order
            var centre = top.Uvs.Skip( 4 * 4 ).Take( 4 ).ToArray( );
            Assert.AreEqual( ownUV.X, centre[ 0 ].X, 1e-6, "sw clamps" );
            Assert.AreEqual( ownUV.X, centre[ 1 ].X, 1e-6, "se clamps" );
            Assert.AreNotEqual( ownUV.X, centre[ 2 ].X, "nw still blends" );
            Assert.AreNotEqual( ownUV.X, centre[ 3 ].X, "ne still blends" );
        }

        [ Test ]
        public void LoneDiagonalBiome_DoesNotBleedIntoTheCorner( ) {
            // The centre cell and both its NE-corner axis neighbours (east, north) are sand; only the
            // NE diagonal cell is grass, unrelated to either axis neighbour. Reaching for it would smear
            // a disproportionate diamond of grass into an otherwise uniform sand patch - a "step" of any
            // jagged/staircase biome boundary looks exactly like this. Only the NE corner should clamp;
            // the other three corners (whose diagonals are all sand, matching everything around them)
            // must still blend normally.
            var rows = new[] { "sss", "sss", "ss#" };
            var p = TerrainTestHelper.Params( overhang: false );
            var top = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, rows ), p ).Top;
            int texSize = p.ChunkSize + 2;
            var ownUV = new Float2( 2.5f / texSize, 2.5f / texSize );
            // the centre cell (1, 1) is the 5th quad: vertices 16..19, in sw, se, nw, ne order
            var centre = top.Uvs.Skip( 4 * 4 ).Take( 4 ).ToArray( );
            Assert.AreNotEqual( ownUV.X, centre[ 0 ].X, "sw still blends" );
            Assert.AreNotEqual( ownUV.X, centre[ 1 ].X, "se still blends" );
            Assert.AreNotEqual( ownUV.X, centre[ 2 ].X, "nw still blends" );
            Assert.AreEqual( ownUV.X, centre[ 3 ].X, 1e-6, "ne clamps: its diagonal is an unrelated lone biome" );
            Assert.AreEqual( ownUV.Y, centre[ 3 ].Y, 1e-6, "ne clamps: its diagonal is an unrelated lone biome" );
        }

        [ Test ]
        public void LipOverANonSolidNeighbour_IsFullSize_OverALowerNeighbour_ItIsReduced( ) {
            var p = TerrainTestHelper.Params( );
            var over = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "e" ), p ).Top; // neighbours missing
            var step = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "e#" ), p ).Top; // east neighbour is lower

            float MaxX( MeshBuffers mesh ) => mesh.Vertices.Where( v => v.Y > 0 ).Max( v => v.X );
            float cellEdge = p.TileSize / 2;
            // full lip: cell edge + overhang; the stepped east side only gets overhang * innerScale
            Assert.AreEqual( cellEdge + p.Overhang, MaxX( over ), 1e-5 );
            // (the corner fills at the two east corners keep the full size, so look for the strip itself)
            Assert.IsTrue( step.Vertices.Any( v => Math.Abs( v.X - ( cellEdge + p.Overhang * p.InnerOverhangScale ) ) < 1e-5f ) );
        }

        [ Test ]
        public void LipIsSuppressed_WhenTheLowerNeighbourIsAFlattenedFloorTile( ) {
            // A floor tile placed on the (lower) east neighbour must not get a grass shelf hanging out over
            // it from the elevated cell next door (docs/BUILDING_SYSTEM_PLAN.md section 9) - a real height
            // step still gets its usual reduced lip (see the test above), only a flattened one doesn't.
            var p = TerrainTestHelper.Params( );
            var grid = TerrainTestHelper.Grid( 26, "e#" );
            float cellEdge = p.TileSize / 2;
            float steppedLipX = cellEdge + p.Overhang * p.InnerOverhangScale;

            var withoutFloor = TerrainTestHelper.Compute( grid, p ).Top;
            Assert.IsTrue( withoutFloor.Vertices.Any( v => Math.Abs( v.X - steppedLipX ) < 1e-5f ), "sanity: the ordinary step has a reduced lip" );

            grid.SetFlattened( 1, 0, true );
            var withFloor = TerrainTestHelper.Compute( grid, p ).Top;
            Assert.IsFalse( withFloor.Vertices.Any( v => Math.Abs( v.X - steppedLipX ) < 1e-5f ), "no lip should reach out over the floor tile" );
        }

        [ Test ]
        public void WallCell_KeepsItsSurface_ButOffersNoTopHeightToNeighbours( ) {
            var p = TerrainTestHelper.Params( overhang: false );
            var data = TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "W" ), p );
            Assert.AreEqual( 4, data.Top.VertexCount );
            Assert.IsTrue( data.Top.Vertices.All( v => Math.Abs( v.Y - p.BaseTopY ) < 1e-6f ) );
        }

        [ Test ]
        public void SameInput_GivesIdenticalMesh( ) {
            var grid = TerrainTestHelper.Grid( 26, "##e#", "#ee#", "s#o#" );
            var a = TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ) ).Top;
            var b = TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ) ).Top;
            CollectionAssert.AreEqual( a.Vertices, b.Vertices );
            CollectionAssert.AreEqual( a.Triangles, b.Triangles );
        }

        [ Test ]
        public void ChunkBorder_UsesTheNeighbourChunksCells( ) {
            // Two chunks side by side: the east edge of chunk 0 must not get a lip, because chunk 1 continues it.
            var grid = TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( ) );
            var lonely = TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( ) );
            for( int y = 0; y < 26; y++ ) {
                for( int x = 13; x < 26; x++ ) {
                    lonely.Set( x, y, default );
                }
            }
            var joined = TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ), 0, 0 ).Top;
            var alone = TerrainTestHelper.Compute( lonely, TerrainTestHelper.Params( ), 0, 0 ).Top;
            Assert.Less( joined.VertexCount, alone.VertexCount );
        }
    }

    public class TerrainCliffTests {

        private static MeshBuffers Cliffs( TerrainGrid grid ) {
            return TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ) ).Cliffs;
        }

        [ Test ]
        public void SingleCell_HasFourDeepWalls( ) {
            var cliffs = Cliffs( TerrainTestHelper.Grid( 26, "#" ) );
            var p = TerrainTestHelper.Params( );
            // 4 walls, each a jagged strip of 3 segments (12 vertices, 18 indices) instead of one flat quad
            Assert.AreEqual( 4 * 12, cliffs.VertexCount );
            Assert.AreEqual( 4 * 18, cliffs.Triangles.Count );
            Assert.AreEqual( p.BaseTopY - p.CliffDepth, cliffs.Vertices.Min( v => v.Y ), 1e-5 );
            Assert.AreEqual( p.BaseTopY, cliffs.Vertices.Max( v => v.Y ), 1e-5 );
            TerrainTestHelper.AssertWellFormed( cliffs );
        }

        [ Test ]
        public void NeighboursOfEqualHeight_HaveNoWallBetweenThem( ) {
            var cliffs = Cliffs( TerrainTestHelper.Grid( 26, "##" ) );
            Assert.AreEqual( 6 * 12, cliffs.VertexCount ); // 3 outer walls each, 12 vertices per jagged wall
        }

        [ Test ]
        public void ElevatedCellNextToLowerOne_HasAShortWallDownToTheLowerTop( ) {
            var p = TerrainTestHelper.Params( );
            var cliffs = Cliffs( TerrainTestHelper.Grid( 26, "e#" ) );
            // e: 3 deep jagged walls (12 vertices each) + 1 short flat step down to the lower neighbour
            // (4 vertices, not jagged - see EmitDirectionalCliff); #: 3 deep jagged walls
            Assert.AreEqual( 6 * 12 + 1 * 4, cliffs.VertexCount );
            var shortWall = cliffs.Vertices.Where( v => v.Y > p.BaseTopY + 1e-4f || Math.Abs( v.Y - p.BaseTopY ) < 1e-5f ).ToArray( );
            Assert.IsTrue( shortWall.Any( v => Math.Abs( v.Y - ( p.BaseTopY + p.ElevatedRise ) ) < 1e-5f ) );
        }

        [ Test ]
        public void NonGroundNeighbour_CountsAsAHole_SoTheWallGoesDeep( ) {
            var p = TerrainTestHelper.Params( );
            var cliffs = Cliffs( TerrainTestHelper.Grid( 26, "#o" ) );
            Assert.AreEqual( 4 * 12, cliffs.VertexCount ); // 4 deep jagged walls, 12 vertices each
            Assert.AreEqual( p.BaseTopY - p.CliffDepth, cliffs.Vertices.Min( v => v.Y ), 1e-5 );
        }

        [ Test ]
        public void Wall_FacesOutwards( ) {
            var cliffs = Cliffs( TerrainTestHelper.Grid( 26, "#" ) );
            var normals = cliffs.Normals.Distinct( ).ToArray( );
            Assert.AreEqual( 4, normals.Length );
            Assert.IsTrue( normals.All( n => Math.Abs( n.Y ) < 1e-6f ) );
        }

        [ Test ]
        public void CliffsDisabled_ProduceNoCliffMesh( ) {
            var p = TerrainTestHelper.Params( );
            p.EnableCliffs = false;
            Assert.IsNull( TerrainTestHelper.Compute( TerrainTestHelper.Grid( 26, "#" ), p ).Cliffs );
        }
    }

    public class GrassBuilderTests {

        private static TerrainGrid Meadow( int radius ) {
            var grid = new TerrainGrid( 26 );
            for( int y = 0; y < radius; y++ ) {
                for( int x = 0; x < radius; x++ ) {
                    grid.Set( x, y, TerrainTestHelper.Ground( ) );
                }
            }
            return grid;
        }

        private static GrassBuilder.GrassMesh Grass( TerrainGrid grid, TerrainVisualParams p, int chunkX = 0, int chunkY = 0 ) {
            return GrassBuilder.Build( TerrainTestHelper.WindowOf( grid, chunkX, chunkY ), chunkX, chunkY, p );
        }

        [ Test ]
        public void Disabled_ProducesNothing( ) {
            Assert.IsNull( Grass( Meadow( 6 ), TerrainTestHelper.Params( grass: false ) ) );
        }

        [ Test ]
        public void NoStyledBiome_ProducesNothing( ) {
            var p = TerrainTestHelper.Params( grass: true );
            p.GrassByBiome = new GrassStyle[ 0 ];
            Assert.IsNull( Grass( Meadow( 6 ), p ) );
        }

        [ Test ]
        public void IsolatedCell_GetsNoGrass_BecauseAllItsNeighboursAreDifferent( ) {
            Assert.IsNull( Grass( TerrainTestHelper.Grid( 26, "#" ), TerrainTestHelper.Params( grass: true ) ) );
        }

        [ Test ]
        public void Meadow_GrowsGrassOnlyInsideItsBorderCells( ) {
            var p = TerrainTestHelper.Params( grass: true );
            var grass = Grass( Meadow( 6 ), p );
            Assert.NotNull( grass );

            // Only cells 1..4 have four solid neighbours of equal height; tufts stay within their tile.
            float margin = p.TileSize * 0.5f;
            float minAllowed = 1 * p.TileSize - margin - 1e-4f;
            float maxAllowed = 4 * p.TileSize + margin + p.GrassWidth;
            Assert.IsTrue( grass.Mesh.Vertices.All( v => v.X >= minAllowed - p.GrassWidth && v.X <= maxAllowed && v.Z >= minAllowed - p.GrassWidth && v.Z <= maxAllowed ) );
        }

        [ Test ]
        public void FlattenedCell_GetsNoGrass_ButItsNeighboursStillDo( ) {
            var p = TerrainTestHelper.Params( grass: true );
            var grid = Meadow( 6 );
            grid.SetFlattened( 2, 2, true ); // a floor tile placed here (docs/BUILDING_SYSTEM_PLAN.md section 9)
            var grass = Grass( grid, p );
            Assert.NotNull( grass, "the rest of the meadow still grows grass" );

            float cellMin = 2 * p.TileSize - p.GrassWidth;
            float cellMax = 3 * p.TileSize + p.GrassWidth;
            Assert.IsFalse( grass.Mesh.Vertices.Any( v => v.X >= cellMin && v.X <= cellMax && v.Z >= cellMin && v.Z <= cellMax ),
                "no tuft should sit on the flattened cell" );
        }

        [ Test ]
        public void GrassClearZone_RemovesTuftsItTouches_PerTuftAndNotPerCell( ) {
            var p = TerrainTestHelper.Params( grass: true );
            var plain = Grass( Meadow( 6 ), p );
            var grid = Meadow( 6 );
            grid.AddGrassClearZone( GrassClearZone.ForCells( 2, 2, 1, 1, 0f ) );
            var grass = Grass( grid, p );
            Assert.NotNull( grass );

            float min = 1.5f * p.TileSize, max = 2.5f * p.TileSize;
            bool IsBaseInZone( Float3 v ) => Math.Abs( v.Y - p.BaseTopY ) < 1e-5f && v.X >= min && v.X <= max && v.Z >= min && v.Z <= max;
            Assert.IsTrue( plain.Mesh.Vertices.Any( IsBaseInZone ), "the control meadow does grow grass there" );
            Assert.IsFalse( grass.Mesh.Vertices.Any( IsBaseInZone ), "no tuft may touch the zone" );
            Assert.Less( grass.Mesh.Vertices.Count(), plain.Mesh.Vertices.Count() );
        }

        [ Test ]
        public void GrassClearZone_RadiusWidensTheClearedArea( ) {
            var p = TerrainTestHelper.Params( grass: true );
            int Count( float radius ) {
                var grid = Meadow( 6 );
                grid.AddGrassClearZone( GrassClearZone.ForCells( 2, 2, 1, 1, radius ) );
                return Grass( grid, p )?.Mesh.Vertices.Count( ) ?? 0;
            }
            Assert.Less( Count( 1f ), Count( 0f ) );
            Assert.Less( Count( 2f ), Count( 1f ) );
        }

        [ Test ]
        public void GrassClearZone_TouchesIsAColliderStyleDistanceToTheFootprint( ) {
            var zone = GrassClearZone.ForCells( 5, 5, 1, 1, 1f );
            Assert.IsTrue( zone.Touches( 5f, 5f, 0f ), "inside the footprint" );
            Assert.IsTrue( zone.Touches( 6.4f, 5f, 0f ), "within the radius of the footprint edge (5.5 + 1)" );
            Assert.IsFalse( zone.Touches( 6.6f, 5f, 0f ), "beyond the radius" );
            Assert.IsTrue( zone.Touches( 6.6f, 5f, 0.2f ), "but a card reaching back into it still touches" );
            Assert.IsFalse( zone.Touches( 6.4f, 6.4f, 0f ), "the corner is rounded: (0.9, 0.9) is 1.27 away" );
        }

        [ Test ]
        public void CellsAdjacentToAFlattenedCell_GetNoGrass_EvenAtTheSameHeight( ) {
            // A flat (non-elevated) meadow: flattening (2,2) changes nothing about its TopY, so the existing
            // "next to a height step" check alone would miss this - the floor tile still needs its own
            // grass-free margin (docs/BUILDING_SYSTEM_PLAN.md section 9).
            var p = TerrainTestHelper.Params( grass: true );
            var plain = Grass( Meadow( 6 ), p );
            var grid = Meadow( 6 );
            grid.SetFlattened( 2, 2, true );
            var grass = Grass( grid, p );
            Assert.NotNull( grass, "cells further away still grow grass" );

            List<Float3> VerticesInCell( GrassBuilder.GrassMesh g, int cx, int cz ) {
                // A tuft's base sits at (gridX, gridY) * TileSize - the cell's centre, not its lower corner -
                // so the cell spans [(cx - 0.5) * TileSize, (cx + 0.5) * TileSize], same as the top-mesh tests above.
                // No extra GrassWidth padding here (unlike the single-cell check above): with GrassTileMargin
                // jitter maxing out just short of the true half-tile edge, padding the window past that edge
                // pulls in a diagonal, un-suppressed neighbour's legitimate near-border tufts as false positives.
                float min = ( cx - 0.5f ) * p.TileSize;
                float max = ( cx + 0.5f ) * p.TileSize;
                float minZ = ( cz - 0.5f ) * p.TileSize;
                float maxZ = ( cz + 0.5f ) * p.TileSize;
                return g.Mesh.Vertices.Where( v => v.X >= min && v.X <= max && v.Z >= minZ && v.Z <= maxZ ).ToList();
            }

            Assert.IsEmpty( VerticesInCell( grass, 1, 2 ), "cardinal neighbour (-x) must stay clear" );
            Assert.IsEmpty( VerticesInCell( grass, 3, 2 ), "cardinal neighbour (+x) must stay clear" );
            Assert.IsEmpty( VerticesInCell( grass, 2, 1 ), "cardinal neighbour (-z) must stay clear" );
            Assert.IsEmpty( VerticesInCell( grass, 2, 3 ), "cardinal neighbour (+z) must stay clear" );
            // A diagonal neighbour is unaffected: same tufts as an ordinary meadow with nothing flattened.
            CollectionAssert.AreEqual( VerticesInCell( plain, 1, 1 ), VerticesInCell( grass, 1, 1 ) );
        }

        [ Test ]
        public void Tufts_AreOneQuadEach_WithMatchingColorsAndNormals( ) {
            var grass = Grass( Meadow( 6 ), TerrainTestHelper.Params( grass: true ) );
            // Each tuft is one card: 4 vertices, 2 triangles (6 indices).
            Assert.AreEqual( 0, grass.Mesh.VertexCount % 4 );
            Assert.AreEqual( grass.Mesh.VertexCount, grass.Mesh.Colors.Count );
            Assert.AreEqual( grass.Mesh.VertexCount, grass.Mesh.Normals.Count );
            Assert.AreEqual( grass.Mesh.VertexCount, grass.Mesh.Uvs.Count );
            Assert.AreEqual( grass.Mesh.VertexCount / 4 * 6, grass.Mesh.Triangles.Count );
            // every quad: two darker base vertices (alpha 0) and two tip vertices (alpha 255)
            for( int i = 0; i < grass.Mesh.Colors.Count; i += 4 ) {
                Assert.AreEqual( 0, grass.Mesh.Colors[ i ].A );
                Assert.AreEqual( 0, grass.Mesh.Colors[ i + 1 ].A );
                Assert.AreEqual( 255, grass.Mesh.Colors[ i + 2 ].A );
                Assert.AreEqual( 255, grass.Mesh.Colors[ i + 3 ].A );
                Assert.Greater( grass.Mesh.Colors[ i + 2 ].G, grass.Mesh.Colors[ i ].G );
            }
        }

        [ Test ]
        public void Grass_IsDeterministic_AndDiffersBetweenChunks( ) {
            var grid = TerrainTestHelper.Filled( 39, TerrainTestHelper.Ground( ) );
            var p = TerrainTestHelper.Params( grass: true );
            var a = GrassBuilder.Build( TerrainTestHelper.WindowOf( grid, 1, 1 ), 1, 1, p );
            var b = GrassBuilder.Build( TerrainTestHelper.WindowOf( grid, 1, 1 ), 1, 1, p );
            var other = GrassBuilder.Build( TerrainTestHelper.WindowOf( grid, 2, 1 ), 2, 1, p );

            CollectionAssert.AreEqual( a.Mesh.Vertices, b.Mesh.Vertices );
            CollectionAssert.AreEqual( a.Mesh.Colors, b.Mesh.Colors );
            Assert.AreNotEqual( a.Mesh.VertexCount + "/" + a.Mesh.Vertices[ 0 ].Y, "x" );
            Assert.AreNotEqual( a.Mesh.Vertices[ 0 ].X, other.Mesh.Vertices[ 0 ].X );
        }

        [ Test ]
        public void Density_FollowsTheNoiseThreshold( ) {
            var grid = TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( ) );
            var sparse = TerrainTestHelper.Params( grass: true );
            sparse.GrassNoiseThreshold = 0.7f;
            var dense = TerrainTestHelper.Params( grass: true );
            dense.GrassNoiseThreshold = 0f;

            var sparseVerts = Grass( grid, sparse )?.Mesh.VertexCount ?? 0;
            var denseVerts = Grass( grid, dense ).Mesh.VertexCount;
            Assert.Less( sparseVerts, denseVerts );
        }

        [ Test ]
        public void BiomeWithoutStyle_GetsNoGrass( ) {
            var grid = TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( TerrainBiome.Stone ) );
            Assert.IsNull( Grass( grid, TerrainTestHelper.Params( grass: true ) ) );
        }

        private static bool InsideBounds( GrassBuilder.GrassMesh g, Float3 v ) {
            return Math.Abs( v.X - g.BoundsCenter.X ) <= g.BoundsSize.X * 0.5f
                && Math.Abs( v.Y - g.BoundsCenter.Y ) <= g.BoundsSize.Y * 0.5f
                && Math.Abs( v.Z - g.BoundsCenter.Z ) <= g.BoundsSize.Z * 0.5f;
        }

        [ Test ]
        public void Bounds_ContainEveryTuft( ) {
            var p = TerrainTestHelper.Params( grass: true );
            var grass = Grass( Meadow( 13 ), p );
            Assert.IsTrue( grass.Mesh.Vertices.All( v => InsideBounds( grass, v ) ) );
        }

        [ Test ]
        public void Cells_SplitTheChunksGrass_AndEachHasItsOwnBounds( ) {
            var grid = TerrainTestHelper.Filled( 39, TerrainTestHelper.Ground( ) );
            var p = TerrainTestHelper.Params( grass: true );
            var window = TerrainTestHelper.WindowOf( grid, 1, 1 );
            var whole = GrassBuilder.Build( window, 1, 1, p );
            var cells = GrassBuilder.BuildCells( window, 1, 1, p );

            Assert.Greater( cells.Length, 1 );
            Assert.AreEqual( whole.Mesh.VertexCount, cells.Sum( c => c.Mesh.VertexCount ), "same tufts, just partitioned" );
            Assert.AreEqual( whole.Mesh.Vertices.Sum( v => ( double ) v.X ), cells.Sum( c => c.Mesh.Vertices.Sum( v => ( double ) v.X ) ), 1e-2 );
            foreach( var cell in cells ) {
                Assert.IsTrue( cell.Mesh.Vertices.All( v => InsideBounds( cell, v ) ) );
                Assert.Less( cell.BoundsSize.X, whole.BoundsSize.X, "a block is smaller than the whole chunk" );
            }
        }

        [ Test ]
        public void Cells_Disabled_ProducesNothing( ) {
            var grid = Meadow( 6 );
            Assert.IsNull( GrassBuilder.BuildCells( TerrainTestHelper.WindowOf( grid, 0, 0 ), 0, 0, TerrainTestHelper.Params( grass: false ) ) );
        }

        [ Test ]
        public void Hash_IsStable( ) {
            Assert.AreEqual( GrassBuilder.Hash( 1, 2, 3, 4, 5 ), GrassBuilder.Hash( 1, 2, 3, 4, 5 ) );
            Assert.AreNotEqual( GrassBuilder.Hash( 1, 2, 3, 4, 5 ), GrassBuilder.Hash( 1, 2, 3, 4, 6 ) );
        }
    }

    public class ChunkVisualComputerTests {

        [ Test ]
        public void EmptyChunk_HasNoTerrain( ) {
            var data = TerrainTestHelper.Compute( new TerrainGrid( 26 ), TerrainTestHelper.Params( ) );
            Assert.IsFalse( data.HasTerrain );
            Assert.IsNull( data.Top );
        }

        [ Test ]
        public void ChunkWithGround_ProducesAllParts( ) {
            var data = TerrainTestHelper.Compute( TerrainTestHelper.Filled( 26, TerrainTestHelper.Ground( ) ), TerrainTestHelper.Params( grass: true ) );
            Assert.IsTrue( data.HasTerrain );
            Assert.NotNull( data.Splat );
            Assert.NotNull( data.Top );
            Assert.NotNull( data.Cliffs );
            Assert.NotNull( data.Grass );
        }

        [ Test ]
        public void FullyInteriorChunk_HasNoCliffs( ) {
            var grid = TerrainTestHelper.Filled( 39, TerrainTestHelper.Ground( ) );
            var data = TerrainTestHelper.Compute( grid, TerrainTestHelper.Params( ), 1, 1 );
            Assert.IsNull( data.Cliffs );
        }

        [ Test ]
        public void Window_CoversTheChunkPlusOneCell( ) {
            Assert.AreEqual( -1, ChunkVisualComputer.WindowOrigin( 0, 13 ) );
            Assert.AreEqual( 12, ChunkVisualComputer.WindowOrigin( 1, 13 ) );
            Assert.AreEqual( 15, ChunkVisualComputer.WindowSize( 13 ) );
        }

        [ Test ]
        public void Compute_OnAHomeSizedMap_IsFast( ) {
            var grid = TerrainTestHelper.Filled( 130, TerrainTestHelper.Ground( ) );
            var p = TerrainTestHelper.Params( grass: true );
            var stopwatch = System.Diagnostics.Stopwatch.StartNew( );
            for( int cy = 0; cy < 10; cy++ ) {
                for( int cx = 0; cx < 10; cx++ ) {
                    TerrainTestHelper.Compute( grid, p, cx, cy );
                }
            }
            stopwatch.Stop( );
            Assert.Less( stopwatch.ElapsedMilliseconds, 3000, "100 chunks" );
        }
    }
}
