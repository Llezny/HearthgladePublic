using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Where a placed building piece keeps grass away: its footprint rectangle grown by <see cref="Radius"/>.
    /// Everything is in cell units (a cell's centre is an integer, as <c>GridMath.WorldToGrid</c> defines it),
    /// so tufts - which are placed per cell - can test against it without knowing the tile size.
    /// </summary>
    public readonly struct GrassClearZone {

        public readonly float CenterX, CenterZ;
        public readonly float HalfX, HalfZ;
        public readonly float Radius;

        public GrassClearZone( float centerX, float centerZ, float halfX, float halfZ, float radius ) {
            CenterX = centerX;
            CenterZ = centerZ;
            HalfX = halfX;
            HalfZ = halfZ;
            Radius = Math.Max( 0f, radius );
        }

        /// <summary>A footprint of sizeX x sizeZ whole cells whose lowest cell is (x0, z0).</summary>
        public static GrassClearZone ForCells( int x0, int z0, int sizeX, int sizeZ, float radius ) {
            return new GrassClearZone( x0 + ( sizeX - 1 ) * 0.5f, z0 + ( sizeZ - 1 ) * 0.5f, sizeX * 0.5f, sizeZ * 0.5f, radius );
        }

        /// <summary>
        /// A wall/door: a thin strip along the edge between two cells (the edge is half a cell off the cell
        /// centres on its normal axis, whole-cell on the other).
        /// </summary>
        public static GrassClearZone ForEdge( int x, int z, EdgeSide side, float thickness, float radius ) {
            return side == EdgeSide.PlusX
                ? new GrassClearZone( x + 0.5f, z, thickness * 0.5f, 0.5f, radius )
                : new GrassClearZone( x, z + 0.5f, 0.5f, thickness * 0.5f, radius );
        }

        /// <summary>
        /// Whether a tuft standing at (x, z) with a card reaching <paramref name="cardHalfWidth"/> to each side
        /// touches the zone - the footprint plus the radius, measured like a collider (so a tuft whose card only
        /// just overlaps the edge of the radius is removed too).
        /// </summary>
        public bool Touches( float x, float z, float cardHalfWidth ) {
            float dx = Math.Max( Math.Abs( x - CenterX ) - HalfX, 0f );
            float dz = Math.Max( Math.Abs( z - CenterZ ) - HalfZ, 0f );
            float reach = Radius + cardHalfWidth;
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>Whether the zone, grown by <paramref name="margin"/> cells, overlaps the cell rectangle.</summary>
        public bool Overlaps( float minX, float minZ, float maxX, float maxZ, float margin ) {
            float reach = Radius + margin;
            return CenterX + HalfX + reach >= minX && CenterX - HalfX - reach <= maxX
                && CenterZ + HalfZ + reach >= minZ && CenterZ - HalfZ - reach <= maxZ;
        }
    }
}
