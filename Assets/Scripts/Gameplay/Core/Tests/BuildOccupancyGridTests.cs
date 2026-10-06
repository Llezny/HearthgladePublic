using System;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class BuildOccupancyGridTests {

        [ Test ]
        public void EmptyGrid_IsFreeEverywhere( ) {
            var grid = new BuildOccupancyGrid( );
            Assert.IsTrue( grid.CanPlace( new BuildCellBox( 0, 0, 0, 1, 1, 1 ) ) );
            Assert.IsFalse( grid.IsOccupied( 0, 0, 0 ) );
            Assert.AreEqual( 0, grid.TopY( 0, 0 ) );
        }

        [ Test ]
        public void Place_OccupiesEveryCellOfTheBox( ) {
            var grid = new BuildOccupancyGrid( );
            var box = new BuildCellBox( 1, 0, 2, 3, 2, 1 ); // e.g. a table footprint
            grid.Place( 1, box );

            foreach( var ( x, y, z ) in box.Cells( ) ) {
                Assert.IsTrue( grid.IsOccupied( x, y, z ), $"({x},{y},{z}) should be occupied" );
            }
            Assert.IsFalse( grid.IsOccupied( 0, 0, 2 ), "outside the box must stay free" );
            Assert.IsFalse( grid.IsOccupied( 1, 2, 2 ), "one above the box must stay free" );
        }

        [ Test ]
        public void CanPlace_IsFalseOnOverlap_AndTrueOnceTheOverlappingPieceIsRemoved( ) {
            var grid = new BuildOccupancyGrid( );
            grid.Place( 1, new BuildCellBox( 0, 0, 0, 2, 2, 2 ) );

            Assert.IsFalse( grid.CanPlace( new BuildCellBox( 1, 0, 1, 2, 2, 2 ) ), "overlaps piece 1 by one cell" );
            Assert.IsTrue( grid.CanPlace( new BuildCellBox( 2, 0, 0, 1, 1, 1 ) ), "adjacent, not overlapping" );

            grid.Remove( 1 );
            Assert.IsTrue( grid.CanPlace( new BuildCellBox( 1, 0, 1, 2, 2, 2 ) ) );
        }

        [ Test ]
        public void Place_Throws_WhenCellsAreAlreadyTaken( ) {
            var grid = new BuildOccupancyGrid( );
            grid.Place( 1, new BuildCellBox( 0, 0, 0, 1, 1, 1 ) );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 2, new BuildCellBox( 0, 0, 0, 1, 1, 1 ) ) );
        }

        [ Test ]
        public void Place_Throws_WhenTheSamePieceIdIsPlacedTwice( ) {
            var grid = new BuildOccupancyGrid( );
            grid.Place( 1, new BuildCellBox( 0, 0, 0, 1, 1, 1 ) );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 1, new BuildCellBox( 5, 0, 5, 1, 1, 1 ) ) );
        }

        [ Test ]
        public void Remove_UnknownPieceId_IsANoOp( ) {
            var grid = new BuildOccupancyGrid( );
            Assert.DoesNotThrow( ( ) => grid.Remove( 42 ) );
        }

        [ Test ]
        public void TopY_IsTheHighestBoxTopCoveringThatColumn( ) {
            var grid = new BuildOccupancyGrid( );
            // A table: footprint 3x2 (x,z), one cell tall, sitting on the ground.
            grid.Place( 1, new BuildCellBox( 0, 0, 0, 3, 1, 2 ) );
            Assert.AreEqual( 1, grid.TopY( 0, 0 ) );
            Assert.AreEqual( 1, grid.TopY( 2, 1 ), "still inside the table's footprint" );
            Assert.AreEqual( 0, grid.TopY( 3, 0 ), "outside the footprint" );

            // A bowl placed on top of the table.
            grid.Place( 2, new BuildCellBox( 1, 1, 0, 1, 1, 1 ) );
            Assert.AreEqual( 2, grid.TopY( 1, 0 ) );
            Assert.AreEqual( 1, grid.TopY( 0, 0 ), "no bowl over this cell, just the table" );
        }

        [ Test ]
        public void SupportY_IsTheCommonTopYAcrossTheFootprint( ) {
            var grid = new BuildOccupancyGrid( );
            Assert.AreEqual( 0, grid.SupportY( 0, 0, 1, 1 ), "bare ground everywhere" );

            // A table: footprint 3x2 (x,z), one cell tall.
            grid.Place( 1, new BuildCellBox( 0, 0, 0, 3, 1, 2 ) );
            Assert.AreEqual( 1, grid.SupportY( 0, 0, 3, 2 ), "a footprint that matches the table exactly" );
            Assert.AreEqual( 1, grid.SupportY( 1, 0, 1, 1 ), "a single cell inside the table" );

            // Half on the table, half on bare ground next to it: no single support height.
            Assert.IsNull( grid.SupportY( 2, 0, 2, 1 ) );
        }

        [ Test ]
        public void BoxOf_ReturnsTheBoxOfAPlacedPiece_AndNullOtherwise( ) {
            var grid = new BuildOccupancyGrid( );
            var box = new BuildCellBox( 4, 0, 4, 1, 2, 1 );
            grid.Place( 7, box );

            var found = grid.BoxOf( 7 );
            Assert.IsTrue( found.HasValue );
            Assert.AreEqual( box.X, found.Value.X );
            Assert.AreEqual( box.TopY, found.Value.TopY );
            Assert.IsNull( grid.BoxOf( 99 ) );
        }

        [ Test ]
        public void Cells_EnumeratesExactlyTheBoxVolume( ) {
            var box = new BuildCellBox( 0, 0, 0, 2, 3, 1 );
            Assert.AreEqual( 2 * 3 * 1, box.Cells( ).Count( ) );
            Assert.AreEqual( 2 * 3 * 1, box.Cells( ).Distinct( ).Count( ), "no duplicate cells" );
        }

        [ Test ]
        public void Constructor_RejectsNonPositiveSize( ) {
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new BuildCellBox( 0, 0, 0, 0, 1, 1 ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new BuildCellBox( 0, 0, 0, 1, -1, 1 ) );
        }
    }
}
