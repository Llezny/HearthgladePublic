using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>
    /// <see cref="RoomEnclosureDetector.TryGetEnclosedCells"/> with its working collections kept between calls.
    /// The static version builds a HashSet, a Queue and a List for every call, and when the player is outside
    /// (the usual case) the fill runs all the way to the limit and the result is thrown away, so each room check
    /// left tens of kilobytes of garbage. Not thread-safe; one instance per caller.
    /// </summary>
    public sealed class RoomFloodFill {

        private readonly HashSet<long> visited = new();
        private readonly Queue<long> queue = new();
        private readonly List<(int x, int z)> order = new();

        /// <summary>
        /// The cells of the pocket the fill from (startX, startZ) is confined to (start included), or null when
        /// it reaches <paramref name="limit"/> cells first. <paramref name="canMove"/>(x, z, nx, nz) is whether
        /// stepping from a cell to its cardinal neighbour is allowed. The returned list is a fresh copy only when
        /// a pocket was found, so callers may keep it.
        /// </summary>
        public List<(int x, int z)> TryGetEnclosedCells( int startX, int startZ, Func<int, int, int, int, bool> canMove, int limit = RoomEnclosureDetector.DefaultSearchLimit ) {
            return TryFill( startX, startZ, canMove, limit ) ? new List<(int x, int z)>( order ) : null;
        }

        /// <summary>The cells found by the last successful <see cref="TryFill"/>; reused by the next call, so copy it to keep it.</summary>
        public List<(int x, int z)> Cells => order;

        /// <summary>
        /// Same fill as <see cref="TryGetEnclosedCells"/> but allocation-free: true when the fill is confined to a
        /// pocket, whose cells are then in <see cref="Cells"/>.
        /// </summary>
        public bool TryFill( int startX, int startZ, Func<int, int, int, int, bool> canMove, int limit = RoomEnclosureDetector.DefaultSearchLimit ) {
            visited.Clear();
            queue.Clear();
            order.Clear();

            visited.Add( Pack( startX, startZ ) );
            order.Add( ( startX, startZ ) );
            queue.Enqueue( Pack( startX, startZ ) );

            while( queue.Count > 0 && visited.Count <= limit ) {
                long packed = queue.Dequeue();
                int x = ( int ) ( packed >> 32 );
                int z = ( int ) packed;
                Step( x, z, x + 1, z, canMove );
                Step( x, z, x - 1, z, canMove );
                Step( x, z, x, z + 1, canMove );
                Step( x, z, x, z - 1, canMove );
            }
            return visited.Count <= limit;
        }

        private void Step( int x, int z, int nx, int nz, Func<int, int, int, int, bool> canMove ) {
            long key = Pack( nx, nz );
            if( visited.Contains( key ) || !canMove( x, z, nx, nz ) ) {
                return;
            }
            visited.Add( key );
            queue.Enqueue( key );
            order.Add( ( nx, nz ) );
        }

        private static long Pack( int x, int z ) {
            return ( ( long ) x << 32 ) | ( uint ) z;
        }
    }
}
