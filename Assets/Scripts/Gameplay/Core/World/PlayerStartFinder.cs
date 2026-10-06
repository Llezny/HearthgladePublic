using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Picks where the player starts: on the west coast, in the middle of the map, a couple of cells in from the water.
    /// The ship that leads out of the map is placed to the west of the start, so it ends up floating at the shore.
    /// </summary>
    public static class PlayerStartFinder {

        /// <summary>How many cells inland of the first standable cell of a row the player starts.</summary>
        public const int InlandOffset = 2;

        /// <summary>
        /// Scans the rows around the middle from the west edge and takes the first standable cell that belongs to a region bigger
        /// than <paramref name="minRegionSize"/>. Returns false only when the map has no such region at all.
        /// </summary>
        public static bool TryFind( int size, Func<int, int, bool> standable, int minRegionSize, out int startX, out int startZ ) {
            startX = startZ = 0;
            int middle = size / 2;
            for( int distance = 0; distance < size; distance++ ) {
                for( int sign = -1; sign <= 1; sign += 2 ) {
                    if( distance == 0 && sign == 1 ) {
                        continue;
                    }
                    int z = middle + sign * distance;
                    if( z < 0 || z >= size ) {
                        continue;
                    }
                    for( int x = 0; x < size; x++ ) {
                        if( !standable( x, z ) || GridFlood.CountReachable( x, z, standable, minRegionSize ) <= minRegionSize ) {
                            continue;
                        }
                        int inland = x;
                        for( int step = 1; step <= InlandOffset && standable( x + step, z ); step++ ) {
                            inland = x + step;
                        }
                        startX = inland;
                        startZ = z;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
