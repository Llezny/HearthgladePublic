using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class TerrainGeneratorTests {

        // The shipped Home presets.
        private static readonly NoiseSettings Height = new NoiseSettings( 4, 0.5, 2, 30 );
        private static readonly NoiseSettings Temperature = new NoiseSettings( 2, 0.5, 2, 60 );
        private static readonly NoiseSettings Humidity = new NoiseSettings( 3, 0.5, 2, 40 );

        private static BiomeRule CatchAll( ) {
            return new BiomeRule {
                HeightFrom = -1.01f, HeightTo = 1.01f,
                TemperatureFrom = -1.01f, TemperatureTo = 1.01f,
                HumidityFrom = -1.01f, HumidityTo = 1.01f
            };
        }

        // Splits the height axis into two biomes, so a generated map has both of them.
        private static BiomeRule[] LowAndHigh( ) {
            var low = CatchAll( );
            low.HeightTo = 0f;
            var high = CatchAll( );
            high.HeightFrom = 0f;
            return new[] { low, high };
        }

        [ Test ]
        public void SameSeed_GivesIdenticalTerrain( ) {
            var a = TerrainGenerator.Generate( 40, 1234, Height, Temperature, Humidity, LowAndHigh( ) );
            var b = TerrainGenerator.Generate( 40, 1234, Height, Temperature, Humidity, LowAndHigh( ) );
            CollectionAssert.AreEqual( a.Biome, b.Biome );
            CollectionAssert.AreEqual( a.Height, b.Height );
            CollectionAssert.AreEqual( a.Humidity, b.Humidity );
        }

        [ Test ]
        public void DifferentSeeds_GiveDifferentTerrain( ) {
            var a = TerrainGenerator.Generate( 40, 1, Height, Temperature, Humidity, LowAndHigh( ) );
            var b = TerrainGenerator.Generate( 40, 2, Height, Temperature, Humidity, LowAndHigh( ) );
            var differing = a.Biome.Where( ( biome, i ) => biome != b.Biome[ i ] ).Count( );
            Assert.Greater( differing, 100 );
        }

        [ Test ]
        public void HumidityLayer_UsesItsOwnSettings( ) {
            // Regression: the humidity noise used to be generated from the temperature preset.
            var withHumiditySettings = TerrainGenerator.Generate( 30, 5, Height, Temperature, Humidity, LowAndHigh( ) );
            var withTemperatureSettings = TerrainGenerator.Generate( 30, 5, Height, Temperature, Temperature, LowAndHigh( ) );
            CollectionAssert.AreEqual( withHumiditySettings.Height, withTemperatureSettings.Height );
            CollectionAssert.AreEqual( withHumiditySettings.Temperature, withTemperatureSettings.Temperature );
            CollectionAssert.AreNotEqual( withHumiditySettings.Humidity, withTemperatureSettings.Humidity );
        }

        [ Test ]
        public void Layers_AreIndependentOfEachOther( ) {
            var data = TerrainGenerator.Generate( 30, 5, Height, Height, Height, LowAndHigh( ) );
            CollectionAssert.AreNotEqual( data.Height, data.Temperature );
            CollectionAssert.AreNotEqual( data.Temperature, data.Humidity );
        }

        [ Test ]
        public void CatchAllRule_MatchesEveryCell( ) {
            var data = TerrainGenerator.Generate( 25, 3, Height, Temperature, Humidity, new[] { CatchAll( ) } );
            Assert.AreEqual( 0, data.UnmatchedCount );
            Assert.IsTrue( data.Biome.All( biome => biome == 0 ) );
        }

        [ Test ]
        public void NoRules_LeavesEveryCellUnmatched( ) {
            var data = TerrainGenerator.Generate( 10, 3, Height, Temperature, Humidity, new BiomeRule[ 0 ] );
            Assert.AreEqual( 100, data.UnmatchedCount );
            Assert.IsTrue( data.Biome.All( biome => biome == GeneratedTerrain.NoBiome ) );
        }

        [ Test ]
        public void FirstMatchingRule_Wins( ) {
            var data = TerrainGenerator.Generate( 10, 3, Height, Temperature, Humidity, new[] { CatchAll( ), CatchAll( ) } );
            Assert.IsTrue( data.Biome.All( biome => biome == 0 ) );
        }

        [ Test ]
        public void Rule_UsesHalfOpenRanges( ) {
            var rule = CatchAll( );
            rule.HeightFrom = 0.25f;
            rule.HeightTo = 0.5f;
            Assert.IsTrue( rule.Matches( 0.25, 0, 0 ) );
            Assert.IsFalse( rule.Matches( 0.5, 0, 0 ) );
            Assert.IsFalse( rule.Matches( 0.2499, 0, 0 ) );
        }

        [ Test ]
        public void LowAndHighRules_BothAppear_OnARealisticMap( ) {
            var data = TerrainGenerator.Generate( 130, 99, Height, Temperature, Humidity, LowAndHigh( ) );
            var low = data.Biome.Count( biome => biome == 0 );
            var high = data.Biome.Count( biome => biome == 1 );
            Assert.AreEqual( 130 * 130, low + high );
            Assert.Greater( low, 1000 );
            Assert.Greater( high, 1000 );
        }

        [ Test ]
        public void Index_IsRowMajor_AndSizeZeroIsEmpty( ) {
            var data = TerrainGenerator.Generate( 4, 1, Height, Temperature, Humidity, LowAndHigh( ) );
            Assert.AreEqual( 0, data.Index( 0, 0 ) );
            Assert.AreEqual( 3, data.Index( 3, 0 ) );
            Assert.AreEqual( 4, data.Index( 0, 1 ) );
            Assert.AreEqual( 15, data.Index( 3, 3 ) );
            Assert.AreEqual( 0, TerrainGenerator.Generate( 0, 1, Height, Temperature, Humidity, LowAndHigh( ) ).Biome.Length );
        }

        [ Test ]
        public void InvalidArguments_Throw( ) {
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => TerrainGenerator.Generate( -1, 1, Height, Temperature, Humidity, LowAndHigh( ) ) );
            Assert.Throws<ArgumentNullException>( ( ) => TerrainGenerator.Generate( 5, 1, Height, Temperature, Humidity, null ) );
        }

        [ Test ]
        public void Layers_AreEqualised_SoRangesAreSharesOfTheMap( ) {
            var data = TerrainGenerator.Generate( 130, 5, Height, Temperature, Humidity, LowAndHigh( ) );
            foreach( var layer in new[] { data.Height, data.Temperature, data.Humidity } ) {
                Assert.Greater( layer.Min( ), -1.0 );
                Assert.Less( layer.Max( ), 1.0 );
                Assert.AreEqual( 0.0, layer.Average( ), 0.001 );
                // Uniform: the lowest tenth of the range holds a tenth of the cells.
                Assert.AreEqual( 0.1, layer.Count( v => v < -0.8 ) / ( double ) layer.Length, 0.001 );
            }
        }

        [ Test ]
        public void BiomeShares_DoNotDependOnTheSeed( ) {
            var lowest = CatchAll( );
            lowest.HeightTo = -0.8f;
            var rest = CatchAll( );
            rest.HeightFrom = -0.8f;
            foreach( var seed in new[] { 1, 2, 3, 12345, 777 } ) {
                var data = TerrainGenerator.Generate( 130, seed, Height, Temperature, Humidity, new[] { lowest, rest } );
                var stats = TerrainStats.Compute( data, 2 );
                Assert.AreEqual( 0.10, stats.BiomeShare[ 0 ], 0.001, $"seed {seed}" );
                Assert.AreEqual( 0.90, stats.BiomeShare[ 1 ], 0.001, $"seed {seed}" );
            }
        }

        [ Test ]
        public void LargerScale_GivesSmootherFeatures( ) {
            double Roughness( double scale ) {
                var data = TerrainGenerator.Generate( 60, 9, new NoiseSettings( 1, 0.5, 2, scale ), Temperature, Humidity, LowAndHigh( ) );
                double sum = 0;
                for( int z = 0; z < 60; z++ ) {
                    for( int x = 0; x < 59; x++ ) {
                        sum += Math.Abs( data.Height[ data.Index( x + 1, z ) ] - data.Height[ data.Index( x, z ) ] );
                    }
                }
                return sum;
            }
            Assert.Less( Roughness( 40 ), Roughness( 8 ) );
        }

        [ Test ]
        public void NonPositiveScale_IsTreatedAsOne( ) {
            var explicitOne = TerrainGenerator.Generate( 20, 4, new NoiseSettings( 2, 0.5, 2, 1 ), Temperature, Humidity, LowAndHigh( ) );
            var zero = TerrainGenerator.Generate( 20, 4, new NoiseSettings( 2, 0.5, 2, 0 ), Temperature, Humidity, LowAndHigh( ) );
            CollectionAssert.AreEqual( explicitOne.Height, zero.Height );
        }

        [ Test ]
        public void HomePresets_KeepAGoldenBiomeLayout( ) {
            // Guards determinism: any change to the noise, the seed mixing or the equalising shifts this hash.
            // If the change is intended, bump WorldGenVersion.Current and update the expected value.
            var data = TerrainGenerator.Generate( 130, 12345, Height, Temperature, Humidity, LowAndHigh( ) );
            unchecked {
                uint hash = 2166136261u;
                foreach( var biome in data.Biome ) {
                    hash = ( hash ^ ( uint ) ( biome + 1 ) ) * 16777619u;
                }
                Assert.AreEqual( 2926191557u, hash );
            }
        }

        [ Test ]
        public void Generation_OfAHomeSizedMap_IsFast( ) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew( );
            TerrainGenerator.Generate( 130, 1, Height, Temperature, Humidity, LowAndHigh( ) );
            stopwatch.Stop( );
            // Generous bound: it is ~tens of ms, the old delegate-based Perlin was several times slower.
            Assert.Less( stopwatch.ElapsedMilliseconds, 1500 );
        }
    }

    public class ChunkMathTests {

        [ TestCase( 0, 13, 0 ) ]
        [ TestCase( 12, 13, 0 ) ]
        [ TestCase( 13, 13, 1 ) ]
        [ TestCase( 25, 13, 1 ) ]
        [ TestCase( 26, 13, 2 ) ]
        [ TestCase( -1, 13, -1 ) ]
        [ TestCase( -13, 13, -1 ) ]
        [ TestCase( -14, 13, -2 ) ]
        public void FloorDiv_RoundsTowardsNegativeInfinity( int value, int divisor, int expected ) {
            Assert.AreEqual( expected, ChunkMath.FloorDiv( value, divisor ) );
        }

        [ Test ]
        public void ChunkOf_EveryBlockOfAMap_BelongsToItsOwnChunk( ) {
            const int chunkSize = 13;
            for( int chunk = 0; chunk < 10; chunk++ ) {
                for( int offset = 0; offset < chunkSize; offset++ ) {
                    var block = chunk * chunkSize + offset;
                    Assert.AreEqual( new ChunkCoord( chunk, chunk ), ChunkMath.ChunkOf( block, block, chunkSize ) );
                }
            }
        }

        [ Test ]
        public void IsInMap_RespectsBounds_AndNegativeSizeIsEndless( ) {
            Assert.IsTrue( ChunkMath.IsInMap( new ChunkCoord( 0, 0 ), 3 ) );
            Assert.IsTrue( ChunkMath.IsInMap( new ChunkCoord( 2, 2 ), 3 ) );
            Assert.IsFalse( ChunkMath.IsInMap( new ChunkCoord( 3, 0 ), 3 ) );
            Assert.IsFalse( ChunkMath.IsInMap( new ChunkCoord( 0, -1 ), 3 ) );
            Assert.IsTrue( ChunkMath.IsInMap( new ChunkCoord( -50, 900 ), -1 ) );
        }

        [ Test ]
        public void Distance_IsChebyshev( ) {
            Assert.AreEqual( 0, ChunkMath.Distance( new ChunkCoord( 1, 1 ), new ChunkCoord( 1, 1 ) ) );
            Assert.AreEqual( 1, ChunkMath.Distance( new ChunkCoord( 1, 1 ), new ChunkCoord( 2, 2 ) ) );
            Assert.AreEqual( 3, ChunkMath.Distance( new ChunkCoord( 0, 0 ), new ChunkCoord( -3, 2 ) ) );
        }

        [ Test ]
        public void ChunkCoord_WorksAsHashSetKey( ) {
            var set = new HashSet<ChunkCoord> { new ChunkCoord( 1, 2 ), new ChunkCoord( 1, 2 ), new ChunkCoord( 2, 1 ) };
            Assert.AreEqual( 2, set.Count );
        }

        [ Test ]
        public void WorldToGrid_RoundTripsEveryCell_AndCoversTheWholeTile( ) {
            const float tile = 0.3675f;
            for( int grid = -20; grid < 400; grid++ ) {
                var centre = GridMath.GridToWorld( grid, tile );
                Assert.AreEqual( grid, GridMath.WorldToGrid( centre, tile ) );
                Assert.AreEqual( grid, GridMath.WorldToGrid( centre + tile * 0.49f, tile ) );
                Assert.AreEqual( grid, GridMath.WorldToGrid( centre - tile * 0.49f, tile ) );
            }
        }
    }

    public class ChunkStreamingPlannerTests {

        private static HashSet<ChunkCoord> Set( params ( int x, int y )[] cells ) {
            return new HashSet<ChunkCoord>( cells.Select( c => new ChunkCoord( c.x, c.y ) ) );
        }

        [ Test ]
        public void FirstUpdate_InsideAMap_ShowsTheWholeThreeByThree_CentreFirst( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            Assert.IsTrue( planner.Update( new ChunkCoord( 5, 5 ) ) );

            Assert.AreEqual( 9, planner.ToShow.Count );
            Assert.AreEqual( new ChunkCoord( 5, 5 ), planner.ToShow[ 0 ] );
            Assert.AreEqual( 0, planner.ToHide.Count );
            Assert.AreEqual( 9, planner.Loaded.Count );
        }

        [ Test ]
        public void SameCentre_ReportsNoChange( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 5, 5 ) );
            Assert.IsFalse( planner.Update( new ChunkCoord( 5, 5 ) ) );
        }

        [ Test ]
        public void StepAlongAxis_ShowsThreeAndHidesThree( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 5, 5 ) );
            planner.Update( new ChunkCoord( 6, 5 ) );

            CollectionAssert.AreEquivalent( Set( ( 7, 4 ), ( 7, 5 ), ( 7, 6 ) ), planner.ToShow );
            CollectionAssert.AreEquivalent( Set( ( 4, 4 ), ( 4, 5 ), ( 4, 6 ) ), planner.ToHide );
        }

        [ Test ]
        public void DiagonalStep_ShowsFiveAndHidesFive( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 5, 5 ) );
            planner.Update( new ChunkCoord( 6, 6 ) );

            Assert.AreEqual( 5, planner.ToShow.Count );
            Assert.AreEqual( 5, planner.ToHide.Count );
        }

        [ Test ]
        public void NearMapCorner_OnlyChunksInsideTheMapAreLoaded( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 0, 0 ) );
            CollectionAssert.AreEquivalent( Set( ( 0, 0 ), ( 1, 0 ), ( 0, 1 ), ( 1, 1 ) ), planner.Loaded );

            planner.Update( new ChunkCoord( 9, 9 ) );
            CollectionAssert.AreEquivalent( Set( ( 9, 9 ), ( 8, 9 ), ( 9, 8 ), ( 8, 8 ) ), planner.Loaded );
        }

        [ Test ]
        public void WalkingOutAndBack_EndsWithTheOriginalWindow( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 5, 5 ) );
            var original = new HashSet<ChunkCoord>( planner.Loaded );

            planner.Update( new ChunkCoord( 6, 5 ) );
            var hiddenOnTheWayOut = planner.ToHide.ToList( );
            planner.Update( new ChunkCoord( 5, 5 ) );

            CollectionAssert.AreEquivalent( original, planner.Loaded );
            // Everything that was hidden on the way out is offered again, so a consumer that still has a
            // pending hide for it can (and must) check Loaded before hiding.
            CollectionAssert.AreEquivalent( hiddenOnTheWayOut, planner.ToShow );
        }

        [ Test ]
        public void Refresh_WithNewCentre_ReplacesTheWindow( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            planner.Update( new ChunkCoord( 2, 2 ) );
            planner.Refresh( new ChunkCoord( 8, 8 ) );

            Assert.AreEqual( 9, planner.ToShow.Count );
            Assert.AreEqual( 9, planner.ToHide.Count );
            Assert.IsTrue( planner.Loaded.Contains( new ChunkCoord( 8, 8 ) ) );
        }

        [ Test ]
        public void GetRing_IsSortedNearestFirst_AndDeterministic( ) {
            var planner = new ChunkStreamingPlanner( 10 );
            var first = new List<ChunkCoord>( );
            var second = new List<ChunkCoord>( );
            planner.GetRing( new ChunkCoord( 5, 5 ), 2, first );
            planner.GetRing( new ChunkCoord( 5, 5 ), 2, second );

            Assert.AreEqual( 25, first.Count );
            CollectionAssert.AreEqual( first, second );
            for( int i = 1; i < first.Count; i++ ) {
                Assert.LessOrEqual(
                    ChunkMath.Distance( first[ i - 1 ], new ChunkCoord( 5, 5 ) ),
                    ChunkMath.Distance( first[ i ], new ChunkCoord( 5, 5 ) ) );
            }
        }

        [ Test ]
        public void EndlessMap_HasNoBoundsAtAll( ) {
            var planner = new ChunkStreamingPlanner( -1 );
            planner.Update( new ChunkCoord( -7, 40 ) );
            Assert.AreEqual( 9, planner.Loaded.Count );
        }

        [ Test ]
        public void RadiusTwo_LoadsFiveByFive( ) {
            var planner = new ChunkStreamingPlanner( 10, 2 );
            planner.Update( new ChunkCoord( 5, 5 ) );
            Assert.AreEqual( 25, planner.Loaded.Count );
        }
    }

    public class GridFloodTests {

        private static Func<int, int, bool> Open( int size ) {
            return ( x, y ) => x >= 0 && y >= 0 && x < size && y < size;
        }

        [ Test ]
        public void StartNotPassable_ReturnsZero( ) {
            Assert.AreEqual( 0, GridFlood.CountReachable( 0, 0, ( x, y ) => false, 5 ) );
        }

        [ Test ]
        public void IsolatedCell_HasSizeOne( ) {
            Assert.AreEqual( 1, GridFlood.CountReachable( 3, 3, ( x, y ) => x == 3 && y == 3, 5 ) );
        }

        [ Test ]
        public void SmallIsland_IsCountedExactly( ) {
            var island = new HashSet<(int, int)> { ( 0, 0 ), ( 1, 0 ), ( 2, 0 ), ( 2, 1 ) };
            Assert.AreEqual( 4, GridFlood.CountReachable( 0, 0, ( x, y ) => island.Contains( ( x, y ) ), 100 ) );
        }

        [ Test ]
        public void DiagonalNeighbours_AreConnected( ) {
            var cells = new HashSet<(int, int)> { ( 0, 0 ), ( 1, 1 ), ( 2, 2 ) };
            Assert.AreEqual( 3, GridFlood.CountReachable( 0, 0, ( x, y ) => cells.Contains( ( x, y ) ), 100 ) );
        }

        [ Test ]
        public void DisjointIslands_AreNotMixed( ) {
            var cells = new HashSet<(int, int)> { ( 0, 0 ), ( 1, 0 ), ( 10, 10 ), ( 11, 10 ), ( 12, 10 ) };
            Assert.AreEqual( 2, GridFlood.CountReachable( 0, 0, ( x, y ) => cells.Contains( ( x, y ) ), 100 ) );
            Assert.AreEqual( 3, GridFlood.CountReachable( 10, 10, ( x, y ) => cells.Contains( ( x, y ) ), 100 ) );
        }

        [ Test ]
        public void LargeRegion_ExceedsTheLimit( ) {
            Assert.Greater( GridFlood.CountReachable( 5, 5, Open( 50 ), 5 ), 5 );
        }

        [ Test ]
        public void LargeRegion_StopsEarly_InsteadOfWalkingTheWholeMap( ) {
            var calls = 0;
            Func<int, int, bool> counting = ( x, y ) => {
                calls++;
                return x >= 0 && y >= 0 && x < 1000 && y < 1000;
            };
            GridFlood.CountReachable( 500, 500, counting, 5 );
            Assert.Less( calls, 200, "a limit of 5 must not flood a million cells" );
        }

        [ Test ]
        public void NegativeCoordinates_Work( ) {
            Assert.AreEqual( 9, GridFlood.CountReachable( -5, -5, ( x, y ) => x >= -6 && x <= -4 && y >= -6 && y <= -4, 100 ) );
        }
    }

    public class ResourceRollerTests {

        private static readonly ResourceRule[] Trees = {
            new ResourceRule { Count = 1, Probability = 0.2f, SpawnRange = 0.35f, MinRotation = 0, MaxRotation = 360 },
            new ResourceRule { Count = 2, Probability = 0.5f, SpawnRange = 0.2f, MinRotation = 90, MaxRotation = 180 }
        };

        private static List<ResourcePlacement> Roll( ResourceRule[] rules, int seed, int x, int y ) {
            var result = new List<ResourcePlacement>( );
            ResourceRoller.Roll( rules, seed, x, y, result );
            return result;
        }

        [ Test ]
        public void SameSeedAndCell_GiveTheSameResult( ) {
            for( int cell = 0; cell < 200; cell++ ) {
                var a = Roll( Trees, 42, cell, cell * 3 );
                var b = Roll( Trees, 42, cell, cell * 3 );
                CollectionAssert.AreEqual( a, b );
            }
        }

        [ Test ]
        public void Result_DoesNotDependOnWhatWasRolledBefore( ) {
            var direct = Roll( Trees, 7, 30, 40 );
            for( int i = 0; i < 50; i++ ) {
                Roll( Trees, 7, i, i );
            }
            CollectionAssert.AreEqual( direct, Roll( Trees, 7, 30, 40 ) );
        }

        [ Test ]
        public void DifferentSeedsOrCells_GiveDifferentLayouts( ) {
            var layoutA = Enumerable.Range( 0, 500 ).Select( i => Roll( Trees, 1, i, 0 ).Count ).ToArray( );
            var layoutB = Enumerable.Range( 0, 500 ).Select( i => Roll( Trees, 2, i, 0 ).Count ).ToArray( );
            CollectionAssert.AreNotEqual( layoutA, layoutB );
        }

        [ Test ]
        public void NoRules_GiveNoPlacements( ) {
            Assert.AreEqual( 0, Roll( null, 1, 0, 0 ).Count );
            Assert.AreEqual( 0, Roll( new ResourceRule[ 0 ], 1, 0, 0 ).Count );
        }

        [ Test ]
        public void ProbabilityZeroNeverSpawns_ProbabilityOneAlwaysDoes( ) {
            var never = new[] { new ResourceRule { Count = 3, Probability = 0f, SpawnRange = 0.3f, MaxRotation = 360 } };
            var always = new[] { new ResourceRule { Count = 3, Probability = 1f, SpawnRange = 0.3f, MaxRotation = 360 } };
            for( int i = 0; i < 300; i++ ) {
                Assert.AreEqual( 0, Roll( never, 1, i, i ).Count );
                Assert.AreEqual( 3, Roll( always, 1, i, i ).Count );
            }
        }

        [ Test ]
        public void Placements_StayInsideTheSpawnDisc_AndRotationRange( ) {
            for( int i = 0; i < 1000; i++ ) {
                foreach( var placement in Roll( Trees, 5, i, i + 1 ) ) {
                    var rule = Trees[ placement.RuleIndex ];
                    var distance = Math.Sqrt( placement.OffsetX * placement.OffsetX + placement.OffsetZ * placement.OffsetZ );
                    Assert.LessOrEqual( distance, rule.SpawnRange + 1e-5 );
                    Assert.GreaterOrEqual( placement.RotationY, rule.MinRotation );
                    Assert.Less( placement.RotationY, rule.MaxRotation );
                }
            }
        }

        [ Test ]
        public void SpawnRate_MatchesTheConfiguredProbability( ) {
            var rules = new[] { new ResourceRule { Count = 1, Probability = 0.2f, SpawnRange = 0.3f, MaxRotation = 360 } };
            var total = 0;
            const int cells = 20000;
            for( int i = 0; i < cells; i++ ) {
                total += Roll( rules, 11, i % 130, i / 130 ).Count;
            }
            Assert.AreEqual( 0.2, total / ( double ) cells, 0.02 );
        }

        [ Test ]
        public void Offsets_AreSpreadOverTheWholeDisc( ) {
            var rules = new[] { new ResourceRule { Count = 1, Probability = 1f, SpawnRange = 0.3f, MaxRotation = 360 } };
            var quadrants = new int[ 4 ];
            for( int i = 0; i < 4000; i++ ) {
                var placement = Roll( rules, 3, i, 0 )[ 0 ];
                quadrants[ ( placement.OffsetX >= 0 ? 1 : 0 ) + ( placement.OffsetZ >= 0 ? 2 : 0 ) ]++;
            }
            foreach( var count in quadrants ) {
                Assert.Greater( count, 800 );
            }
        }
    }
}
