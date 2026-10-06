namespace Hearthglade.Core.World {

    public static class BuildCategoryExtensions {

        /// <summary>Walls, doors, fences and gates sit on the boundary between two cells (<see cref="BuildEdgeGrid"/>), not in a cell.</summary>
        public static bool IsEdgePiece( this BuildCategory category ) {
            return category == BuildCategory.Wall || category == BuildCategory.Door
                || category == BuildCategory.Fence || category == BuildCategory.Gate;
        }

        /// <summary>A fence or gate blocks movement like a wall but never closes a room.</summary>
        public static EdgeKind ToEdgeKind( this BuildCategory category ) {
            return category == BuildCategory.Fence || category == BuildCategory.Gate ? EdgeKind.Fence : EdgeKind.Wall;
        }
    }
}
