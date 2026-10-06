using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class PoiPlacerTests {

        private const float Cell = 0.3675f;
        private const int Size = 65;

        // Biome 0: land in the west, biome 1: land in the middle, biome 2: water in the east.
        // A separate little island of biome 0 lies in the water and cannot be reached on foot.
        private static readonly bool[] Liquid = { false, false, true };

        private static ResourceField Field( int seed = 1 ) {
            var terrain = new GeneratedTerrain( Size );
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    short biome = x < 25 ? ( short ) 0 : ( x < 45 ? ( short ) 1 : ( short ) 2 );
                    if( x >= 52 && x <= 62 && z >= 20 && z <= 40 ) {
                        biome = 0; // the island
                    }
                    terrain.Biome[ terrain.Index( x, z ) ] = biome;
                }
            }
            return ResourceField.Build( terrain, seed, Liquid, 10 );
        }

        private static PoiRule Camp( int count = 3, int[] biomes = null ) {
            return new PoiRule { MinCount = count, MaxCount = count, ClearRadius = 3f, Biomes = biomes, MinStartDistance = 8f, MinSpacing = 12f };
        }

        [ Test ]
        public void Sites_FollowTheirRules( ) {
            var field = Field();
            var rules = new[] { Camp( 3, new[] { 1 } ) };
            var sites = PoiPlacer.Place( field, rules, 5 );
            Assert.AreEqual( 3, sites.Count );
            foreach( var site in sites ) {
                Assert.AreEqual( 1, field.BiomeAt( site.CellX, site.CellY ), "biome" );
                Assert.GreaterOrEqual( site.StartDistance, 8f, "start distance" );
                Assert.That( site.RotationY, Is.InRange( 0f, 360f ) );
                Assert.IsTrue( field.IsReserved( site.CellX, site.CellY ), "the site is reserved" );
            }
            for( int a = 0; a < sites.Count; a++ ) {
                for( int b = a + 1; b < sites.Count; b++ ) {
                    float dx = sites[ a ].CellX - sites[ b ].CellX, dz = sites[ a ].CellY - sites[ b ].CellY;
                    Assert.GreaterOrEqual( dx * dx + dz * dz, 12f * 12f, "spacing" );
                }
            }
        }

        [ Test ]
        public void EverySiteIsOnWalkableGround_ConnectedToTheStart( ) {
            var field = Field();
            var rules = new[] { Camp( 40 ) };
            var sites = PoiPlacer.Place( field, rules, 8 );
            Assert.Greater( sites.Count, 3 );
            var reachable = GridFlood.ReachableFrom( Size, field.StartX, field.StartZ, ( x, z ) => field.IsGround( x, z ) );
            foreach( var site in sites ) {
                for( int z = site.CellY - 3; z <= site.CellY + 3; z++ ) {
                    for( int x = site.CellX - 3; x <= site.CellX + 3; x++ ) {
                        if( ( x - site.CellX ) * ( x - site.CellX ) + ( z - site.CellY ) * ( z - site.CellY ) > 9 ) {
                            continue;
                        }
                        Assert.IsTrue( field.IsGround( x, z ), $"cell {x},{z} of the site at {site.CellX},{site.CellY} is not ground" );
                        Assert.IsTrue( reachable[ z * Size + x ], $"cell {x},{z} cannot be reached from the start" );
                    }
                }
                Assert.IsFalse( site.CellX >= 52, "nothing on the island in the water" );
            }
        }

        [ Test ]
        public void Count_IsBetweenMinAndMax_AndTheSeedDecidesIt( ) {
            var counts = new HashSet<int>();
            for( int seed = 1; seed <= 20; seed++ ) {
                var rule = Camp( 2 );
                rule.MaxCount = 5;
                int count = PoiPlacer.Place( Field(), new[] { rule }, seed ).Count;
                Assert.That( count, Is.InRange( 2, 5 ) );
                counts.Add( count );
            }
            Assert.Greater( counts.Count, 1, "different seeds give different numbers of sites" );
        }

        [ Test ]
        public void SameSeedGivesTheSameSites_AnotherSeedDoesNot( ) {
            var rules = new[] { Camp( 4 ) };
            string Describe( int seed ) => string.Join( ";", PoiPlacer.Place( Field(), rules, seed ).Select( s => $"{s.Type},{s.CellX},{s.CellY},{s.RotationY:F2}" ) );
            Assert.AreEqual( Describe( 3 ), Describe( 3 ) );
            Assert.AreNotEqual( Describe( 3 ), Describe( 4 ) );
        }

        [ Test ]
        public void Sites_KeepResourcesAway( ) {
            var field = Field();
            var sites = PoiPlacer.Place( field, new[] { Camp( 3 ) }, 2 );
            var trees = new ResourceRule { Count = 3, Probability = 1f, SpawnRange = 0.3f, MaxRotation = 360 };
            var planner = new ResourcePlanner( field, new IReadOnlyList<ResourceRule>[] { new[] { trees }, new[] { trees }, new ResourceRule[ 0 ] }, 2, Cell, 13, 0 );
            var items = planner.PlanMap().Values.SelectMany( l => l ).ToList();
            Assert.Greater( items.Count, 500 );
            foreach( var site in sites ) {
                Assert.IsFalse( items.Any( i => ( System.Math.Floor( i.X + 0.5 ) - site.CellX ) * ( System.Math.Floor( i.X + 0.5 ) - site.CellX ) + ( System.Math.Floor( i.Y + 0.5 ) - site.CellY ) * ( System.Math.Floor( i.Y + 0.5 ) - site.CellY ) <= 9 ), "a tree lands inside a camp, even one rolled by a cell outside it" );
            }
        }

        [ Test ]
        public void SiteDiscs_DoNotOverlap_AcrossKindsOfSites( ) {
            var field = Field();
            var rules = new[] { Camp( 6 ), new PoiRule { MinCount = 6, MaxCount = 6, ClearRadius = 4f, MinSpacing = 5f } };
            var sites = PoiPlacer.Place( field, rules, 6 );
            Assert.Greater( sites.Count( s => s.Type == 0 ) + sites.Count( s => s.Type == 1 ), 6 );
            for( int a = 0; a < sites.Count; a++ ) {
                for( int b = a + 1; b < sites.Count; b++ ) {
                    float dx = sites[ a ].CellX - sites[ b ].CellX, dz = sites[ a ].CellY - sites[ b ].CellY;
                    float radii = rules[ sites[ a ].Type ].ClearRadius + rules[ sites[ b ].Type ].ClearRadius;
                    Assert.GreaterOrEqual( dx * dx + dz * dz, 1f, "two sites on the same cell" );
                    Assert.GreaterOrEqual( System.Math.Sqrt( dx * dx + dz * dz ), System.Math.Min( 5f, radii ) - 1e-3, "sites too close" );
                }
            }
        }

        [ Test ]
        public void StartDistanceLimits_AreRespected( ) {
            var field = Field();
            var rule = Camp( 20 );
            rule.MinStartDistance = 10f;
            rule.MaxStartDistance = 20f;
            var sites = PoiPlacer.Place( field, new[] { rule }, 4 );
            Assert.Greater( sites.Count, 0 );
            Assert.That( sites.Select( s => s.StartDistance ), Has.All.InRange( 10f, 20f ) );
        }

        [ Test ]
        public void WaterCondition_DecidesByTheVicinityOfWater( ) {
            var field = Field();
            var rule = Camp( 20 );
            rule.ClearRadius = 1f; // near water means within 2 cells of it, and the whole clear disc must be ground
            rule.Water = WaterCondition.NearWater;
            var near = PoiPlacer.Place( field, new[] { rule }, 4 );
            Assert.Greater( near.Count, 0 );
            Assert.That( near, Has.All.Matches<PoiPlacement>( s => s.CellX >= 40 ), "only the east coast is near the water" );

            var away = Camp( 20 );
            away.Water = WaterCondition.AwayFromWater;
            var inland = PoiPlacer.Place( Field(), new[] { away }, 4 );
            Assert.That( inland, Has.All.Matches<PoiPlacement>( s => s.CellX < 44 ) );
        }

        [ Test ]
        public void ImpossibleRules_AndNoRules_PlaceNothing( ) {
            var giant = Camp( 3 );
            giant.ClearRadius = 200f;
            Assert.IsEmpty( PoiPlacer.Place( Field(), new[] { giant }, 1 ) );
            Assert.IsEmpty( PoiPlacer.Place( Field(), new PoiRule[ 0 ], 1 ) );
            Assert.IsEmpty( PoiPlacer.Place( Field(), null, 1 ) );
            var none = Camp( 0 );
            none.MaxCount = 0;
            Assert.IsEmpty( PoiPlacer.Place( Field(), new[] { none }, 1 ) );
        }
    }

    public class AnchoredPoiTests {

        private const int Size = 65;
        private static readonly bool[] Liquid = { false, false, true };

        // Land for x < 45, water for x >= 45, apart from a little island at x 52..62 that cannot be reached on foot.
        private static ResourceField Field( int seed = 1 ) {
            var terrain = new GeneratedTerrain( Size );
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    short biome = x < 45 ? ( short ) 0 : ( short ) 2;
                    if( x >= 52 && x <= 62 && z >= 20 && z <= 40 ) {
                        biome = 0;
                    }
                    terrain.Biome[ terrain.Index( x, z ) ] = biome;
                }
            }
            return ResourceField.Build( terrain, seed, Liquid, 10 );
        }

        private static PoiRule Harbour( int x, int z, float rotation = 0f, float clearRadius = 4f ) {
            return new PoiRule { Anchored = true, AnchorX = x, AnchorZ = z, AnchorRotation = rotation, ClearRadius = clearRadius };
        }

        [ Test ]
        public void AnchoredSite_StandsAtTheOffsetFromTheStart_WithTheRotation( ) {
            for( int seed = 1; seed <= 5; seed++ ) {
                var field = Field( seed );
                var sites = PoiPlacer.Place( field, new[] { Harbour( 10, 3, 90f ) }, seed );
                Assert.AreEqual( 1, sites.Count );
                Assert.AreEqual( field.StartX + 10, sites[ 0 ].CellX );
                Assert.AreEqual( field.StartZ + 3, sites[ 0 ].CellY );
                Assert.AreEqual( 90f, sites[ 0 ].RotationY );
                Assert.IsTrue( field.IsReserved( sites[ 0 ].CellX, sites[ 0 ].CellY ) );
            }
        }

        [ Test ]
        public void AnchoredSite_OnWaterOrOnAnUnreachableIsland_IsNotPlaced( ) {
            var field = Field();
            Assert.IsEmpty( PoiPlacer.Place( field, new[] { Harbour( 47 - field.StartX, 0 ) }, 1 ), "water" );
            Assert.IsEmpty( PoiPlacer.Place( field, new[] { Harbour( 55 - field.StartX, 32 - field.StartZ ) }, 1 ), "an island the start cannot walk to" );
            Assert.IsEmpty( PoiPlacer.Place( field, new[] { Harbour( -100, 0 ) }, 1 ), "outside the map" );
        }

        [ Test ]
        public void RandomSites_KeepAwayFromTheAnchoredOne( ) {
            var field = Field();
            var camp = new PoiRule { MinCount = 20, MaxCount = 20, ClearRadius = 2f, MinSpacing = 3f };
            var sites = PoiPlacer.Place( field, new[] { camp, Harbour( 15, 0, 0f, 6f ) }, 3 );
            var harbour = sites.Single( s => s.Type == 1 );
            Assert.Greater( sites.Count, 2 );
            foreach( var site in sites.Where( s => s.Type == 0 ) ) {
                float dx = site.CellX - harbour.CellX, dz = site.CellY - harbour.CellY;
                Assert.Greater( System.Math.Sqrt( dx * dx + dz * dz ), 6f, "a camp inside the reserved harbour" );
            }
        }
    }

    public class PoiPathTests {

        private const int Size = 24;

        private static GeneratedTerrain Terrain( ) {
            var terrain = new GeneratedTerrain( Size );
            for( int i = 0; i < terrain.Biome.Length; i++ ) {
                terrain.Biome[ i ] = 0;
            }
            return terrain;
        }

        private static (PoiPlacement[] sites, PoiRule[] rules) Site( float rotation, params PoiPathSegment[] paths ) {
            return ( new[] { new PoiPlacement { Type = 0, CellX = 10, CellY = 10, RotationY = rotation } }, new[] { new PoiRule { Paths = paths } } );
        }

        private static bool Painted( GeneratedTerrain terrain, int x, int z ) => terrain.Patch[ terrain.Index( x, z ) ] != 0;

        [ Test ]
        public void Stripe_PaintsTheCellsNearIt_WithTheLookPlusOne( ) {
            var terrain = Terrain();
            var ( sites, rules ) = Site( 0f, new PoiPathSegment { FromX = 0, FromZ = 0, ToX = 4, ToZ = 0, HalfWidth = 1f, Look = 3 } );
            PoiLayout.PaintPaths( terrain, sites, rules, ( x, z ) => true );
            for( int x = 10; x <= 14; x++ ) {
                Assert.AreEqual( 4, terrain.Patch[ terrain.Index( x, 10 ) ], $"cell {x},10" );
                Assert.IsTrue( Painted( terrain, x, 9 ) && Painted( terrain, x, 11 ), $"width at {x}" );
                Assert.IsFalse( Painted( terrain, x, 12 ), $"too far at {x}" );
            }
            Assert.IsFalse( Painted( terrain, 16, 10 ), "past the end" );
        }

        [ Test ]
        public void Stripe_TurnsWithTheSite( ) {
            var terrain = Terrain();
            var ( sites, rules ) = Site( 90f, new PoiPathSegment { FromX = 0, FromZ = 0, ToX = 4, ToZ = 0, HalfWidth = 0.5f, Look = 1 } );
            PoiLayout.PaintPaths( terrain, sites, rules, ( x, z ) => true );
            Assert.IsTrue( Painted( terrain, 10, 7 ), "turned toward -z like a piece offset of (4, 0)" );
            Assert.IsFalse( Painted( terrain, 14, 10 ), "no longer along +x" );
        }

        [ Test ]
        public void Disc_PaintsAPlaza_AndWaterIsLeftAlone( ) {
            var terrain = Terrain();
            var ( sites, rules ) = Site( 0f, new PoiPathSegment { FromX = 0, FromZ = 0, ToX = 0, ToZ = 0, HalfWidth = 2f, Look = 1 } );
            int painted = PoiLayout.PaintPaths( terrain, sites, rules, ( x, z ) => x != 11 );
            Assert.IsTrue( Painted( terrain, 10, 10 ) && Painted( terrain, 9, 10 ) );
            Assert.IsFalse( Painted( terrain, 11, 10 ), "not ground" );
            Assert.IsFalse( Painted( terrain, 13, 13 ), "outside the disc" );
            Assert.Greater( painted, 8 );
        }

        [ Test ]
        public void SitesWithoutPaths_AndSegmentsOffTheMap_AreHarmless( ) {
            var terrain = Terrain();
            var ( sites, rules ) = Site( 0f );
            Assert.AreEqual( 0, PoiLayout.PaintPaths( terrain, sites, rules, ( x, z ) => true ) );
            ( sites, rules ) = Site( 0f, new PoiPathSegment { FromX = -50, FromZ = -50, ToX = 80, ToZ = 80, HalfWidth = 1f, Look = 1 } );
            Assert.DoesNotThrow( ( ) => PoiLayout.PaintPaths( terrain, sites, rules, ( x, z ) => true ) );
        }
    }

    public class LootRollerTests {

        private static readonly LootEntry[] Entries = {
            new LootEntry { Item = "Stick", Min = 2, Max = 4, Weight = 3f, MinTier = 0 },
            new LootEntry { Item = "Herb", Min = 1, Max = 2, Weight = 2f, MinTier = 0 },
            new LootEntry { Item = "Iron", Min = 1, Max = 2, Weight = 1f, MinTier = 1 },
            new LootEntry { Item = "Gold", Min = 1, Max = 1, Weight = 1f, MinTier = 2 },
        };

        private static List<LootStack> Roll( int tier, int seed, int x, int y, int min = 3, int max = 5 ) {
            var result = new List<LootStack>();
            LootRoller.Roll( Entries, min, max, tier, seed, x, y, result );
            return result;
        }

        [ Test ]
        public void TierFor_GrowsWithTheDistance_UpToTheMaximum( ) {
            Assert.AreEqual( 0, LootRoller.TierFor( 0f, 30f, 2 ) );
            Assert.AreEqual( 0, LootRoller.TierFor( 29.9f, 30f, 2 ) );
            Assert.AreEqual( 1, LootRoller.TierFor( 30f, 30f, 2 ) );
            Assert.AreEqual( 2, LootRoller.TierFor( 75f, 30f, 2 ) );
            Assert.AreEqual( 2, LootRoller.TierFor( 500f, 30f, 2 ) );
            Assert.AreEqual( 0, LootRoller.TierFor( 500f, 0f, 2 ) );
        }

        [ Test ]
        public void SameArguments_GiveTheSameLoot_OtherCellsDiffer( ) {
            CollectionAssert.AreEqual( Roll( 1, 5, 10, 20 ).Select( s => $"{s.Item}x{s.Count}" ), Roll( 1, 5, 10, 20 ).Select( s => $"{s.Item}x{s.Count}" ) );
            var layouts = Enumerable.Range( 0, 40 ).Select( i => string.Join( ",", Roll( 1, 5, i, 3 ).Select( s => $"{s.Item}x{s.Count}" ) ) ).Distinct().Count();
            Assert.Greater( layouts, 10 );
        }

        [ Test ]
        public void BetterItems_OnlyDropFromTheirTierUp( ) {
            for( int i = 0; i < 300; i++ ) {
                Assert.IsFalse( Roll( 0, 1, i, 0 ).Any( s => s.Item == "Iron" || s.Item == "Gold" ), "tier 0 holds only the basics" );
                Assert.IsFalse( Roll( 1, 1, i, 0 ).Any( s => s.Item == "Gold" ), "tier 1 has no gold yet" );
            }
            Assert.IsTrue( Enumerable.Range( 0, 300 ).Any( i => Roll( 1, 1, i, 0 ).Any( s => s.Item == "Iron" ) ) );
            Assert.IsTrue( Enumerable.Range( 0, 300 ).Any( i => Roll( 2, 1, i, 0 ).Any( s => s.Item == "Gold" ) ) );
        }

        [ Test ]
        public void Stacks_AreMerged_AndCountsStayInTheirRanges( ) {
            for( int i = 0; i < 200; i++ ) {
                var loot = Roll( 2, 9, i, i, 3, 5 );
                Assert.AreEqual( loot.Count, loot.Select( s => s.Item ).Distinct().Count(), "one stack per item" );
                int total = loot.Sum( s => s.Count );
                Assert.GreaterOrEqual( total, 3, "at least one unit per roll" );
                Assert.LessOrEqual( total, 5 * 4, "no roll gives more than its maximum" );
                Assert.IsTrue( loot.All( s => s.Count >= 1 ) );
            }
        }

        [ Test ]
        public void Weights_DecideHowOftenAnItemDrops( ) {
            int sticks = 0, herbs = 0;
            for( int i = 0; i < 3000; i++ ) {
                var loot = new List<LootStack>();
                LootRoller.Roll( Entries.Take( 2 ).ToArray(), 1, 1, 0, 3, i, 0, loot );
                sticks += loot.Count( s => s.Item == "Stick" );
                herbs += loot.Count( s => s.Item == "Herb" );
            }
            Assert.AreEqual( 0.6, sticks / 3000.0, 0.04 );
            Assert.AreEqual( 0.4, herbs / 3000.0, 0.04 );
        }

        [ Test ]
        public void EmptyOrImpossibleTables_GiveNothing( ) {
            var result = new List<LootStack> { new LootStack { Item = "old", Count = 1 } };
            LootRoller.Roll( null, 1, 2, 0, 1, 0, 0, result );
            Assert.IsEmpty( result );
            LootRoller.Roll( new[] { new LootEntry { Item = "Gold", Min = 1, Max = 1, Weight = 1f, MinTier = 5 } }, 3, 3, 0, 1, 0, 0, result );
            Assert.IsEmpty( result, "nothing is allowed at this tier" );
            LootRoller.Roll( new[] { new LootEntry { Item = "Dust", Min = 1, Max = 1, Weight = 0f } }, 3, 3, 0, 1, 0, 0, result );
            Assert.IsEmpty( result, "zero weight never drops" );
        }
    }
}
