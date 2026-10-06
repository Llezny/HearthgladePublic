using System;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class BuildFloorGridTests {

        [ Test ]
        public void EmptyGrid_IsFreeEverywhere( ) {
            var grid = new BuildFloorGrid( );
            Assert.IsTrue( grid.CanPlace( 0, 0 ) );
            Assert.IsFalse( grid.Has( 0, 0 ) );
        }

        [ Test ]
        public void Place_OccupiesOnlyThatExactCell( ) {
            var grid = new BuildFloorGrid( );
            grid.Place( 1, 2, 3 );
            Assert.IsTrue( grid.Has( 2, 3 ) );
            Assert.IsFalse( grid.Has( 2, 4 ), "a different cell must stay free" );
            Assert.IsFalse( grid.Has( 3, 3 ), "a different cell must stay free" );
        }

        [ Test ]
        public void CanPlace_IsFalseOnOverlap_AndTrueOnceTheOverlappingPieceIsRemoved( ) {
            var grid = new BuildFloorGrid( );
            grid.Place( 1, 0, 0 );

            Assert.IsFalse( grid.CanPlace( 0, 0 ) );
            grid.Remove( 1 );
            Assert.IsTrue( grid.CanPlace( 0, 0 ) );
        }

        [ Test ]
        public void Place_Throws_WhenCellIsAlreadyTaken( ) {
            var grid = new BuildFloorGrid( );
            grid.Place( 1, 0, 0 );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 2, 0, 0 ) );
        }

        [ Test ]
        public void Place_Throws_WhenTheSamePieceIdIsPlacedTwice( ) {
            var grid = new BuildFloorGrid( );
            grid.Place( 1, 0, 0 );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 1, 5, 5 ) );
        }

        [ Test ]
        public void Remove_UnknownPieceId_IsANoOp( ) {
            var grid = new BuildFloorGrid( );
            Assert.DoesNotThrow( ( ) => grid.Remove( 42 ) );
        }

        [ Test ]
        public void CellOf_ReturnsTheCellOfAPlacedPiece_AndNullOtherwise( ) {
            var grid = new BuildFloorGrid( );
            grid.Place( 7, 4, 9 );

            var found = grid.CellOf( 7 );
            Assert.IsTrue( found.HasValue );
            Assert.AreEqual( ( 4, 9 ), found.Value );
            Assert.IsNull( grid.CellOf( 99 ) );
        }
    }
}
