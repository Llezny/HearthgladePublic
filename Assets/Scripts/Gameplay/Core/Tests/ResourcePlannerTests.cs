using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ResourcePlannerTests {

        private const float Cell = 0.3675f;
        private const int ChunkSize = 13;
        private const int Size = 65; // 5x5 chunks

        private static readonly bool[] Liquid = { false, true };

        // Biome 0 is land everywhere, except for the right part when withWater is set (biome 1 = water).
        private static ResourceField LandField( int seed = 1, bool withWater = false ) {
            var terrain = new GeneratedTerrain( Size );
            for( int z = 0; z < Size; z++ ) {
                for( int x = 0; x < Size; x++ ) {
                    terrain.Biome[ terrain.Index( x, z ) ] = ( short ) ( withWater && x >= 40 ? 1 : 0 );
                }
            }
            return ResourceField.Build( terrain, seed, Liquid, 10 );
        }

        private static ResourcePlanner Planner( ResourceField field, ResourceRule[] rules, int budget = 0, int seed = 5 ) {
            return new ResourcePlanner( field, new IReadOnlyList<ResourceRule>[] { rules, new ResourceRule[ 0 ] }, seed, Cell, ChunkSize, budget );
        }

        private static ResourceRule Trees( float probability, float spacing = 0f ) {
            return new ResourceRule { Count = 3, Probability = probability, SpawnRange = 0.35f, MaxRotation = 360, MinSpacing = spacing };
        }

        private static List<PlannedResource> All( ResourcePlanner planner ) {
            return planner.PlanMap( ).Values.SelectMany( list => list ).ToList( );
        }

        private static string Key( PlannedResource p ) => $"{p.GridX},{p.GridY},{p.Table},{p.RuleIndex},{p.X:F3},{p.Y:F3}";

        [ Test ]
        public void StartArea_IsKeptClear( ) {
            var field = LandField( );
            var items = All( Planner( field, new[] { Trees( 1f ) } ) );
            Assert.Greater( items.Count, 1000 );
            foreach( var item in items ) {
                double dx = item.GridX - field.StartX, dz = item.GridY - field.StartZ;
                Assert.Greater( dx * dx + dz * dz, ResourceField.StartClearRadius * ResourceField.StartClearRadius, $"object in the cell {item.GridX},{item.GridY} next to the start" );
            }
            Assert.IsTrue( items.Any( i => i.GridX == field.StartX + ResourceField.StartClearRadius + 1 || i.GridY == field.StartZ + ResourceField.StartClearRadius + 1 ), "the clearing is small" );
        }

        [ Test ]
        public void ReservedCells_StayEmpty( ) {
            var field = LandField( );
            field.Reserve( 30, 30, 5f );
            var items = All( Planner( field, new[] { Trees( 1f ) } ) );
            Assert.IsFalse( items.Any( i => ( i.GridX - 30 ) * ( i.GridX - 30 ) + ( i.GridY - 30 ) * ( i.GridY - 30 ) <= 25 ) );
            Assert.IsTrue( items.Any( i => i.GridX == 36 && i.GridY == 30 ), "just outside the disc things still grow" );
            Assert.IsTrue( field.IsReserved( 30, 30 ) && field.IsReserved( 34, 30 ) && !field.IsReserved( 36, 30 ) );
        }

        [ Test ]
        public void Water_HoldsNothing( ) {
            var items = All( Planner( LandField( withWater: true ), new[] { Trees( 1f ) } ) );
            Assert.IsFalse( items.Any( i => i.GridX >= 40 ) );
            Assert.IsTrue( items.Any( i => i.GridX == 39 ) );
        }

        [ Test ]
        public void MinSpacing_KeepsObjectsApart_AlsoAcrossChunkBorders( ) {
            var field = LandField( );
            const float spacing = 1.6f;
            var items = All( Planner( field, new[] { Trees( 0.6f, spacing ) } ) );
            Assert.Greater( items.Count, 300 );
            for( int a = 0; a < items.Count; a++ ) {
                for( int b = a + 1; b < items.Count; b++ ) {
                    float dx = items[ a ].X - items[ b ].X, dy = items[ a ].Y - items[ b ].Y;
                    Assert.GreaterOrEqual( dx * dx + dy * dy, spacing * spacing - 1e-4f, $"{Key( items[ a ] )} and {Key( items[ b ] )} are too close" );
                }
            }
        }

        [ Test ]
        public void WithoutSpacing_ObjectsDoOverlap_AndSpacingOnlyThinsThemOut( ) {
            var field = LandField( );
            var free = All( Planner( field, new[] { Trees( 0.6f ) } ) );
            var spaced = All( Planner( field, new[] { Trees( 0.6f, 1.6f ) } ) );
            bool overlap = false;
            foreach( var a in free.Take( 400 ) ) {
                overlap |= free.Any( b => !ReferenceEquals( a, b ) && ( a.X - b.X ) * ( a.X - b.X ) + ( a.Y - b.Y ) * ( a.Y - b.Y ) < 1.6f * 1.6f );
            }
            Assert.IsTrue( overlap, "the test would prove nothing if nothing overlapped" );
            Assert.Less( spaced.Count, free.Count );
            Assert.Greater( spaced.Count, free.Count / 10, "spacing thins the trees, it must not wipe the forest out" );
            var freeKeys = new HashSet<string>( free.Select( Key ) );
            Assert.IsTrue( spaced.All( s => freeKeys.Contains( Key( s ) ) ), "spacing only removes objects, it never moves them" );
        }

        [ Test ]
        public void SpacingOfOneRule_DoesNotAffectAnother( ) {
            var field = LandField( );
            var rules = new[] { Trees( 0.5f, 2f ), Trees( 0.5f ) };
            var items = All( Planner( field, rules ) );
            var plain = items.Where( i => i.RuleIndex == 1 ).ToList( );
            var unspaced = All( Planner( field, new[] { Trees( 0.5f ), Trees( 0.5f ) } ) ).Where( i => i.RuleIndex == 1 ).ToList( );
            CollectionAssert.AreEqual( unspaced.Select( Key ), plain.Select( Key ) );
        }

        [ Test ]
        public void Plan_DoesNotDependOnTheOrderOfChunks( ) {
            var field = LandField( );
            var rules = new[] { Trees( 0.5f, 1.5f ) };
            var whole = Planner( field, rules ).PlanMap( );

            var reversed = Planner( field, rules );
            var chunk = new List<PlannedResource>( );
            for( int cy = 4; cy >= 0; cy-- ) {
                for( int cx = 4; cx >= 0; cx-- ) {
                    reversed.PlanChunk( cx, cy, chunk );
                    var expected = whole.Where( kv => kv.Key % Size / ChunkSize == cx && kv.Key / Size / ChunkSize == cy ).SelectMany( kv => kv.Value ).Select( Key ).OrderBy( k => k );
                    CollectionAssert.AreEqual( expected, chunk.Select( Key ).OrderBy( k => k ), $"chunk {cx},{cy}" );
                }
            }
        }

        [ Test ]
        public void SameSeed_GivesTheSamePlan_AnotherSeedDoesNot( ) {
            var field = LandField( );
            var rules = new[] { Trees( 0.5f, 1.5f ) };
            var a = All( Planner( field, rules, seed: 9 ) ).Select( Key ).ToList( );
            var b = All( Planner( field, rules, seed: 9 ) ).Select( Key ).ToList( );
            var c = All( Planner( field, rules, seed: 10 ) ).Select( Key ).ToList( );
            CollectionAssert.AreEqual( a, b );
            CollectionAssert.AreNotEqual( a, c );
        }

        [ Test ]
        public void ChunkBudget_CapsEveryChunk_AndOnlyRemovesObjects( ) {
            var field = LandField( );
            var rules = new[] { Trees( 0.9f ) };
            var free = Planner( field, rules ).PlanMap( ).Values.SelectMany( l => l ).ToList( );
            var planner = Planner( field, rules, budget: 25 );
            var freeKeys = new HashSet<string>( free.Select( Key ) );
            var chunk = new List<PlannedResource>( );
            bool someChunkWasCut = false;
            for( int cy = 0; cy < planner.ChunksPerSide; cy++ ) {
                for( int cx = 0; cx < planner.ChunksPerSide; cx++ ) {
                    planner.PlanChunk( cx, cy, chunk );
                    Assert.LessOrEqual( chunk.Count, 25, $"chunk {cx},{cy}" );
                    Assert.IsTrue( chunk.All( c => freeKeys.Contains( Key( c ) ) ) );
                    someChunkWasCut |= chunk.Count == 25;
                }
            }
            Assert.IsTrue( someChunkWasCut, "the budget must actually bite in this test" );
        }

        [ Test ]
        public void ChunkBudget_Zero_MeansNoLimit( ) {
            var field = LandField( );
            var rules = new[] { Trees( 0.9f ) };
            var counts = new List<int>( );
            var planner = Planner( field, rules, budget: 0 );
            var chunk = new List<PlannedResource>( );
            for( int cy = 0; cy < planner.ChunksPerSide; cy++ ) {
                for( int cx = 0; cx < planner.ChunksPerSide; cx++ ) {
                    planner.PlanChunk( cx, cy, chunk );
                    counts.Add( chunk.Count );
                }
            }
            Assert.Greater( counts.Max( ), 100, "13x13 cells with 3 tries at 0.9 hold far more than a small budget" );
        }

        [ Test ]
        public void ChunkBudget_CutsDepositsLast( ) {
            var field = LandField( );
            var deposit = new ResourceRule {
                Count = 1, Probability = 1f, MaxRotation = 360,
                ClusterSpacing = ChunkSize, ClusterMin = 2, ClusterMax = 2, ClusterRadius = 0f
            };
            var rules = new[] { Trees( 0.9f ), deposit };
            var planner = Planner( field, rules, budget: 6 );
            var chunk = new List<PlannedResource>( );
            int chunksWithDeposits = 0;
            for( int cy = 0; cy < planner.ChunksPerSide; cy++ ) {
                for( int cx = 0; cx < planner.ChunksPerSide; cx++ ) {
                    planner.PlanChunk( cx, cy, chunk );
                    Assert.LessOrEqual( chunk.Count, 6 );
                    int deposits = chunk.Count( c => c.Deposit );
                    if( deposits > 0 ) {
                        chunksWithDeposits++;
                        Assert.AreEqual( 2, deposits, $"chunk {cx},{cy} lost part of its deposit while plain trees survived" );
                    }
                }
            }
            Assert.Greater( chunksWithDeposits, 10 );
        }

        [ Test ]
        public void ChunksOutsideTheMap_AreEmpty( ) {
            var planner = Planner( LandField( ), new[] { Trees( 1f ) } );
            var result = new List<PlannedResource>( );
            planner.PlanChunk( 5, 0, result );
            Assert.IsEmpty( result );
            planner.PlanChunk( -1, 2, result );
            Assert.IsEmpty( result );
        }

        [ Test ]
        public void MapWithoutRules_PlansNothing( ) {
            var planner = new ResourcePlanner( LandField( ), new IReadOnlyList<ResourceRule>[] { null, new ResourceRule[ 0 ] }, 1, Cell, ChunkSize, 10 );
            Assert.IsEmpty( planner.PlanMap( ) );
        }
    }
}
