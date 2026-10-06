using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    public static class GridFlood {

        /// <summary>
        /// Counts the cells connected to the start cell (start included, 8-connected) for which
        /// <paramref name="passable"/> is true, and stops as soon as the count exceeds <paramref name="limit"/>.
        /// Callers that only ask "is this region bigger than N" pay for N cells instead of the whole map.
        /// Returns 0 when the start cell itself is not passable.
        /// </summary>
        public static int CountReachable( int startX, int startY, Func<int, int, bool> passable, int limit ) {
            if( !passable( startX, startY ) ) {
                return 0;
            }
            var visited = new HashSet<long> { Pack( startX, startY ) };
            var queue = new Queue<long>();
            queue.Enqueue( Pack( startX, startY ) );

            while( queue.Count > 0 && visited.Count <= limit ) {
                long packed = queue.Dequeue();
                int x = ( int ) ( packed >> 32 );
                int y = ( int ) packed;
                for( int dy = -1; dy <= 1; dy++ ) {
                    for( int dx = -1; dx <= 1; dx++ ) {
                        if( dx == 0 && dy == 0 ) {
                            continue;
                        }
                        int nx = x + dx;
                        int ny = y + dy;
                        long key = Pack( nx, ny );
                        if( visited.Contains( key ) || !passable( nx, ny ) ) {
                            continue;
                        }
                        visited.Add( key );
                        queue.Enqueue( key );
                    }
                }
            }
            return visited.Count;
        }

        /// <summary>
        /// Every cell of a <paramref name="size"/> x <paramref name="size"/> grid connected (8-connected) to the start cell
        /// through cells for which <paramref name="passable"/> is true; index is <c>y * size + x</c>. Empty when the start is not passable.
        /// </summary>
        public static bool[] ReachableFrom( int size, int startX, int startY, Func<int, int, bool> passable ) {
            var reached = new bool[ size * size ];
            if( startX < 0 || startY < 0 || startX >= size || startY >= size || !passable( startX, startY ) ) {
                return reached;
            }
            var queue = new Queue<int>();
            reached[ startY * size + startX ] = true;
            queue.Enqueue( startY * size + startX );
            while( queue.Count > 0 ) {
                int cell = queue.Dequeue();
                int x = cell % size, y = cell / size;
                for( int dy = -1; dy <= 1; dy++ ) {
                    for( int dx = -1; dx <= 1; dx++ ) {
                        int nx = x + dx, ny = y + dy;
                        if( nx < 0 || ny < 0 || nx >= size || ny >= size || reached[ ny * size + nx ] || !passable( nx, ny ) ) {
                            continue;
                        }
                        reached[ ny * size + nx ] = true;
                        queue.Enqueue( ny * size + nx );
                    }
                }
            }
            return reached;
        }

        /// <summary>
        /// Counts the cells connected to the start cell (start included, 4-connected/cardinal only, no
        /// diagonal step) by a move <paramref name="canMove"/>(x, y, nx, ny) allows, and stops as soon as the
        /// count exceeds <paramref name="limit"/>. Cardinal-only because an edge-anchored wall (see
        /// <see cref="BuildEdgeGrid"/>) only ever blocks a cardinal crossing - an 8-connected fill could leak
        /// diagonally through a corner two walls meet at, even though nothing there was ever left open.
        /// </summary>
        public static int CountReachableCardinal( int startX, int startY, Func<int, int, int, int, bool> canMove, int limit ) {
            var visited = new HashSet<long> { Pack( startX, startY ) };
            var queue = new Queue<long>();
            queue.Enqueue( Pack( startX, startY ) );

            while( queue.Count > 0 && visited.Count <= limit ) {
                long packed = queue.Dequeue();
                int x = ( int ) ( packed >> 32 );
                int y = ( int ) packed;
                TryStepCardinal( x, y, x + 1, y, canMove, visited, queue );
                TryStepCardinal( x, y, x - 1, y, canMove, visited, queue );
                TryStepCardinal( x, y, x, y + 1, canMove, visited, queue );
                TryStepCardinal( x, y, x, y - 1, canMove, visited, queue );
            }
            return visited.Count;
        }

        private static void TryStepCardinal( int x, int y, int nx, int ny, Func<int, int, int, int, bool> canMove, HashSet<long> visited, Queue<long> queue ) {
            long key = Pack( nx, ny );
            if( visited.Contains( key ) || !canMove( x, y, nx, ny ) ) {
                return;
            }
            visited.Add( key );
            queue.Enqueue( key );
        }

        /// <summary>
        /// Like <see cref="CountReachableCardinal"/> but returns the visited cells themselves (start included)
        /// instead of just a count - needed to know a bounded pocket's exact shape (see
        /// <see cref="RoomEnclosureDetector"/>), not only whether one exists. Returns null when the fill hits
        /// <paramref name="limit"/> before draining its queue, matching CountReachableCardinal's "reached > limit" case.
        /// </summary>
        public static List<(int x, int y)> FloodReachableCardinal( int startX, int startY, Func<int, int, int, int, bool> canMove, int limit ) {
            var visited = new HashSet<long> { Pack( startX, startY ) };
            var order = new List<(int x, int y)> { ( startX, startY ) };
            var queue = new Queue<long>();
            queue.Enqueue( Pack( startX, startY ) );

            while( queue.Count > 0 && visited.Count <= limit ) {
                long packed = queue.Dequeue();
                int x = ( int ) ( packed >> 32 );
                int y = ( int ) packed;
                TryStepCardinalCollecting( x, y, x + 1, y, canMove, visited, queue, order );
                TryStepCardinalCollecting( x, y, x - 1, y, canMove, visited, queue, order );
                TryStepCardinalCollecting( x, y, x, y + 1, canMove, visited, queue, order );
                TryStepCardinalCollecting( x, y, x, y - 1, canMove, visited, queue, order );
            }
            return visited.Count <= limit ? order : null;
        }

        private static void TryStepCardinalCollecting( int x, int y, int nx, int ny, Func<int, int, int, int, bool> canMove, HashSet<long> visited, Queue<long> queue, List<(int x, int y)> order ) {
            long key = Pack( nx, ny );
            if( visited.Contains( key ) || !canMove( x, y, nx, ny ) ) {
                return;
            }
            visited.Add( key );
            queue.Enqueue( key );
            order.Add( ( nx, ny ) );
        }

        private static long Pack( int x, int y ) {
            return ( ( long ) x << 32 ) | ( uint ) y;
        }
    }
}
