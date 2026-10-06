using System.Collections.Generic;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    // Climate niches and own patches of resource rules (docs/WORLD_GEN_SYSTEM.md section 6).
    public class ResourceNicheTests {

        private const float Cell = 0.3675f;
        private const int Size = 80;

        private static readonly bool[] Liquid = { false };

        private static ResourceField Field( int seed = 1 ) {
            var terrain = new GeneratedTerrain( Size );
            for( int i = 0; i < terrain.Biome.Length; i++ ) {
                terrain.Biome[ i ] = 0;
            }
            return ResourceField.Build( terrain, seed, Liquid, 10 );
        }

        private static ResourceRule Warm() => new ResourceRule {
            Count = 1, Probability = 0.5f, SpawnRange = 0.3f, MaxRotation = 360,
            UseClimate = true, TemperatureFrom = 0.4f, TemperatureTo = 0.8f, HumidityFrom = -0.2f, HumidityTo = 0.2f,
        };

        private static int Count( ResourceRule rule, ResourceContext context, int cells = 20000 ) {
            var rules = new[] { rule };
            var result = new List<ResourcePlacement>();
            int total = 0;
            for( int i = 0; i < cells; i++ ) {
                ResourceRoller.Roll( rules, 1f, context, 9, 1, i % 130, i / 130, Cell, result );
                total += result.Count;
            }
            return total;
        }

        [ Test ]
        public void ClimateFit_IsFullInside_AndZeroAMarginOutside( ) {
            var rule = Warm();
            Assert.AreEqual( 1f, ResourceRoller.ClimateFit( rule, new ResourceContext { Temperature = 0.6f, Humidity = 0f } ), 1e-5f );
            Assert.AreEqual( 0.5f, ResourceRoller.ClimateFit( rule, new ResourceContext { Temperature = 0.4f - ResourceRoller.ClimateMargin / 2f, Humidity = 0f } ), 1e-5f );
            Assert.AreEqual( 0f, ResourceRoller.ClimateFit( rule, new ResourceContext { Temperature = -0.5f, Humidity = 0f } ), 1e-5f );
            Assert.AreEqual( 0f, ResourceRoller.ClimateFit( rule, new ResourceContext { Temperature = 0.6f, Humidity = 0.9f } ), 1e-5f );
        }

        [ Test ]
        public void ClimateRule_GrowsOnlyWhereItFits( ) {
            var rule = Warm();
            Assert.AreEqual( 0.5, Count( rule, new ResourceContext { Temperature = 0.6f, Humidity = 0f } ) / 20000.0, 0.02 );
            Assert.AreEqual( 0.25, Count( rule, new ResourceContext { Temperature = 0.4f - ResourceRoller.ClimateMargin / 2f, Humidity = 0f } ) / 20000.0, 0.02 );
            Assert.AreEqual( 0, Count( rule, new ResourceContext { Temperature = -0.8f, Humidity = 0f } ) );
        }

        [ Test ]
        public void WithoutUseClimate_TheClimateIsIgnored( ) {
            var rule = Warm();
            rule.UseClimate = false;
            Assert.AreEqual( 0.5, Count( rule, new ResourceContext { Temperature = -0.9f, Humidity = 0.9f } ) / 20000.0, 0.02 );
        }

        [ Test ]
        public void PatchFactor_IsOneInsideThePatchesAndZeroOutside( ) {
            var rule = new ResourceRule { PatchScale = 8f, PatchCoverage = 0.4f };
            Assert.AreEqual( 1f, ResourceRoller.PatchFactor( rule, 0.95f ), 1e-5f );
            Assert.AreEqual( 0f, ResourceRoller.PatchFactor( rule, 0.3f ), 1e-5f );
            Assert.AreEqual( 0.5f, ResourceRoller.PatchFactor( rule, 0.6f ), 1e-5f );
            rule.PatchCoverage = 1f;
            Assert.AreEqual( 1f, ResourceRoller.PatchFactor( rule, 0f ), 1e-5f );
        }

        [ Test ]
        public void PatchField_IsSpreadEvenly_AndOneRuleDiffersFromAnother( ) {
            var field = Field();
            var rule = new ResourceRule { PatchScale = 6f, PatchCoverage = 0.4f };
            int above = 0, differs = 0;
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    float a = field.PatchAt( 1, 0, rule, x, z );
                    Assert.That( a, Is.InRange( 0f, 1f ) );
                    if( a > 0.6f ) {
                        above++;
                    }
                    if( System.Math.Abs( a - field.PatchAt( 1, 1, rule, x, z ) ) > 0.2f ) {
                        differs++;
                    }
                }
            }
            Assert.AreEqual( 0.4, above / ( double ) ( Size * Size ), 0.02 );
            Assert.Greater( differs, Size * Size / 10, "two rules share a patch field" );
        }

        [ Test ]
        public void Patches_GatherTheObjectsIntoClumps( ) {
            var field = Field();
            var plain = new ResourceRule { Count = 1, Probability = 0.2f, SpawnRange = 0.3f, MaxRotation = 360 };
            var patchy = plain;
            patchy.PatchScale = 6f;
            patchy.PatchCoverage = 0.4f;
            patchy.Probability = 0.5f;

            // The same amount of objects, but in patchy ones the neighbours of an object are far more often objects too.
            double plainShare = NeighbourShare( field, plain, out int plainCount );
            double patchyShare = NeighbourShare( field, patchy, out int patchyCount );
            Assert.AreEqual( plainCount, patchyCount, plainCount * 0.15 );
            Assert.Greater( patchyShare, plainShare * 1.5 );
        }

        // Share of the objects' neighbouring cells (4-neighbourhood) that hold an object as well.
        private static double NeighbourShare( ResourceField field, ResourceRule rule, out int objects ) {
            var rules = new[] { rule };
            var result = new List<ResourcePlacement>();
            var has = new bool[ Size, Size ];
            objects = 0;
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 5, 1, x, z, Cell, result, field );
                    has[ x, z ] = result.Count > 0;
                    objects += result.Count;
                }
            }
            int pairs = 0, hits = 0;
            for( int z = 1; z < Size - 1; z++ ) {
                for( int x = 1; x < Size - 1; x++ ) {
                    if( !has[ x, z ] ) {
                        continue;
                    }
                    foreach( var ( dx, dz ) in new[] { ( 1, 0 ), ( -1, 0 ), ( 0, 1 ), ( 0, -1 ) } ) {
                        pairs++;
                        if( has[ x + dx, z + dz ] ) {
                            hits++;
                        }
                    }
                }
            }
            return pairs == 0 ? 0 : hits / ( double ) pairs;
        }

        [ Test ]
        public void Patches_AreDeterministic( ) {
            var rule = new ResourceRule { Count = 1, Probability = 0.4f, SpawnRange = 0.3f, MaxRotation = 360, PatchScale = 7f, PatchCoverage = 0.5f };
            var rules = new[] { rule };
            var a = new List<ResourcePlacement>();
            var b = new List<ResourcePlacement>();
            var fieldA = Field( 3 );
            var fieldB = Field( 3 );
            for( int i = 0; i < 2000; i++ ) {
                ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 3, 1, i % Size, i / Size, Cell, a, fieldA );
                ResourceRoller.Roll( rules, 1f, ResourceContext.Neutral, 3, 1, i % Size, i / Size, Cell, b, fieldB );
                Assert.AreEqual( a.Count, b.Count );
            }
        }
    }
}
