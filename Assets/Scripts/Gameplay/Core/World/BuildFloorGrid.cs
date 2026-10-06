using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Which cells have a floor tile, and which piece placed it. Parallel to <see cref="BuildOccupancyGrid"/>
    /// and <see cref="BuildEdgeGrid"/> but deliberately its own thing, not built on either
    /// (docs/BUILDING_SYSTEM_PLAN.md section 9): a floor tile is thin, never stacks, and never blocks or is
    /// blocked by anything else on its cell (a floor and a piece of furniture on the same cell simply
    /// coexist) - forcing that into BuildOccupancyGrid.TopY/SupportY would corrupt furniture stacking, which
    /// assumes every registered box is a solid thing other pieces can rest on.
    /// </summary>
    public sealed class BuildFloorGrid {

        private readonly Dictionary<(int x, int z), int> occupiedBy = new();
        private readonly Dictionary<int, (int x, int z)> pieces = new();

        public bool Has( int x, int z ) {
            return occupiedBy.ContainsKey( ( x, z ) );
        }

        /// <summary>True when the cell has no floor tile yet.</summary>
        public bool CanPlace( int x, int z ) {
            return !Has( x, z );
        }

        /// <summary>Registers a floor tile at (x, z). Throws if the cell already has one or the id is already placed.</summary>
        public void Place( int pieceId, int x, int z ) {
            if( pieces.ContainsKey( pieceId ) ) {
                throw new InvalidOperationException( $"piece {pieceId} is already placed" );
            }
            if( !CanPlace( x, z ) ) {
                throw new InvalidOperationException( "cannot place a floor tile on a cell that already has one" );
            }
            occupiedBy[ ( x, z ) ] = pieceId;
            pieces[ pieceId ] = ( x, z );
        }

        /// <summary>Frees a placed piece's cell. Does nothing if the id was never placed.</summary>
        public void Remove( int pieceId ) {
            if( !pieces.TryGetValue( pieceId, out var cell ) ) {
                return;
            }
            occupiedBy.Remove( cell );
            pieces.Remove( pieceId );
        }

        public (int x, int z)? CellOf( int pieceId ) {
            return pieces.TryGetValue( pieceId, out var cell ) ? cell : ( (int x, int z)? ) null;
        }
    }
}
