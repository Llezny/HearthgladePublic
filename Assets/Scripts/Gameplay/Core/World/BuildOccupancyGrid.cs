using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>What a buildable piece is, for movement blocking and (later) room-enclosure detection.</summary>
    public enum BuildCategory { Wall, Door, Floor, Furniture, Decoration, Fence, Gate, Plot }

    /// <summary>
    /// The axis-aligned box of cells a placed building piece occupies on the build grid: an anchor (its lowest,
    /// most negative corner) and a size, all in whole cells. X/Z share the world's terrain grid cell size
    /// (<c>MapGenerator.TILE_X_OFFSET</c>); Y is the same cell size stacked upward from the ground.
    /// </summary>
    public readonly struct BuildCellBox {
        public readonly int X, Y, Z;
        public readonly int SizeX, SizeY, SizeZ;

        public BuildCellBox( int x, int y, int z, int sizeX, int sizeY, int sizeZ ) {
            if( sizeX <= 0 || sizeY <= 0 || sizeZ <= 0 ) {
                throw new ArgumentOutOfRangeException( "size", "a build piece must occupy at least one cell on every axis" );
            }
            X = x;
            Y = y;
            Z = z;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
        }

        /// <summary>The cell one above the box's top layer - where something resting on top of this piece starts.</summary>
        public int TopY => Y + SizeY;

        public IEnumerable<(int x, int y, int z)> Cells( ) {
            for( int dy = 0; dy < SizeY; dy++ ) {
                for( int dz = 0; dz < SizeZ; dz++ ) {
                    for( int dx = 0; dx < SizeX; dx++ ) {
                        yield return ( X + dx, Y + dy, Z + dz );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Which cells of the 3D build grid are taken, and by which piece. Pure occupancy bookkeeping: it knows
    /// nothing about terrain walkability or item categories, so a placement validator (later phase) combines
    /// this with <see cref="TerrainGrid"/> to decide whether a piece can actually go somewhere.
    /// </summary>
    public sealed class BuildOccupancyGrid {

        private readonly Dictionary<(int x, int y, int z), int> occupiedBy = new Dictionary<(int, int, int), int>( );
        private readonly Dictionary<int, BuildCellBox> pieces = new Dictionary<int, BuildCellBox>( );

        public bool IsOccupied( int x, int y, int z ) {
            return occupiedBy.ContainsKey( ( x, y, z ) );
        }

        /// <summary>True when every cell of the box is free (ignoring terrain/support - see the type doc).</summary>
        public bool CanPlace( BuildCellBox box ) {
            foreach( var cell in box.Cells( ) ) {
                if( occupiedBy.ContainsKey( cell ) ) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Registers a piece's cells as occupied. Throws if any cell is already taken or the id is already placed.</summary>
        public void Place( int pieceId, BuildCellBox box ) {
            if( pieces.ContainsKey( pieceId ) ) {
                throw new InvalidOperationException( $"piece {pieceId} is already placed" );
            }
            if( !CanPlace( box ) ) {
                throw new InvalidOperationException( "cannot place a piece on cells that are already occupied" );
            }
            foreach( var cell in box.Cells( ) ) {
                occupiedBy[ cell ] = pieceId;
            }
            pieces[ pieceId ] = box;
        }

        /// <summary>Frees a placed piece's cells. Does nothing if the id was never placed.</summary>
        public void Remove( int pieceId ) {
            if( !pieces.TryGetValue( pieceId, out var box ) ) {
                return;
            }
            foreach( var cell in box.Cells( ) ) {
                occupiedBy.Remove( cell );
            }
            pieces.Remove( pieceId );
        }

        public BuildCellBox? BoxOf( int pieceId ) {
            return pieces.TryGetValue( pieceId, out var box ) ? box : ( BuildCellBox? ) null;
        }

        /// <summary>
        /// Highest <see cref="BuildCellBox.TopY"/> among placed pieces covering column (x, z), or 0 (ground
        /// level) when nothing is placed there - what something stacked on top of this column would rest on.
        /// </summary>
        public int TopY( int x, int z ) {
            int top = 0;
            foreach( var box in pieces.Values ) {
                if( x >= box.X && x < box.X + box.SizeX && z >= box.Z && z < box.Z + box.SizeZ ) {
                    top = Math.Max( top, box.TopY );
                }
            }
            return top;
        }

        /// <summary>
        /// The support height for a sizeX x sizeZ footprint anchored at (x0, z0): the common <see cref="TopY"/>
        /// of every column it covers, or null when the columns disagree - too uneven a place to build on
        /// (e.g. half the footprint rests on a table, the other half on bare ground).
        /// </summary>
        public int? SupportY( int x0, int z0, int sizeX, int sizeZ ) {
            int? support = null;
            for( int dz = 0; dz < sizeZ; dz++ ) {
                for( int dx = 0; dx < sizeX; dx++ ) {
                    int y = TopY( x0 + dx, z0 + dz );
                    if( support == null ) {
                        support = y;
                    } else if( support != y ) {
                        return null;
                    }
                }
            }
            return support;
        }
    }
}
