using System;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class LayerEqualizerTests {

        [ Test ]
        public void Equalize_SpreadsValuesEvenly_InsideOpenRange( ) {
            var values = new[] { 5.0, -3.0, 0.5, 100.0 };
            LayerEqualizer.Equalize( values );
            // Ranks 2, 0, 1, 3 of 4 -> the midpoints of four equal slices of [-1, 1).
            CollectionAssert.AreEqual( new[] { 0.25, -0.75, -0.25, 0.75 }, values );
        }

        [ Test ]
        public void Equalize_KeepsTheOrderOfTheValues( ) {
            var values = Enumerable.Range( 0, 200 ).Select( i => Math.Sin( i * 12.9898 ) * 0.3 ).ToArray( );
            var original = ( double[] ) values.Clone( );
            LayerEqualizer.Equalize( values );
            for( int a = 0; a < values.Length; a += 7 ) {
                for( int b = 0; b < values.Length; b += 11 ) {
                    if( original[ a ] < original[ b ] ) {
                        Assert.Less( values[ a ], values[ b ] );
                    }
                }
            }
        }

        [ Test ]
        public void Equalize_EqualValues_GetDistinctRanks_ByCellIndex( ) {
            var values = new[] { 0.0, 0.0, 0.0, 0.0 };
            LayerEqualizer.Equalize( values );
            CollectionAssert.AreEqual( new[] { -0.75, -0.25, 0.25, 0.75 }, values );
        }

        [ Test ]
        public void Equalize_IsDeterministic( ) {
            double[] Make( ) => Enumerable.Range( 0, 500 ).Select( i => Math.Cos( i * 0.37 ) ).ToArray( );
            var a = Make( );
            var b = Make( );
            LayerEqualizer.Equalize( a );
            LayerEqualizer.Equalize( b );
            CollectionAssert.AreEqual( a, b );
        }

        [ Test ]
        public void Equalize_EmptyAndSingle_AreHandled( ) {
            LayerEqualizer.Equalize( new double[ 0 ] );
            var single = new[] { 42.0 };
            LayerEqualizer.Equalize( single );
            Assert.AreEqual( 0.0, single[ 0 ] );
            Assert.Throws<ArgumentNullException>( ( ) => LayerEqualizer.Equalize( null ) );
        }
    }

    public class BiomeRuleValidatorTests {

        private static BiomeRule Rule( float h0, float h1, float t0 = -1, float t1 = 1, float m0 = -1, float m1 = 1 ) {
            return new BiomeRule {
                HeightFrom = h0, HeightTo = h1,
                TemperatureFrom = t0, TemperatureTo = t1,
                HumidityFrom = m0, HumidityTo = m1
            };
        }

        [ Test ]
        public void RulesThatPartitionTheCube_HaveNoGapsAndNoOverlaps( ) {
            var report = BiomeRuleValidator.Validate( new[] {
                Rule( -1, -0.5f ),
                Rule( -0.5f, 1, -1, 0 ),
                Rule( -0.5f, 1, 0, 1 )
            } );
            Assert.IsFalse( report.HasGaps );
            Assert.AreEqual( 0, report.Overlaps.Count );
            Assert.AreEqual( 0.0, report.GapVolume );
        }

        [ Test ]
        public void MissingSlice_IsReportedAsAGap_WithItsVolume( ) {
            // Nothing covers height [0.5, 1): a quarter of the cube.
            var report = BiomeRuleValidator.Validate( new[] { Rule( -1, 0.5f ) } );
            Assert.IsTrue( report.HasGaps );
            Assert.AreEqual( 0.25, report.GapVolume, 1e-9 );
            Assert.IsTrue( report.Gaps.All( gap => gap.HeightFrom >= 0.5f ) );
        }

        [ Test ]
        public void NoRules_MeansTheWholeCubeIsAGap( ) {
            var report = BiomeRuleValidator.Validate( new BiomeRule[ 0 ] );
            Assert.AreEqual( 1.0, report.GapVolume, 1e-9 );
        }

        [ Test ]
        public void TinyGap_BetweenTwoRules_IsFound( ) {
            var report = BiomeRuleValidator.Validate( new[] { Rule( -1, 0.1f ), Rule( 0.1001f, 1 ) } );
            Assert.IsTrue( report.HasGaps );
            Assert.Less( report.GapVolume, 0.001 );
        }

        [ Test ]
        public void ThePreviousHomeConfig_LeftTheTopOfTheRangeUncovered( ) {
            // To = 1 is exclusive, and the old configs had no rule for temperature above 1 either; the
            // equalised layers stay below 1, so a rule that ends at 1 covers everything.
            var report = BiomeRuleValidator.Validate( new[] { Rule( -1, 1 ) } );
            Assert.IsFalse( report.HasGaps );
        }

        [ Test ]
        public void OverlappingRules_AreReported_FirstOneWins( ) {
            var report = BiomeRuleValidator.Validate( new[] { Rule( -1, 0.5f ), Rule( 0, 1 ) } );
            Assert.IsFalse( report.HasGaps );
            Assert.AreEqual( 1, report.Overlaps.Count );
            Assert.AreEqual( 0, report.Overlaps[ 0 ].Winner );
            Assert.AreEqual( 1, report.Overlaps[ 0 ].Shadowed );
            Assert.GreaterOrEqual( report.Overlaps[ 0 ].Example.HeightFrom, 0f );
            Assert.LessOrEqual( report.Overlaps[ 0 ].Example.HeightTo, 0.5f );
        }

        [ Test ]
        public void RuleOutsideTheCube_DoesNotCreateFalseGapsOrOverlaps( ) {
            var report = BiomeRuleValidator.Validate( new[] { Rule( -1.5f, 1.5f ) } );
            Assert.IsFalse( report.HasGaps );
            Assert.AreEqual( 0, report.Overlaps.Count );
        }

        [ Test ]
        public void Null_Throws( ) {
            Assert.Throws<ArgumentNullException>( ( ) => BiomeRuleValidator.Validate( null ) );
        }

        [ Test ]
        public void ValidationAgreesWithGeneration_NoGapsMeansNoUnmatchedCells( ) {
            var rules = new[] { Rule( -1, -0.8f ), Rule( -0.8f, 1, -1, 0.1f ), Rule( -0.8f, 1, 0.1f, 1 ) };
            Assert.IsFalse( BiomeRuleValidator.Validate( rules ).HasGaps );
            var terrain = TerrainGenerator.Generate( 100, 3, new NoiseSettings( 4, 0.5, 2, 30 ), new NoiseSettings( 2, 0.5, 2, 60 ), new NoiseSettings( 3, 0.5, 2, 40 ), rules );
            Assert.AreEqual( 0, terrain.UnmatchedCount );
        }

        [ Test ]
        public void ClimateOffset_MovesTheMapTowardsTheWarmOrColdBiome( ) {
            var rules = new[] { Rule( -1, 1, -1, 0 ), Rule( -1, 1, 0, 1 ) };   // 0 = cold, 1 = warm, half the map each
            int Warm( double offset ) {
                var shape = new WorldShapeSettings { TemperatureOffset = offset };
                var terrain = TerrainGenerator.Generate( 80, 9, new NoiseSettings( 4, 0.5, 2, 30 ), new NoiseSettings( 2, 0.5, 2, 60 ), new NoiseSettings( 3, 0.5, 2, 40 ), rules, shape );
                return terrain.Biome.Count( b => b == 1 );
            }

            int cells = 80 * 80;
            Assert.That( Warm( 0 ), Is.InRange( cells * 0.45, cells * 0.55 ) );
            Assert.Greater( Warm( 0.4 ), Warm( 0 ) );
            Assert.Less( Warm( -0.4 ), Warm( 0 ) );
            Assert.AreEqual( cells, Warm( 1.0 ) );
            Assert.AreEqual( 0, Warm( -1.0 ) );
        }
    }

    public class TerrainStatsTests {

        [ Test ]
        public void Compute_CountsBiomeShares_AndUnmatchedCells( ) {
            var terrain = new GeneratedTerrain( 2 );
            terrain.Biome[ 0 ] = 0;
            terrain.Biome[ 1 ] = 0;
            terrain.Biome[ 2 ] = 1;
            terrain.Biome[ 3 ] = GeneratedTerrain.NoBiome;

            var stats = TerrainStats.Compute( terrain, 2 );

            Assert.AreEqual( 4, stats.CellCount );
            Assert.AreEqual( 0.5, stats.BiomeShare[ 0 ] );
            Assert.AreEqual( 0.25, stats.BiomeShare[ 1 ] );
            Assert.AreEqual( 0.25, stats.UnmatchedShare );
        }

        [ Test ]
        public void Compute_ReportsLayerRanges( ) {
            var terrain = new GeneratedTerrain( 2 );
            terrain.Height[ 0 ] = -0.5;
            terrain.Height[ 1 ] = 0.5;
            terrain.Height[ 2 ] = 0.25;
            terrain.Height[ 3 ] = 0.75;

            var stats = TerrainStats.Compute( terrain, 0 );

            Assert.AreEqual( -0.5, stats.Height.Min );
            Assert.AreEqual( 0.75, stats.Height.Max );
            Assert.AreEqual( 0.25, stats.Height.Mean, 1e-12 );
        }

        [ Test ]
        public void Compute_OfAnEmptyTerrain_IsAllZero( ) {
            var stats = TerrainStats.Compute( new GeneratedTerrain( 0 ), 3 );
            Assert.AreEqual( 0, stats.CellCount );
            Assert.AreEqual( 0.0, stats.UnmatchedShare );
            CollectionAssert.AreEqual( new[] { 0.0, 0.0, 0.0 }, stats.BiomeShare );
        }
    }
}
