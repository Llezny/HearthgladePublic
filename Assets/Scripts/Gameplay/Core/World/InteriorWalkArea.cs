using System.Collections.Generic;

namespace Hearthglade.Core.World
{
    /// <summary>An axis-aligned rectangle on the ground plane (x east, z north), in world metres.</summary>
    public readonly struct WalkRect
    {
        public readonly float MinX, MinZ, MaxX, MaxZ;

        public WalkRect(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX < maxX ? minX : maxX;
            MaxX = minX < maxX ? maxX : minX;
            MinZ = minZ < maxZ ? minZ : maxZ;
            MaxZ = minZ < maxZ ? maxZ : minZ;
        }

        public bool Contains(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        /// <summary>The same rectangle grown (or shrunk, when negative) by an amount on every side.</summary>
        public WalkRect Grown(float amount)
        {
            return new WalkRect(MinX - amount, MinZ - amount, MaxX + amount, MaxZ + amount);
        }
    }

    /// <summary>
    /// Where the player may stand inside a house: a rectangular room with rectangular pieces of furniture in it. The player's
    /// pivot is kept <c>margin</c> away from the walls and from the furniture. Pure data and arithmetic, so it is testable without Unity.
    /// </summary>
    public sealed class InteriorWalkArea
    {
        private readonly WalkRect room;
        private readonly WalkRect[] obstacles;

        public InteriorWalkArea(WalkRect room, IReadOnlyList<WalkRect> obstacles, float margin)
        {
            this.room = room.Grown(-margin);
            this.obstacles = new WalkRect[obstacles?.Count ?? 0];
            for (int i = 0; i < this.obstacles.Length; i++)
            {
                this.obstacles[i] = obstacles[i].Grown(margin);
            }
        }

        public bool CanStand(float x, float z)
        {
            if (!room.Contains(x, z))
            {
                return false;
            }
            foreach (var obstacle in obstacles)
            {
                if (obstacle.Contains(x, z))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// A step is allowed when it ends on a free spot. The start is not looked at, so a player who somehow ends up inside a
        /// piece of furniture can still walk out of it.
        /// </summary>
        public bool CanMoveTo(float toX, float toZ)
        {
            return CanStand(toX, toZ);
        }
    }
}
