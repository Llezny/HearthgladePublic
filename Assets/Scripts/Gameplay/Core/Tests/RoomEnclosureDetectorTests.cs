using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class RoomEnclosureDetectorTests {

        private static bool AlwaysWalkable( int x, int z ) => true;

        // Builds the four walls around a square room spanning [minX, maxX] x [minZ, maxZ] (inclusive) into an
        // edge grid at Y layer 0, and returns an edgeOpen delegate RoomEnclosureDetector can use directly.
        private static (BuildEdgeGrid edges, System.Func<int, int, int, int, bool> edgeOpen) BuildSquareRoom( int minX, int maxX, int minZ, int maxZ ) {
            var edges = new BuildEdgeGrid();
            int id = 1;
            for( int x = minX; x <= maxX; x++ ) {
                edges.Place( id++, new BuildEdgeBox( x, minZ - 1, 0, EdgeSide.PlusZ, 1 ) ); // south wall
                edges.Place( id++, new BuildEdgeBox( x, maxZ, 0, EdgeSide.PlusZ, 1 ) );     // north wall
            }
            for( int z = minZ; z <= maxZ; z++ ) {
                edges.Place( id++, new BuildEdgeBox( minX - 1, z, 0, EdgeSide.PlusX, 1 ) ); // west wall
                edges.Place( id++, new BuildEdgeBox( maxX, z, 0, EdgeSide.PlusX, 1 ) );     // east wall
            }
            bool EdgeOpen( int x, int z, int nx, int nz ) {
                var ( ex, ez, side ) = BuildEdgeGrid.Canonicalize( x, z, nx - x, nz - z );
                return !edges.IsOccupied( ex, ez, 0, side );
            }
            return ( edges, EdgeOpen );
        }

        [ Test ]
        public void OpenField_WithNoWalls_IsNotEnclosed( ) {
            bool EdgeOpen( int x, int z, int nx, int nz ) => true;
            Assert.IsFalse( RoomEnclosureDetector.IsEnclosed( 0, 0, AlwaysWalkable, EdgeOpen, limit: 20 ) );
        }

        [ Test ]
        public void FullyWalledSquareRoom_IsEnclosed( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 2, 0, 2 ); // 3x3 room
            Assert.IsTrue( RoomEnclosureDetector.IsEnclosed( 1, 1, AlwaysWalkable, edgeOpen, limit: 500 ) );
        }

        [ Test ]
        public void SquareRoom_WithOneMissingWallSegment_IsNotEnclosed( ) {
            // Same 3x3 room as above, but with a doorway-sized hole left open in the south wall at x=1.
            var openRoom = new BuildEdgeGrid();
            int id = 1;
            for( int x = 0; x <= 2; x++ ) {
                if( x != 1 ) {
                    openRoom.Place( id++, new BuildEdgeBox( x, -1, 0, EdgeSide.PlusZ, 1 ) );
                }
                openRoom.Place( id++, new BuildEdgeBox( x, 2, 0, EdgeSide.PlusZ, 1 ) );
            }
            for( int z = 0; z <= 2; z++ ) {
                openRoom.Place( id++, new BuildEdgeBox( -1, z, 0, EdgeSide.PlusX, 1 ) );
                openRoom.Place( id++, new BuildEdgeBox( 2, z, 0, EdgeSide.PlusX, 1 ) );
            }
            bool EdgeOpen( int x, int z, int nx, int nz ) {
                var ( ex, ez, side ) = BuildEdgeGrid.Canonicalize( x, z, nx - x, nz - z );
                return !openRoom.IsOccupied( ex, ez, 0, side );
            }
            Assert.IsFalse( RoomEnclosureDetector.IsEnclosed( 1, 1, AlwaysWalkable, EdgeOpen, limit: 500 ) );
        }

        [ Test ]
        public void NonWalkableCells_AreTreatedLikeWalls( ) {
            // A room with no walls at all, but every cell outside a 3x3 box is simply not walkable (e.g. water).
            bool Walkable( int x, int z ) => x >= 0 && x <= 2 && z >= 0 && z <= 2;
            bool EdgeOpen( int x, int z, int nx, int nz ) => true;
            Assert.IsTrue( RoomEnclosureDetector.IsEnclosed( 1, 1, Walkable, EdgeOpen, limit: 500 ) );
        }

        [ Test ]
        public void RoomFloodFill_ReturnsTheRoomsCells_ForAWalledSquare( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 2, 0, 2 );
            var fill = new RoomFloodFill( );
            var cells = fill.TryGetEnclosedCells( 1, 1, ( x, z, nx, nz ) => edgeOpen( x, z, nx, nz ) );
            Assert.NotNull( cells );
            Assert.AreEqual( 9, cells.Count );
            Assert.Contains( ( 0, 0 ), cells );
            Assert.Contains( ( 2, 2 ), cells );
        }

        [ Test ]
        public void RoomFloodFill_ReturnsNull_ForAnOpenField_AndKeepsWorkingAfterwards( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 2, 0, 2 );
            var fill = new RoomFloodFill( );
            Assert.IsNull( fill.TryGetEnclosedCells( 10, 10, ( x, z, nx, nz ) => true, limit: 20 ) );

            // The collections are reused: a later call must not see leftovers of the failed one.
            var cells = fill.TryGetEnclosedCells( 1, 1, ( x, z, nx, nz ) => edgeOpen( x, z, nx, nz ) );
            Assert.AreEqual( 9, cells.Count );
        }

        [ Test ]
        public void RoomFloodFill_ResultIsACopy_SoALaterCallDoesNotChangeIt( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 2, 0, 2 );
            var fill = new RoomFloodFill( );
            var first = fill.TryGetEnclosedCells( 1, 1, ( x, z, nx, nz ) => edgeOpen( x, z, nx, nz ) );
            fill.TryGetEnclosedCells( 10, 10, ( x, z, nx, nz ) => true, limit: 20 );
            Assert.AreEqual( 9, first.Count );
        }

        [ Test ]
        public void RoomFloodFill_AgreesWithTheStaticDetector( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 4, 0, 3 );
            var expected = RoomEnclosureDetector.TryGetEnclosedCells( 2, 1, AlwaysWalkable, edgeOpen );
            var actual = new RoomFloodFill( ).TryGetEnclosedCells( 2, 1, ( x, z, nx, nz ) => edgeOpen( x, z, nx, nz ) );
            CollectionAssert.AreEquivalent( expected, actual );
        }

        [ Test ]
        public void LargeButBoundedRoom_ExceedingTheLimit_IsNotEnclosed( ) {
            var ( _, edgeOpen ) = BuildSquareRoom( 0, 9, 0, 9 ); // 100 cells, bigger than a small cap
            Assert.IsFalse( RoomEnclosureDetector.IsEnclosed( 5, 5, AlwaysWalkable, edgeOpen, limit: 20 ) );
        }
    }
}
