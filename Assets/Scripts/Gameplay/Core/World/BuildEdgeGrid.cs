using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Which side of cell (X, Z) an edge-anchored piece (wall, door) sits on. Canonical: an edge is always
    /// named from its lower-coordinate cell, so the boundary between (x,z) and (x+1,z) is PlusX of (x,z) -
    /// there is no "MinusX", that would be the same edge named from the other cell.
    /// </summary>
    public enum EdgeSide { PlusX, PlusZ }

    /// <summary>What stands on an edge. Both block movement; only a <see cref="Wall"/> closes a room
    /// (a fenced garden is not a room: no darkening, no roof, no see-through walls).</summary>
    public enum EdgeKind { Wall, Fence }

    /// <summary>
    /// The cells an edge-anchored building piece (wall, door) occupies: the boundary between cell (X, Z) and
    /// its PlusX/PlusZ neighbour, from Y upward for SizeY layers. X/Z/Y share the world grid's cell size
    /// (<c>MapGenerator.TILE_X_OFFSET</c>), the same convention as <see cref="BuildCellBox"/>. Edge pieces are
    /// exactly one cell wide along the edge for now - no multi-cell-long walls yet.
    /// </summary>
    public readonly struct BuildEdgeBox {
        public readonly int X, Z, Y;
        public readonly EdgeSide Side;
        public readonly int SizeY;
        public readonly EdgeKind Kind;

        public BuildEdgeBox( int x, int z, int y, EdgeSide side, int sizeY, EdgeKind kind = EdgeKind.Wall ) {
            if( sizeY <= 0 ) {
                throw new ArgumentOutOfRangeException( "sizeY", "an edge piece must occupy at least one cell of height" );
            }
            X = x;
            Z = z;
            Y = y;
            Side = side;
            SizeY = sizeY;
            Kind = kind;
        }

        /// <summary>The layer one above the box's top - where something resting on top of this piece starts.</summary>
        public int TopY => Y + SizeY;

        public IEnumerable<int> YLayers() {
            for( int dy = 0; dy < SizeY; dy++ ) {
                yield return Y + dy;
            }
        }
    }

    /// <summary>
    /// Which edges of the 3D build grid are walled off, and by which piece. Pure occupancy bookkeeping,
    /// parallel to <see cref="BuildOccupancyGrid"/> but for edge-anchored pieces (walls, doors) instead of
    /// cell-anchored ones (furniture, decoration): a wall belongs to the boundary between two cells, not to
    /// either cell's interior, so straight runs and 90-degree corners are always flush by construction - no
    /// snap-point system needed (docs/BUILDING_SYSTEM_PLAN.md section 6, revised). This is also exactly what a
    /// room-enclosure flood-fill (section 7) needs: crossing from (x,z) to a neighbour is blocked when the
    /// edge between them is occupied here, independent of whatever furniture sits in either cell.
    /// </summary>
    public sealed class BuildEdgeGrid {

        private readonly Dictionary<(int x, int z, int y, EdgeSide side), int> occupiedBy = new ();
        private readonly Dictionary<int, BuildEdgeBox> pieces = new ();
        private readonly HashSet<int> openPieces = new ();

        /// <summary>Whether a piece (wall, door, fence or gate) physically occupies this edge - true for a door
        /// regardless of open/closed state. Use <see cref="IsPassable"/> for movement instead, and
        /// <see cref="IsWallOccupied"/> for room detection.</summary>
        public bool IsOccupied( int x, int z, int y, EdgeSide side ) {
            return occupiedBy.ContainsKey( ( x, z, y, side ) );
        }

        /// <summary>Whether a wall or door (not a fence or gate) occupies this edge, open or closed. This is what
        /// room-enclosure flood-fill (<see cref="RoomEnclosureDetector"/>) uses, so a doorway keeps counting as
        /// a wall while it stands open and a fence never closes a room.</summary>
        public bool IsWallOccupied( int x, int z, int y, EdgeSide side ) {
            return occupiedBy.TryGetValue( ( x, z, y, side ), out var pieceId ) && pieces[ pieceId ].Kind == EdgeKind.Wall;
        }

        /// <summary>Whether a piece occupying this edge currently lets you walk through it (an open door).
        /// False when the edge is empty too - this only answers "is whatever's here open", not "is this edge
        /// walkable"; movement should check "occupied AND NOT passable" (see Map.CanMoveTo), not this alone.</summary>
        public bool IsPassable( int x, int z, int y, EdgeSide side ) {
            return occupiedBy.TryGetValue( ( x, z, y, side ), out var pieceId ) && openPieces.Contains( pieceId );
        }

        /// <summary>Marks a placed piece as currently open (passable for movement) or closed (blocking) without
        /// removing it from the grid - a door swinging open/shut stays a wall for room-shape purposes
        /// (<see cref="IsOccupied"/>) the whole time. Does nothing if the id isn't placed.</summary>
        public void SetOpen( int pieceId, bool open ) {
            if( !pieces.ContainsKey( pieceId ) ) {
                return;
            }
            if( open ) {
                openPieces.Add( pieceId );
            }
            else {
                openPieces.Remove( pieceId );
            }
        }

        /// <summary>The id of whichever piece currently occupies this edge layer, if any - lets a caller that
        /// only knows a world position (not the id it was placed under) find it, e.g. a door looking up its own
        /// id to call <see cref="SetOpen"/>.</summary>
        public bool TryGetPieceId( int x, int z, int y, EdgeSide side, out int pieceId ) {
            return occupiedBy.TryGetValue( ( x, z, y, side ), out pieceId );
        }

        /// <summary>True when every layer of the box's edge is free.</summary>
        public bool CanPlace( BuildEdgeBox box ) {
            foreach( var y in box.YLayers() ) {
                if( occupiedBy.ContainsKey( ( box.X, box.Z, y, box.Side ) ) ) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Registers a piece's edge layers as occupied. Throws if any layer is already taken or the id is already placed.</summary>
        public void Place( int pieceId, BuildEdgeBox box ) {
            if( pieces.ContainsKey( pieceId ) ) {
                throw new InvalidOperationException( $"piece {pieceId} is already placed" );
            }
            if( !CanPlace( box ) ) {
                throw new InvalidOperationException( "cannot place a piece on an edge that is already occupied" );
            }
            foreach( var y in box.YLayers() ) {
                occupiedBy[ ( box.X, box.Z, y, box.Side ) ] = pieceId;
            }
            pieces[ pieceId ] = box;
        }

        /// <summary>Frees a placed piece's edge layers. Does nothing if the id was never placed.</summary>
        public void Remove( int pieceId ) {
            if( !pieces.TryGetValue( pieceId, out var box ) ) {
                return;
            }
            foreach( var y in box.YLayers() ) {
                occupiedBy.Remove( ( box.X, box.Z, y, box.Side ) );
            }
            pieces.Remove( pieceId );
            openPieces.Remove( pieceId );
        }

        public BuildEdgeBox? BoxOf( int pieceId ) {
            return pieces.TryGetValue( pieceId, out var box ) ? box : ( BuildEdgeBox? ) null;
        }

        /// <summary>Highest <see cref="BuildEdgeBox.TopY"/> stacked on this exact edge, or 0 (ground) when nothing is there.</summary>
        public int TopY( int x, int z, EdgeSide side ) {
            int top = 0;
            foreach( var box in pieces.Values ) {
                if( box.X == x && box.Z == z && box.Side == side ) {
                    top = Math.Max( top, box.TopY );
                }
            }
            return top;
        }

        /// <summary>
        /// Canonicalises a cell (fromX, fromZ) plus a cardinal step (dx, dz) into the edge between it and that
        /// neighbour, named from whichever of the two cells is lower on the axis that changes.
        /// </summary>
        public static (int x, int z, EdgeSide side) Canonicalize( int fromX, int fromZ, int dx, int dz ) {
            if( dx == 1 && dz == 0 ) return ( fromX, fromZ, EdgeSide.PlusX );
            if( dx == -1 && dz == 0 ) return ( fromX - 1, fromZ, EdgeSide.PlusX );
            if( dz == 1 && dx == 0 ) return ( fromX, fromZ, EdgeSide.PlusZ );
            if( dz == -1 && dx == 0 ) return ( fromX, fromZ - 1, EdgeSide.PlusZ );
            throw new ArgumentException( "(dx, dz) must be one of the four cardinal neighbour directions" );
        }
    }
}
