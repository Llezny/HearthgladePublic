using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>One resource the planner decided to grow: the cell that owns it, which rule of which table, and where.</summary>
    public sealed class PlannedResource {
        public int GridX;
        public int GridY;

        /// <summary>Index of the table (the biome) the rule comes from.</summary>
        public int Table;
        public int RuleIndex;

        /// <summary>Offset from the cell centre in world units.</summary>
        public float OffsetX;
        public float OffsetZ;
        public float RotationY;

        /// <summary>Position in cells; a cell centre has integer coordinates.</summary>
        public float X;
        public float Y;

        /// <summary>Who wins when two objects compete (spacing, budget): a hash of the position, so it never depends on order.</summary>
        public uint Priority;

        /// <summary>The object comes from a deposit rule (ore veins), which the chunk budget cuts last.</summary>
        public bool Deposit;

        internal enum Verdict : byte { Unknown, Pending, Keeps, Loses }
        internal Verdict State;
    }

    /// <summary>
    /// Decides what grows on a map: rolls every cell (<see cref="ResourceRoller"/>), keeps the objects of a rule apart
    /// (<see cref="ResourceRule.MinSpacing"/>), leaves reserved cells empty and caps the number of objects per chunk.
    /// Everything is a pure function of the seed, the terrain and the reservations, so a chunk always gets the same objects
    /// whichever order the chunks are planned in. Reserve cells (<see cref="ResourceField.Reserve"/>) before the first plan:
    /// the planner remembers what it rolled.
    /// </summary>
    public sealed class ResourcePlanner {

        private static readonly List<PlannedResource> None = new List<PlannedResource>( 0 );

        private readonly ResourceField field;
        private readonly IReadOnlyList<ResourceRule>[] tables;
        private readonly int seed;
        private readonly float cellSize;
        private readonly int chunkSize;
        private readonly int chunkBudget;
        private readonly int size;

        // What each cell rolled, before spacing and budget. Filled on demand, so deciding an object near a chunk border
        // rolls the neighbouring chunk's cells too.
        private readonly Dictionary<int, List<PlannedResource>> rolled = new Dictionary<int, List<PlannedResource>>( );

        private readonly List<BiomeShare> mix = new List<BiomeShare>( );
        private readonly List<ResourcePlacement> buffer = new List<ResourcePlacement>( );

        /// <param name="tables">Rules per biome index of the map.</param>
        /// <param name="chunkBudget">Most objects one chunk may hold; 0 = no limit.</param>
        public ResourcePlanner( ResourceField field, IReadOnlyList<ResourceRule>[] tables, int seed, float cellSize, int chunkSize, int chunkBudget ) {
            this.field = field ?? throw new ArgumentNullException( nameof( field ) );
            this.tables = tables ?? throw new ArgumentNullException( nameof( tables ) );
            if( chunkSize <= 0 ) {
                throw new ArgumentOutOfRangeException( nameof( chunkSize ) );
            }
            this.seed = seed;
            this.cellSize = cellSize;
            this.chunkSize = chunkSize;
            this.chunkBudget = chunkBudget;
            this.size = field.Size;
        }

        public int ChunksPerSide => ( size + chunkSize - 1 ) / chunkSize;

        /// <summary>Plans the whole map, chunk by chunk; the key is <c>z * size + x</c> of the owning cell.</summary>
        public Dictionary<int, List<PlannedResource>> PlanMap( ) {
            var byCell = new Dictionary<int, List<PlannedResource>>( );
            var chunk = new List<PlannedResource>( );
            for( int cy = 0; cy < ChunksPerSide; cy++ ) {
                for( int cx = 0; cx < ChunksPerSide; cx++ ) {
                    PlanChunk( cx, cy, chunk );
                    foreach( var item in chunk ) {
                        int key = item.GridY * size + item.GridX;
                        if( !byCell.TryGetValue( key, out var list ) ) {
                            byCell[ key ] = list = new List<PlannedResource>( );
                        }
                        list.Add( item );
                    }
                }
            }
            return byCell;
        }

        /// <summary>Plans one chunk: the resources of the cells it covers, spaced apart and cut to the budget.</summary>
        public void PlanChunk( int chunkX, int chunkY, List<PlannedResource> result ) {
            result.Clear( );
            int x0 = chunkX * chunkSize, z0 = chunkY * chunkSize;
            int x1 = Math.Min( size, x0 + chunkSize ) - 1, z1 = Math.Min( size, z0 + chunkSize ) - 1;
            if( x0 < 0 || z0 < 0 || x0 > x1 || z0 > z1 ) {
                return;
            }
            for( int z = z0; z <= z1; z++ ) {
                for( int x = x0; x <= x1; x++ ) {
                    foreach( var candidate in RolledAt( x, z ) ) {
                        if( Keeps( candidate ) ) {
                            result.Add( candidate );
                        }
                    }
                }
            }

            if( chunkBudget > 0 && result.Count > chunkBudget ) {
                // Cut the least important first: plain spawns before deposits, then the lowest priority.
                var order = new List<PlannedResource>( result );
                order.Sort( Weakest );
                var dropped = new HashSet<PlannedResource>( order.GetRange( 0, result.Count - chunkBudget ) );
                result.RemoveAll( dropped.Contains );
            }
        }

        private static int Weakest( PlannedResource a, PlannedResource b ) {
            if( a.Deposit != b.Deposit ) {
                return a.Deposit ? 1 : -1;
            }
            return Compare( a, b );
        }

        // A total order on candidates: priority first, position as the tie breaker.
        private static int Compare( PlannedResource a, PlannedResource b ) {
            if( a.Priority != b.Priority ) {
                return a.Priority < b.Priority ? -1 : 1;
            }
            int byX = a.X.CompareTo( b.X );
            return byX != 0 ? byX : a.Y.CompareTo( b.Y );
        }

        private List<PlannedResource> RolledAt( int x, int z ) {
            if( x < 0 || z < 0 || x >= size || z >= size ) {
                return None;
            }
            int key = z * size + x;
            if( !rolled.TryGetValue( key, out var list ) ) {
                list = Roll( x, z ) ?? None;
                rolled[ key ] = list;
            }
            return list;
        }

        private List<PlannedResource> Roll( int x, int z ) {
            if( !field.IsGround( x, z ) || field.IsReserved( x, z ) ) {
                return null;
            }
            field.MixAt( x, z, mix );
            if( mix.Count == 0 ) {
                return null;
            }
            var context = field.ContextAt( x, z );
            List<PlannedResource> list = null;
            for( int m = 0; m < mix.Count; m++ ) {
                var share = mix[ m ];
                var rules = share.Biome < tables.Length ? tables[ share.Biome ] : null;
                if( rules == null || rules.Count == 0 ) {
                    continue;
                }
                ResourceRoller.Roll( rules, share.Weight, context, seed, share.Biome + 1, x, z, cellSize, buffer, field );
                foreach( var placement in buffer ) {
                    float px = x + placement.OffsetX / cellSize, pz = z + placement.OffsetZ / cellSize;
                    // An object strays up to about a cell from the cell that rolled it: what it lands on counts, not where it was rolled.
                    if( field.IsReserved( ( int ) Math.Floor( px + 0.5f ), ( int ) Math.Floor( pz + 0.5f ) ) ) {
                        continue;
                    }
                    list ??= new List<PlannedResource>( );
                    list.Add( new PlannedResource {
                        GridX = x, GridY = z, Table = share.Biome, RuleIndex = placement.RuleIndex,
                        OffsetX = placement.OffsetX, OffsetZ = placement.OffsetZ, RotationY = placement.RotationY,
                        X = px, Y = pz,
                        Priority = PriorityAt( share.Biome, placement.RuleIndex, px, pz ),
                        Deposit = rules[ placement.RuleIndex ].ClusterSpacing > 0
                    } );
                }
            }
            return list;
        }

        private uint PriorityAt( int table, int rule, float x, float y ) {
            unchecked {
                uint h = SeedMixer.Mix( ( uint ) seed, ( uint ) ( table * 4096 + rule ) );
                h = SeedMixer.Mix( h, ( uint ) ( int ) Math.Round( x * 256.0 ) );
                return SeedMixer.Mix( h, ( uint ) ( int ) Math.Round( y * 256.0 ) );
            }
        }

        // How far (in cells) an object of the rule can lie from the centre of the cell that rolled it. Deposit objects stay inside their cell.
        private float Stray( in ResourceRule rule ) {
            return rule.ClusterSpacing > 0 ? 0.5f : rule.SpawnRange / cellSize;
        }

        /// <summary>
        /// Greedy by priority: an object stays unless a stronger object of the same rule closer than the rule's spacing
        /// stays itself. The verdict of each object is a pure function of the rolls around it, memoised on the object;
        /// it only ever looks at stronger objects, so the recursion ends.
        /// </summary>
        private bool Keeps( PlannedResource candidate ) {
            switch( candidate.State ) {
                case PlannedResource.Verdict.Keeps:
                    return true;
                case PlannedResource.Verdict.Loses:
                    return false;
                case PlannedResource.Verdict.Pending:
                    throw new InvalidOperationException( "resource spacing looped: the priority order is not strict" );
            }

            var rule = tables[ candidate.Table ][ candidate.RuleIndex ];
            if( rule.MinSpacing <= 0f ) {
                candidate.State = PlannedResource.Verdict.Keeps;
                return true;
            }

            candidate.State = PlannedResource.Verdict.Pending;
            // Where a competitor's own cell can be: its position is within the spacing of ours, and it strays from its cell.
            int reach = ( int ) Math.Ceiling( rule.MinSpacing + Stray( rule ) + 0.5f );
            float limit = rule.MinSpacing * rule.MinSpacing;
            int centerX = ( int ) Math.Floor( candidate.X + 0.5f ), centerZ = ( int ) Math.Floor( candidate.Y + 0.5f );
            bool kept = true;
            for( int z = centerZ - reach; z <= centerZ + reach && kept; z++ ) {
                for( int x = centerX - reach; x <= centerX + reach && kept; x++ ) {
                    foreach( var other in RolledAt( x, z ) ) {
                        if( ReferenceEquals( other, candidate ) || other.Table != candidate.Table || other.RuleIndex != candidate.RuleIndex ) {
                            continue;
                        }
                        float dx = other.X - candidate.X, dy = other.Y - candidate.Y;
                        if( dx * dx + dy * dy < limit && Compare( other, candidate ) > 0 && Keeps( other ) ) {
                            kept = false;
                            break;
                        }
                    }
                }
            }
            candidate.State = kept ? PlannedResource.Verdict.Keeps : PlannedResource.Verdict.Loses;
            return kept;
        }
    }
}
