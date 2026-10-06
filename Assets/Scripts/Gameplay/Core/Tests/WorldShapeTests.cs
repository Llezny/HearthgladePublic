using System;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class WorldShapeTests {

        private const int Size = 130;

        private static readonly NoiseSettings Height = new NoiseSettings( 4, 0.5, 2, 30 );
        private static readonly NoiseSettings Temperature = new NoiseSettings( 2, 0.5, 2, 60 );
        private static readonly NoiseSettings Humidity = new NoiseSettings( 3, 0.5, 2, 40 );

        private static BiomeRule Rule( float heightFrom, float heightTo ) {
            return new BiomeRule {
                HeightFrom = heightFrom, HeightTo = heightTo,
                TemperatureFrom = -1, TemperatureTo = 1,
                HumidityFrom = -1, HumidityTo = 1
            };
        }

        // Water, beach, land - the same height bands as the Home map.
        private static readonly BiomeRule[] Rules = { Rule( -1f, -0.4f ), Rule( -0.4f, -0.2f ), Rule( -0.2f, 1f ) };
        private static readonly bool[] Liquid = { true, false, false };

        private static WorldShapeSettings HomeShape( ) {
            return new WorldShapeSettings {
                IslandRadius = 0.8, IslandFalloff = 0.2, InteriorFloor = -0.3,
                WarpScale = 25, WarpStrength = 10,
                KeepMainIslandOnly = true, MinLakeSize = 12
            };
        }

        private static GeneratedTerrain Generate( int seed, WorldShapeSettings shape ) {
            return TerrainGenerator.Generate( Size, seed, Height, Temperature, Humidity, Rules, shape, Liquid );
        }

        private static bool IsLand( GeneratedTerrain terrain, int x, int z ) {
            int biome = terrain.Biome[ z * terrain.Size + x ];
            return biome >= 0 && !Liquid[ biome ];
        }

        [ Test ]
        public void IslandCoverage_IsZeroInTheMiddleAndOneAtTheBorder( ) {
            var shape = HomeShape( );
            Assert.AreEqual( 0.0, shape.IslandCoverage( 64.5, 64.5, Size ), 1e-9 );
            Assert.AreEqual( 1.0, shape.IslandCoverage( 0, 0, Size ), 1e-9 );
            Assert.AreEqual( 1.0, shape.IslandCoverage( 0, 64.5, Size ), 1e-9 );
        }

        [ Test ]
        public void IslandCoverage_WithoutARadius_IsAlwaysZero( ) {
            Assert.AreEqual( 0.0, default( WorldShapeSettings ).IslandCoverage( 0, 0, Size ) );
        }

        [ Test ]
        public void DefaultShape_MatchesTheOldGeneration( ) {
            var plain = TerrainGenerator.Generate( Size, 7, Height, Temperature, Humidity, Rules );
            var shaped = Generate( 7, default );
            CollectionAssert.AreEqual( plain.Biome, shaped.Biome );
            CollectionAssert.AreEqual( plain.Height, shaped.Height );
        }

        [ Test ]
        public void SameSeed_GivesTheSameIsland( ) {
            var a = Generate( 5, HomeShape( ) );
            var b = Generate( 5, HomeShape( ) );
            CollectionAssert.AreEqual( a.Biome, b.Biome );
        }

        [ Test ]
        public void Island_IsOneConnectedPieceOfLand( [ Range( 1, 12 ) ] int seed ) {
            var terrain = Generate( seed, HomeShape( ) );
            IslandCleanup.Label( terrain, Liquid, wantWater: false, out var pieces );
            Assert.AreEqual( 1, pieces.Count, $"seed {seed}" );
        }

        [ Test ]
        public void Island_LeavesASeaMarginAtTheMapEdge( [ Range( 1, 30 ) ] int seed ) {
            var terrain = Generate( seed, HomeShape( ) );
            const int margin = 4;
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    bool inMargin = x < margin || z < margin || x >= Size - margin || z >= Size - margin;
                    Assert.IsFalse( inMargin && IsLand( terrain, x, z ), $"seed {seed}: land at ({x}, {z})" );
                }
            }
        }

        [ Test ]
        public void Island_HasARoomyShareOfLand( [ Range( 1, 12 ) ] int seed ) {
            var terrain = Generate( seed, HomeShape( ) );
            double land = Enumerable.Range( 0, Size * Size ).Count( i => IsLand( terrain, i % Size, i / Size ) ) / ( double ) ( Size * Size );
            Assert.That( land, Is.InRange( 0.35, 0.65 ), $"seed {seed}" );
        }

        [ Test ]
        public void SmallLakes_AreFilledIn( ) {
            var terrain = Generate( 3, HomeShape( ) );
            var water = IslandCleanup.Label( terrain, Liquid, wantWater: true, out var sizes );
            for( int label = 1; label <= sizes.Count; label++ ) {
                bool touchesBorder = Enumerable.Range( 0, water.Length ).Any( i => water[ i ] == label
                    && ( i % Size == 0 || i / Size == 0 || i % Size == Size - 1 || i / Size == Size - 1 ) );
                Assert.IsTrue( touchesBorder || sizes[ label - 1 ] >= 12, $"a lake of {sizes[ label - 1 ]} cells is left" );
            }
        }

        private static GeneratedTerrain GenerateWith( int seed, WorldShapeSettings shape, BiomePatch[] patches ) {
            return TerrainGenerator.Generate( Size, seed, Height, Temperature, Humidity, Rules, shape, Liquid, patches );
        }

        [ Test ]
        public void ClimateJitter_LeavesNoCellWithoutABiome( [ Range( 1, 6 ) ] int seed ) {
            var shape = HomeShape();
            shape.ClimateJitter = 0.3;
            shape.ClimateJitterScale = 4;
            Assert.AreEqual( 0, Generate( seed, shape ).UnmatchedCount );
        }

        [ Test ]
        public void ClimateJitter_MakesBordersRaggedButKeepsTheBiomeShares( ) {
            // Rules that split the land by temperature, so the border is the temperature contour.
            var rules = new[] { Rule( -1f, -0.4f ), new BiomeRule { HeightFrom = -0.4f, HeightTo = 1f, TemperatureFrom = -1f, TemperatureTo = 0f, HumidityFrom = -1f, HumidityTo = 1f },
                new BiomeRule { HeightFrom = -0.4f, HeightTo = 1f, TemperatureFrom = 0f, TemperatureTo = 1f, HumidityFrom = -1f, HumidityTo = 1f } };
            var plain = TerrainGenerator.Generate( Size, 3, Height, Temperature, Humidity, rules, HomeShape(), Liquid );
            var shape = HomeShape();
            shape.ClimateJitter = 0.15;
            shape.ClimateJitterScale = 4;
            var jittered = TerrainGenerator.Generate( Size, 3, Height, Temperature, Humidity, rules, shape, Liquid );

            int Borders( GeneratedTerrain t ) {
                int count = 0;
                for( int z = 0; z < Size - 1; z++ ) {
                    for( int x = 0; x < Size - 1; x++ ) {
                        int a = t.Biome[ z * Size + x ], b = t.Biome[ z * Size + x + 1 ];
                        count += a != b && a > 0 && b > 0 ? 1 : 0;
                    }
                }
                return count;
            }
            Assert.Greater( Borders( jittered ), Borders( plain ), "a ragged border has more edges than a smooth one" );
        }

        [ Test ]
        public void Patches_AreOnlyInsideTheirBiome_AndTakeTheirCoverage( ) {
            var patch = new BiomePatch { Biome = 2, Visual = TerrainBiome.Dirt, Coverage = 0.3, Scale = 6, Salt = 1 };
            var terrain = GenerateWith( 4, HomeShape(), new[] { patch } );
            int patched = 0, inBiome = 0;
            for( int i = 0; i < terrain.Patch.Length; i++ ) {
                if( terrain.Patch[ i ] != 0 ) {
                    patched++;
                    Assert.AreEqual( 2, terrain.Biome[ i ], "a patch outside its biome" );
                    Assert.AreEqual( TerrainBiome.Dirt + 1, terrain.Patch[ i ] );
                }
                inBiome += terrain.Biome[ i ] == 2 ? 1 : 0;
            }
            Assert.Greater( patched, 0 );
            Assert.That( patched / ( double ) inBiome, Is.InRange( 0.15, 0.45 ), "roughly the coverage of the biome cells" );
        }

        [ Test ]
        public void Patches_AreDeterministic_AndOptional( ) {
            var patch = new BiomePatch { Biome = 2, Visual = TerrainBiome.Stone, Coverage = 0.2, Scale = 5, Salt = 9 };
            CollectionAssert.AreEqual( GenerateWith( 6, HomeShape(), new[] { patch } ).Patch, GenerateWith( 6, HomeShape(), new[] { patch } ).Patch );
            Assert.IsTrue( GenerateWith( 6, HomeShape(), null ).Patch.All( p => p == 0 ) );
            Assert.IsTrue( GenerateWith( 6, HomeShape(), new[] { new BiomePatch { Biome = 2, Visual = 1, Coverage = 0, Scale = 5 } } ).Patch.All( p => p == 0 ) );
        }

        [ Test ]
        public void Patches_DoNotChangeTheBiomes( ) {
            var patch = new BiomePatch { Biome = 2, Visual = TerrainBiome.Dirt, Coverage = 0.3, Scale = 6, Salt = 1 };
            CollectionAssert.AreEqual( Generate( 4, HomeShape() ).Biome, GenerateWith( 4, HomeShape(), new[] { patch } ).Biome );
        }

        [ Test ]
        public void Golden_HomeShape_IsStable( ) {
            // Guards determinism of the shaped generation. If a change is intended, bump WorldGenVersion.Current
            // and update the expected values.
            var terrain = Generate( 12345, HomeShape( ) );
            unchecked {
                uint hash = 2166136261u;
                for( int i = 0; i < terrain.Biome.Length; i++ ) {
                    hash = ( hash ^ ( uint ) ( terrain.Biome[ i ] + 1 ) ) * 16777619u;
                }
                Assert.AreEqual( 420998433u, hash );
            }
        }
    }

    public class PlayerStartFinderTests {

        [ Test ]
        public void Start_IsOnTheWestCoastInTheMiddle( ) {
            // Land is a block from x = 10 on, everything to the west is sea.
            bool Standable( int x, int z ) => x >= 10 && x < 50 && z >= 0 && z < 50;
            Assert.IsTrue( PlayerStartFinder.TryFind( 50, Standable, 5, out int x, out int z ) );
            Assert.AreEqual( 10 + PlayerStartFinder.InlandOffset, x );
            Assert.AreEqual( 25, z );
        }

        [ Test ]
        public void Start_SkipsTinyPatches( ) {
            // A lone standable cell in the middle row, a real region further along.
            bool Standable( int x, int z ) => ( x == 3 && z == 25 ) || ( x >= 20 && z >= 20 && z < 30 );
            Assert.IsTrue( PlayerStartFinder.TryFind( 50, Standable, 5, out int x, out _ ) );
            Assert.GreaterOrEqual( x, 20 );
        }

        [ Test ]
        public void Start_LooksAtNearbyRows_WhenTheMiddleRowIsSea( ) {
            bool Standable( int x, int z ) => x >= 10 && z >= 28 && z < 40;
            Assert.IsTrue( PlayerStartFinder.TryFind( 50, Standable, 5, out _, out int z ) );
            Assert.GreaterOrEqual( z, 28 );
        }

        [ Test ]
        public void NoLand_GivesNoStart( ) {
            Assert.IsFalse( PlayerStartFinder.TryFind( 20, ( x, z ) => false, 5, out _, out _ ) );
        }
    }
}
