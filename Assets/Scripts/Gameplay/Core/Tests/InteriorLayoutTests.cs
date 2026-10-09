using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class InteriorLayoutTests {

        // A room of 6 x 4 cells (x 0..5, z 0..3); the door is on the north side, the entry cell in front of it at (1, 3); a chest of furniture at (4, 3).
        private static InteriorLayout Room() {
            return new InteriorLayout( 0, 0, 5, 3, new[] { ( 4, 3 ) }, ( 1, 3 ) );
        }

        [ Test ]
        public void APiece_InsideTheRoom_CanBePlaced() {
            var room = Room();
            int id = room.Place( "Table", 2, 1, 2, 1, 0, false );
            Assert.Greater( id, 0 );
            Assert.AreEqual( 1, room.Pieces.Count );
            Assert.AreEqual( 2, room.PieceAt( 3, 1 )?.SizeX );
        }

        [ Test ]
        public void APiece_ThatLeavesTheRoom_IsRefused() {
            var room = Room();
            Assert.IsFalse( room.CanPlace( 5, 1, 2, 1, 0, false ) );
            Assert.IsFalse( room.CanPlace( -1, 0, 1, 1, 0, false ) );
            Assert.IsFalse( room.CanPlace( 0, 3, 1, 2, 0, false ) );
        }

        [ Test ]
        public void APiece_OnTheFurnitureOfTheHouse_IsRefused() {
            Assert.IsFalse( Room().CanPlace( 3, 3, 2, 1, 0, false ) );
        }

        [ Test ]
        public void TwoSolidPieces_CannotOverlap() {
            var room = Room();
            room.Place( "Table", 2, 1, 2, 1, 0, false );
            Assert.IsFalse( room.CanPlace( 3, 1, 1, 1, 0, false ) );
            Assert.IsTrue( room.CanPlace( 4, 1, 1, 1, 0, false ) );
        }

        [ Test ]
        public void ATurnedPiece_SwapsItsSides() {
            var room = Room();
            Assert.AreEqual( 0, room.Place( "Table", 4, 2, 2, 1, 1, false ), "a table of 2 x 1 turned is 1 x 2 and would reach into the furniture at (4, 3)" );
            int id = room.Place( "Table", 2, 0, 2, 1, 1, false );
            Assert.Greater( id, 0 );
            var piece = room.PieceAt( 2, 1 ).Value;
            Assert.AreEqual( 1, piece.SizeX );
            Assert.AreEqual( 2, piece.SizeZ );
        }

        [ Test ]
        public void ARug_LiesUnderFurniture_ButNotUnderAnotherRug() {
            var room = Room();
            room.Place( "Rug", 1, 0, 3, 2, 0, true );
            Assert.IsTrue( room.CanPlace( 2, 1, 1, 1, 0, false ), "a stool may stand on a rug" );
            Assert.IsFalse( room.CanPlace( 3, 1, 2, 2, 0, true ), "two rugs would overlap" );
            room.Place( "Stool", 2, 1, 1, 1, 0, false );
            Assert.AreEqual( "Stool", room.PieceAt( 2, 1 )?.Item, "furniture wins over the rug under it when a cell is asked" );
        }

        [ Test ]
        public void ARug_CannotCoverTheFurnitureOfTheHouse() {
            Assert.IsFalse( Room().CanPlace( 4, 2, 1, 2, 0, true ) );
        }

        [ Test ]
        public void TheEntryCell_StaysFree() {
            var room = Room();
            Assert.IsFalse( room.CanPlace( 1, 3, 1, 1, 0, false ) );
            Assert.IsFalse( room.CanPlace( 0, 3, 2, 1, 0, false ) );
            Assert.IsTrue( room.CanPlace( 1, 3, 1, 1, 0, true ), "a rug in front of the door is fine" );
        }

        [ Test ]
        public void FurnitureThatWallsOffAPartOfTheFloor_IsRefused() {
            // A wall of furniture across the whole room at x = 3 would cut the east part off.
            var room = new InteriorLayout( 0, 0, 4, 2, null, ( 0, 0 ) );
            Assert.IsTrue( room.Place( "Shelf", 3, 0, 1, 1, 0, false ) > 0 );
            Assert.IsTrue( room.Place( "Shelf", 3, 1, 1, 1, 0, false ) > 0 );
            Assert.IsFalse( room.CanPlace( 3, 2, 1, 1, 0, false ), "the last cell of the wall would shut the cells at x = 4 in" );
            Assert.IsFalse( room.CanPlace( 4, 2, 1, 1, 0, false ), "the cells (4, 0) and (4, 1) would be shut in as well" );
            Assert.IsTrue( room.CanPlace( 4, 0, 1, 1, 0, false ), "the way to (4, 1) and (4, 2) through (3, 2) stays open" );
        }

        [ Test ]
        public void FurnitureThatCoversAClosedOffPocket_IsStillAllowed() {
            // The pocket is already unreachable; covering it does not cut off anything new.
            var room = new InteriorLayout( 0, 0, 3, 0, new[] { ( 1, 0 ) }, ( 0, 0 ) );
            Assert.IsTrue( room.CanPlace( 2, 0, 2, 1, 0, false ) );
        }

        [ Test ]
        public void RemovingAPiece_FreesItsCells() {
            var room = Room();
            int id = room.Place( "Table", 2, 1, 2, 1, 0, false );
            Assert.IsTrue( room.Remove( id ) );
            Assert.IsNull( room.PieceAt( 2, 1 ) );
            Assert.IsTrue( room.CanPlace( 2, 1, 2, 1, 0, false ) );
            Assert.IsFalse( room.Remove( id ), "a second removal finds nothing" );
        }

        [ Test ]
        public void SolidCells_AreTheHouseFurnitureAndSolidPiecesButNoRugs() {
            var room = Room();
            room.Place( "Rug", 0, 0, 2, 2, 0, true );
            room.Place( "Table", 2, 1, 2, 1, 0, false );
            var cells = room.SolidCells().ToList();
            CollectionAssert.AreEquivalent( new[] { ( 4, 3 ), ( 2, 1 ), ( 3, 1 ) }, cells );
        }
    }
}
