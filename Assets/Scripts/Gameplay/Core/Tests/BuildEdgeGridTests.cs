using System;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class BuildEdgeGridTests {

        [ Test ]
        public void EmptyGrid_IsFreeEverywhere( ) {
            var grid = new BuildEdgeGrid( );
            Assert.IsTrue( grid.CanPlace( new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1 ) ) );
            Assert.IsFalse( grid.IsOccupied( 0, 0, 0, EdgeSide.PlusX ) );
            Assert.AreEqual( 0, grid.TopY( 0, 0, EdgeSide.PlusX ) );
        }

        [ Test ]
        public void Place_OccupiesEveryLayerOfTheBox( ) {
            var grid = new BuildEdgeGrid( );
            var box = new BuildEdgeBox( 1, 2, 0, EdgeSide.PlusZ, 2 ); // a 2-tall wall
            grid.Place( 1, box );

            foreach( var y in box.YLayers( ) ) {
                Assert.IsTrue( grid.IsOccupied( 1, 2, y, EdgeSide.PlusZ ), $"layer {y} should be occupied" );
            }
            Assert.IsFalse( grid.IsOccupied( 1, 2, 0, EdgeSide.PlusX ), "a different side of the same cell must stay free" );
            Assert.IsFalse( grid.IsOccupied( 1, 3, 0, EdgeSide.PlusZ ), "a different edge must stay free" );
        }

        [ Test ]
        public void StraightRun_And_Corner_DoNotOverlap( ) {
            var grid = new BuildEdgeGrid( );
            // Two walls in a straight line along X: the edge between (0,0)/(0,1) and (1,0)/(1,1), both PlusZ.
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusZ, 2 ) );
            Assert.IsTrue( grid.CanPlace( new BuildEdgeBox( 1, 0, 0, EdgeSide.PlusZ, 2 ) ), "next cell along the run is free" );

            // A perpendicular wall meeting it at the corner cell (0,0): PlusX of (0,0) is a different edge.
            Assert.IsTrue( grid.CanPlace( new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 2 ) ), "corner edge is independent of the PlusZ edge" );
        }

        [ Test ]
        public void CanPlace_IsFalseOnOverlap_AndTrueOnceTheOverlappingPieceIsRemoved( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 2 ) );

            Assert.IsFalse( grid.CanPlace( new BuildEdgeBox( 0, 0, 1, EdgeSide.PlusX, 1 ) ), "overlaps the top layer of piece 1" );
            Assert.IsTrue( grid.CanPlace( new BuildEdgeBox( 0, 0, 2, EdgeSide.PlusX, 1 ) ), "stacked above piece 1, not overlapping" );

            grid.Remove( 1 );
            Assert.IsTrue( grid.CanPlace( new BuildEdgeBox( 0, 0, 1, EdgeSide.PlusX, 1 ) ) );
        }

        [ Test ]
        public void Place_Throws_WhenLayersAreAlreadyTaken( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1 ) );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 2, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1 ) ) );
        }

        [ Test ]
        public void Place_Throws_WhenTheSamePieceIdIsPlacedTwice( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1 ) );
            Assert.Throws<InvalidOperationException>( ( ) => grid.Place( 1, new BuildEdgeBox( 5, 5, 0, EdgeSide.PlusZ, 1 ) ) );
        }

        [ Test ]
        public void Remove_UnknownPieceId_IsANoOp( ) {
            var grid = new BuildEdgeGrid( );
            Assert.DoesNotThrow( ( ) => grid.Remove( 42 ) );
        }

        [ Test ]
        public void TopY_IsTheHighestBoxTopOnThatExactEdge( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 2 ) );
            Assert.AreEqual( 2, grid.TopY( 0, 0, EdgeSide.PlusX ) );
            Assert.AreEqual( 0, grid.TopY( 0, 0, EdgeSide.PlusZ ), "a different side of the same cell" );

            // A second wall stacked on top of the first.
            grid.Place( 2, new BuildEdgeBox( 0, 0, 2, EdgeSide.PlusX, 1 ) );
            Assert.AreEqual( 3, grid.TopY( 0, 0, EdgeSide.PlusX ) );
        }

        [ Test ]
        public void BoxOf_ReturnsTheBoxOfAPlacedPiece_AndNullOtherwise( ) {
            var grid = new BuildEdgeGrid( );
            var box = new BuildEdgeBox( 4, 4, 0, EdgeSide.PlusZ, 2 );
            grid.Place( 7, box );

            var found = grid.BoxOf( 7 );
            Assert.IsTrue( found.HasValue );
            Assert.AreEqual( box.X, found.Value.X );
            Assert.AreEqual( box.TopY, found.Value.TopY );
            Assert.IsNull( grid.BoxOf( 99 ) );
        }

        [ Test ]
        public void YLayers_EnumeratesExactlyTheBoxHeight( ) {
            var box = new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 3 );
            Assert.AreEqual( 3, box.YLayers( ).Count( ) );
            Assert.AreEqual( 3, box.YLayers( ).Distinct( ).Count( ), "no duplicate layers" );
        }

        [ Test ]
        public void Constructor_RejectsNonPositiveSizeY( ) {
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 0 ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, -1 ) );
        }

        [ Test ]
        public void Canonicalize_NamesTheEdgeFromItsLowerCoordinateCell( ) {
            Assert.AreEqual( ( 3, 5, EdgeSide.PlusX ), BuildEdgeGrid.Canonicalize( 3, 5, 1, 0 ) );
            Assert.AreEqual( ( 2, 5, EdgeSide.PlusX ), BuildEdgeGrid.Canonicalize( 3, 5, -1, 0 ), "the same edge, named from the other cell" );
            Assert.AreEqual( ( 3, 5, EdgeSide.PlusZ ), BuildEdgeGrid.Canonicalize( 3, 5, 0, 1 ) );
            Assert.AreEqual( ( 3, 4, EdgeSide.PlusZ ), BuildEdgeGrid.Canonicalize( 3, 5, 0, -1 ), "the same edge, named from the other cell" );
        }

        [ Test ]
        public void Fence_OccupiesTheEdgeButIsNotAWall( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1, EdgeKind.Fence ) );
            grid.Place( 2, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusZ, 1 ) );

            Assert.IsTrue( grid.IsOccupied( 0, 0, 0, EdgeSide.PlusX ), "a fence blocks movement and placement" );
            Assert.IsFalse( grid.IsWallOccupied( 0, 0, 0, EdgeSide.PlusX ), "but never closes a room" );
            Assert.IsTrue( grid.IsWallOccupied( 0, 0, 0, EdgeSide.PlusZ ), "a plain box is a wall" );
            Assert.IsFalse( grid.IsWallOccupied( 5, 5, 0, EdgeSide.PlusX ), "an empty edge is no wall" );
            Assert.AreEqual( EdgeKind.Fence, grid.BoxOf( 1 ).Value.Kind );
            Assert.AreEqual( EdgeKind.Wall, grid.BoxOf( 2 ).Value.Kind );
        }

        [ Test ]
        public void OpenGate_IsPassable_ButStillOccupiesItsEdge( ) {
            var grid = new BuildEdgeGrid( );
            grid.Place( 1, new BuildEdgeBox( 0, 0, 0, EdgeSide.PlusX, 1, EdgeKind.Fence ) );
            Assert.IsFalse( grid.IsPassable( 0, 0, 0, EdgeSide.PlusX ), "a closed gate blocks" );
            grid.SetOpen( 1, true );
            Assert.IsTrue( grid.IsPassable( 0, 0, 0, EdgeSide.PlusX ) );
            Assert.IsTrue( grid.IsOccupied( 0, 0, 0, EdgeSide.PlusX ), "nothing else can be built on an open gate" );
        }

        [ Test ]
        public void BuildCategory_FencesAndGatesAreEdgePiecesOfKindFence( ) {
            foreach( var category in new[] { BuildCategory.Wall, BuildCategory.Door, BuildCategory.Fence, BuildCategory.Gate } ) {
                Assert.IsTrue( category.IsEdgePiece( ), category.ToString( ) );
            }
            foreach( var category in new[] { BuildCategory.Floor, BuildCategory.Furniture, BuildCategory.Decoration } ) {
                Assert.IsFalse( category.IsEdgePiece( ), category.ToString( ) );
            }
            Assert.AreEqual( EdgeKind.Wall, BuildCategory.Door.ToEdgeKind( ) );
            Assert.AreEqual( EdgeKind.Fence, BuildCategory.Fence.ToEdgeKind( ) );
            Assert.AreEqual( EdgeKind.Fence, BuildCategory.Gate.ToEdgeKind( ) );
        }

        [ Test ]
        public void Canonicalize_Throws_OnNonCardinalStep( ) {
            Assert.Throws<ArgumentException>( ( ) => BuildEdgeGrid.Canonicalize( 0, 0, 1, 1 ) );
            Assert.Throws<ArgumentException>( ( ) => BuildEdgeGrid.Canonicalize( 0, 0, 0, 0 ) );
        }
    }
}
