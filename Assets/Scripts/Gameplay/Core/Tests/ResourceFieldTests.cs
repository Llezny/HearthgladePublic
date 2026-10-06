using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ResourceRuleContextTests {

        private const float Cell = 0.3675f;

        private static int Count( ResourceRule rule, ResourceContext context, float weight = 1f, int cells = 20000 ) {
            var rules = new[] { rule };
            var result = new List<ResourcePlacement>( );
            int total = 0;
            for( int i = 0; i < cells; i++ ) {
                ResourceRoller.Roll( rules, weight, context, 9, 1, i % 130, i / 130, Cell, result );
                total += result.Count;
            }
            return total;
        }

        private static ResourceRule Plain( float probability ) {
            return new ResourceRule { Count = 1, Probability = probability, SpawnRange = 0.3f, MaxRotation = 360 };
        }

        [ Test ]
        public void DensityRamp_GrowsOnlyWhereTheDensityIsHigh( ) {
            var grove = Plain( 0.5f );
            grove.DensityFrom = 0.5f;
            grove.DensityTo = 1f;
            Assert.AreEqual( 0, Count( grove, new ResourceContext { Density = 0.3f, StartDistance = 1e9f } ) );
            Assert.AreEqual( 0, Count( grove, new ResourceContext { Density = 0.5f, StartDistance = 1e9f } ) );
            Assert.AreEqual( 0.5, Count( grove, new ResourceContext { Density = 1f, StartDistance = 1e9f } ) / 20000.0, 0.02 );
            Assert.Greater( Count( grove, new ResourceContext { Density = 0.9f, StartDistance = 1e9f } ),
                Count( grove, new ResourceContext { Density = 0.6f, StartDistance = 1e9f } ) );
        }

        [ Test ]
        public void InvertedDensityRamp_GrowsInClearings( ) {
            var flowers = Plain( 0.4f );
            flowers.DensityFrom = 0.5f;
            flowers.DensityTo = 0.1f;
            Assert.AreEqual( 0, Count( flowers, new ResourceContext { Density = 0.9f, StartDistance = 1e9f } ) );
            Assert.AreEqual( 0.4, Count( flowers, new ResourceContext { Density = 0f, StartDistance = 1e9f } ) / 20000.0, 0.02 );
        }

        [ Test ]
        public void EqualDensityBounds_IgnoreDensity( ) {
            var rule = Plain( 0.3f );
            foreach( var density in new[] { 0f, 0.5f, 1f } ) {
                Assert.AreEqual( 0.3, Count( rule, new ResourceContext { Density = density, StartDistance = 1e9f } ) / 20000.0, 0.02 );
            }
        }

        [ Test ]
        public void WaterCondition_DecidesByTheVicinityOfWater( ) {
            var reeds = Plain( 1f );
            reeds.Water = WaterCondition.NearWater;
            var inland = Plain( 1f );
            inland.Water = WaterCondition.AwayFromWater;
            var near = new ResourceContext { NearWater = true, StartDistance = 1e9f };
            var far = new ResourceContext { NearWater = false, StartDistance = 1e9f };
            Assert.AreEqual( 20000, Count( reeds, near ) );
            Assert.AreEqual( 0, Count( reeds, far ) );
            Assert.AreEqual( 0, Count( inland, near ) );
            Assert.AreEqual( 20000, Count( inland, far ) );
        }

        [ Test ]
        public void MinStartDistance_IsAHardStepOrARamp( ) {
            var step = Plain( 1f );
            step.MinStartDistance = 30f;
            Assert.AreEqual( 0, Count( step, new ResourceContext { StartDistance = 29f } ) );
            Assert.AreEqual( 20000, Count( step, new ResourceContext { StartDistance = 30f } ) );

            var ramp = step;
            ramp.StartDistanceRamp = 20f;
            Assert.AreEqual( 0, Count( ramp, new ResourceContext { StartDistance = 30f } ) );
            Assert.AreEqual( 0.5, Count( ramp, new ResourceContext { StartDistance = 40f } ) / 20000.0, 0.02 );
            Assert.AreEqual( 20000, Count( ramp, new ResourceContext { StartDistance = 50f } ) );
        }

        [ Test ]
        public void Weight_ThinsTheTableOutProportionally( ) {
            var rule = Plain( 0.6f );
            var context = ResourceContext.Neutral;
            Assert.AreEqual( 0.6, Count( rule, context, 1f ) / 20000.0, 0.02 );
            Assert.AreEqual( 0.3, Count( rule, context, 0.5f ) / 20000.0, 0.02 );
            Assert.AreEqual( 0, Count( rule, context, 0f ) );
        }

        [ Test ]
        public void Tables_UseIndependentRandomStreams( ) {
            var rules = new[] { Plain( 0.5f ) };
            var a = new List<ResourcePlacement>( );
            var b = new List<ResourcePlacement>( );
            var different = 0;
            for( int i = 0; i < 300; i++ ) {
                ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 3, 1, i, 7, Cell, a );
                ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 3, 2, i, 7, Cell, b );
                if( a.Count != b.Count ) {
                    different++;
                }
            }
            Assert.Greater( different, 30, "two biomes must not roll the same trees in the same cells" );
        }
    }

    public class ResourceDepositTests {

        private const float Cell = 0.3675f;

        private static ResourceRule Vein( int spacing, float probability, int min, int max, float radius ) {
            return new ResourceRule {
                Count = 1, Probability = probability, MaxRotation = 360,
                ClusterSpacing = spacing, ClusterMin = min, ClusterMax = max, ClusterRadius = radius
            };
        }

        private static List<(int x, int y, ResourcePlacement placement)> Area( ResourceRule rule, ResourceContext context, int seed, int size, float weight = 1f ) {
            var rules = new[] { rule };
            var result = new List<ResourcePlacement>( );
            var found = new List<(int, int, ResourcePlacement)>( );
            for( int y = 0; y < size; y++ ) {
                for( int x = 0; x < size; x++ ) {
                    ResourceRoller.Roll( rules, weight, context, seed, 1, x, y, Cell, result );
                    foreach( var placement in result ) {
                        found.Add( ( x, y, placement ) );
                    }
                }
            }
            return found;
        }

        [ Test ]
        public void SameSeed_GivesTheSameDeposits( ) {
            var rule = Vein( 16, 1f, 3, 6, 2f );
            var a = Area( rule, ResourceContext.Neutral, 5, 64 );
            var b = Area( rule, ResourceContext.Neutral, 5, 64 );
            CollectionAssert.AreEqual( a, b );
            var otherSeed = Area( rule, ResourceContext.Neutral, 6, 64 );
            Assert.IsFalse( otherSeed.Select( p => ( p.x, p.y ) ).SequenceEqual( a.Select( p => ( p.x, p.y ) ) ) );
        }

        [ Test ]
        public void CertainDeposit_HasTheConfiguredNumberOfObjects( ) {
            // Radius 0: every object of a deposit lands on the centre cell, so the area holds min..max per square.
            var rule = Vein( 20, 1f, 4, 4, 0f );
            var items = Area( rule, ResourceContext.Neutral, 11, 80 );
            Assert.AreEqual( 16 * 4, items.Count, "80x80 cells = 16 squares of 20x20, each with one deposit of 4" );
        }

        [ Test ]
        public void DepositSize_StaysInTheConfiguredRange( ) {
            var rule = Vein( 20, 1f, 3, 6, 0f );
            var perCell = Area( rule, ResourceContext.Neutral, 2, 200 ).GroupBy( p => ( p.x, p.y ) ).Select( g => g.Count( ) ).ToList( );
            Assert.AreEqual( 100, perCell.Count, "one centre cell per square of 20x20" );
            Assert.That( perCell, Has.All.InRange( 3, 6 ) );
            Assert.Greater( perCell.Distinct( ).Count( ), 1, "sizes vary" );
        }

        [ Test ]
        public void DepositObjects_ClusterAroundOneCentre( ) {
            // Five objects within 1.5 cells of a centre: every object has a neighbour a few cells away.
            var rule = Vein( 24, 1f, 5, 5, 1.5f );
            var items = Area( rule, ResourceContext.Neutral, 4, 96 );
            var inner = items.Where( p => p.x >= 5 && p.x < 91 && p.y >= 5 && p.y < 91 ).ToList( );
            Assert.Greater( inner.Count, 0 );
            foreach( var item in inner ) {
                bool hasNeighbour = items.Any( o => !o.placement.Equals( item.placement ) && ( o.x != item.x || o.y != item.y || !o.placement.Equals( item.placement ) )
                    && Math.Abs( o.x - item.x ) <= 3 && Math.Abs( o.y - item.y ) <= 3 );
                Assert.IsTrue( hasNeighbour, $"lonely object at {item.x},{item.y}" );
            }
        }

        [ Test ]
        public void Probability_IsTheShareOfSquaresWithADeposit( ) {
            var rule = Vein( 10, 0.3f, 1, 1, 0f );
            var items = Area( rule, ResourceContext.Neutral, 8, 400 );
            Assert.AreEqual( 0.3, items.Count / 1600.0, 0.05 );
        }

        [ Test ]
        public void Placements_LieInsideTheirCell( ) {
            var rule = Vein( 12, 1f, 6, 6, 3f );
            foreach( var item in Area( rule, ResourceContext.Neutral, 13, 60 ) ) {
                Assert.That( item.placement.OffsetX, Is.InRange( -0.5f * Cell - 1e-4f, 0.5f * Cell + 1e-4f ) );
                Assert.That( item.placement.OffsetZ, Is.InRange( -0.5f * Cell - 1e-4f, 0.5f * Cell + 1e-4f ) );
            }
        }

        [ Test ]
        public void ConditionsAndWeight_ThinDepositsOut( ) {
            var rule = Vein( 20, 1f, 4, 4, 0f );
            rule.MinStartDistance = 50f;
            Assert.AreEqual( 0, Area( rule, new ResourceContext { StartDistance = 10f }, 1, 80 ).Count );
            Assert.AreEqual( 64, Area( rule, new ResourceContext { StartDistance = 60f }, 1, 80 ).Count );
            var half = Area( rule, new ResourceContext { StartDistance = 60f }, 1, 80, 0.5f ).Count;
            Assert.That( half, Is.InRange( 15, 50 ), "half the weight keeps about half of the objects" );
        }

        [ Test ]
        public void NegativeCells_DoNotCrash( ) {
            var rules = new[] { Vein( 8, 1f, 3, 3, 2f ) };
            var result = new List<ResourcePlacement>( );
            for( int i = -30; i < 30; i++ ) {
                ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 1, 1, i, -i, Cell, result );
            }
        }
    }

    public class ResourceFieldTests {

        // 60x60: the left 30 columns are land (biome 0), the right 30 are water (biome 1).
        private static GeneratedTerrain HalfLandHalfWater( ) {
            var terrain = new GeneratedTerrain( 60 );
            for( int z = 0; z < 60; z++ ) {
                for( int x = 0; x < 60; x++ ) {
                    terrain.Biome[ terrain.Index( x, z ) ] = ( short ) ( x < 30 ? 0 : 1 );
                }
            }
            return terrain;
        }

        private static readonly bool[] Liquid = { false, true };

        [ Test ]
        public void Density_IsSpreadEvenlyOver01( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            var values = new List<float>( );
            for( int z = 0; z < 60; z++ ) {
                for( int x = 0; x < 60; x++ ) {
                    values.Add( field.ContextAt( x, z ).Density );
                }
            }
            Assert.That( values, Has.All.InRange( 0f, 1f ) );
            Assert.AreEqual( 0.5, values.Average( ), 0.01 );
            Assert.AreEqual( 0.5, values.Count( v => v >= 0.5f ) / ( double ) values.Count, 0.01 );
        }

        [ Test ]
        public void Density_IsDeterministicAndDependsOnTheSeed( ) {
            var terrain = HalfLandHalfWater( );
            var a = ResourceField.Build( terrain, 1, Liquid, 10 );
            var b = ResourceField.Build( terrain, 1, Liquid, 10 );
            var c = ResourceField.Build( terrain, 2, Liquid, 10 );
            bool differsFromC = false;
            for( int x = 0; x < 60; x++ ) {
                Assert.AreEqual( a.ContextAt( x, 5 ).Density, b.ContextAt( x, 5 ).Density );
                differsFromC |= a.ContextAt( x, 5 ).Density != c.ContextAt( x, 5 ).Density;
            }
            Assert.IsTrue( differsFromC );
        }

        [ Test ]
        public void NearWater_CoversTheCoastStripOnly( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            Assert.IsFalse( field.ContextAt( 10, 30 ).NearWater );
            Assert.IsFalse( field.ContextAt( 27, 30 ).NearWater );
            Assert.IsTrue( field.ContextAt( 28, 30 ).NearWater, "two cells from the first water column" );
            Assert.IsTrue( field.ContextAt( 29, 30 ).NearWater );
            Assert.IsTrue( field.ContextAt( 40, 30 ).NearWater );
        }

        [ Test ]
        public void Start_IsOnLandAndDistanceIsMeasuredFromIt( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            Assert.Less( field.StartX, 30 );
            Assert.AreEqual( 0f, field.ContextAt( field.StartX, field.StartZ ).StartDistance, 1e-4 );
            Assert.AreEqual( 5f, field.ContextAt( field.StartX + 3, field.StartZ + 4 ).StartDistance, 1e-4 );
        }

        [ Test ]
        public void Mix_InsideABiomeIsThatBiomeAlone( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            var mix = new List<BiomeShare>( );
            field.MixAt( 10, 30, mix );
            Assert.AreEqual( 1, mix.Count );
            Assert.AreEqual( 0, mix[ 0 ].Biome );
            Assert.AreEqual( 1f, mix[ 0 ].Weight, 1e-6 );
        }

        [ Test ]
        public void Mix_OnABorderSplitsTheWeightAndSumsToOne( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            var mix = new List<BiomeShare>( );
            foreach( var x in new[] { 28, 29, 30, 31 } ) {
                field.MixAt( x, 30, mix );
                Assert.AreEqual( 2, mix.Count, $"column {x}" );
                Assert.AreEqual( 1f, mix.Sum( s => s.Weight ), 1e-5 );
            }
            field.MixAt( 29, 30, mix );
            Assert.Greater( mix.First( s => s.Biome == 0 ).Weight, mix.First( s => s.Biome == 1 ).Weight );
            field.MixAt( 30, 30, mix );
            Assert.Greater( mix.First( s => s.Biome == 1 ).Weight, mix.First( s => s.Biome == 0 ).Weight );
        }

        [ Test ]
        public void Mix_AtTheMapEdgeDoesNotReadOutside( ) {
            var field = ResourceField.Build( HalfLandHalfWater( ), 1, Liquid, 10 );
            var mix = new List<BiomeShare>( );
            field.MixAt( 0, 0, mix );
            field.MixAt( 59, 59, mix );
            Assert.AreEqual( 1f, mix.Sum( s => s.Weight ), 1e-5 );
        }

        [ Test ]
        public void MapWithoutAnyStandableCell_StillBuilds( ) {
            var terrain = new GeneratedTerrain( 10 );
            for( int i = 0; i < terrain.Biome.Length; i++ ) {
                terrain.Biome[ i ] = 1;
            }
            var field = ResourceField.Build( terrain, 1, Liquid, 10 );
            Assert.AreEqual( 5, field.StartX );
        }
    }

    public class SceneObjectKeyTests {

        [ Test ]
        public void SameObject_GivesTheSameKey( ) {
            Assert.AreEqual(
                SceneObjectKey.Compute( "Tree", 1.5f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1f, 1f ),
                SceneObjectKey.Compute( "Tree", 1.5f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1f, 1f ) );
        }

        [ Test ]
        public void AnyDifference_ChangesTheKey( ) {
            int baseKey = SceneObjectKey.Compute( "Tree", 1.5f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1f, 1f );
            Assert.AreNotEqual( baseKey, SceneObjectKey.Compute( "Rock", 1.5f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1f, 1f ) );
            Assert.AreNotEqual( baseKey, SceneObjectKey.Compute( "Tree", 1.6f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1f, 1f ) );
            Assert.AreNotEqual( baseKey, SceneObjectKey.Compute( "Tree", 1.5f, 0f, -2.25f, 0f, 91f, 0f, 1f, 1f, 1f ) );
            Assert.AreNotEqual( baseKey, SceneObjectKey.Compute( "Tree", 1.5f, 0f, -2.25f, 0f, 90f, 0f, 1f, 1.1f, 1f ) );
        }

        [ Test ]
        public void NegativeZero_IsTheSamePositionAsZero( ) {
            Assert.AreEqual(
                SceneObjectKey.Compute( "Tree", 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f ),
                SceneObjectKey.Compute( "Tree", -0f, 0f, -0f, 0f, 0f, 0f, 1f, 1f, 1f ) );
        }

        [ Test ]
        public void Key_IsTheSameOnEveryRun( ) {
            // A fixed number: the key is written to saves and must match after the next launch (HashCode.Combine would not).
            Assert.AreEqual( -721628469, SceneObjectKey.Compute( "SpruceTree", 12.5f, 0f, 7.25f, 0f, 133f, 0f, 1f, 1f, 1f ) );
        }
    }
}
