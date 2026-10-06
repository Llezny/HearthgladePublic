using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// The "you're in home" POC (docs/BUILDING_SYSTEM_PLAN.md section 7): whether the player's current cell
    /// sits in a walled-off pocket, found with a capped cardinal flood-fill. Unity-free like
    /// <see cref="GridFlood"/>/<see cref="BuildEdgeGrid"/> - the caller supplies terrain walkability and edge
    /// blocking as delegates, so this knows nothing about <c>TerrainGrid</c> or which Y-layer is being tested.
    /// </summary>
    public static class RoomEnclosureDetector {

        public const int DefaultSearchLimit = 500;

        /// <summary>
        /// True when the flood fill from (startX, startZ) runs out of reachable cells before hitting
        /// <paramref name="limit"/> - a bounded pocket, i.e. "enclosed". False when it hits the limit while
        /// cells are still left to explore - the reachable area is at least that big, so treated as outside.
        /// <paramref name="walkable"/>(x, z) is the terrain at that cell; <paramref name="edgeOpen"/>(x, z, nx,
        /// nz) is whether nothing blocks stepping from (x, z) to its cardinal neighbour (nx, nz) - the caller
        /// bakes in whatever Y-layer of <see cref="BuildEdgeGrid"/> it is testing against.
        /// </summary>
        public static bool IsEnclosed( int startX, int startZ, Func<int, int, bool> walkable, Func<int, int, int, int, bool> edgeOpen, int limit = DefaultSearchLimit ) {
            int reached = GridFlood.CountReachableCardinal( startX, startZ,
                ( x, z, nx, nz ) => walkable( nx, nz ) && edgeOpen( x, z, nx, nz ), limit );
            return reached <= limit;
        }

        /// <summary>
        /// Like <see cref="IsEnclosed"/> but returns the enclosed room's own cells (start included) instead of
        /// just a bool - null when not enclosed. Used to know the room's exact footprint, e.g. to darken/reveal
        /// it from outside/inside (docs/BUILDING_SYSTEM_PLAN.md phase 7).
        /// </summary>
        public static System.Collections.Generic.List<(int x, int z)> TryGetEnclosedCells( int startX, int startZ, Func<int, int, bool> walkable, Func<int, int, int, int, bool> edgeOpen, int limit = DefaultSearchLimit ) {
            return GridFlood.FloodReachableCardinal( startX, startZ,
                ( x, z, nx, nz ) => walkable( nx, nz ) && edgeOpen( x, z, nx, nz ), limit );
        }
    }
}
