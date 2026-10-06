using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class GridFloodCardinalTests {

        [ Test ]
        public void CountReachableCardinal_NeverStepsDiagonally( ) {
            // Everything is "passable". The cap is checked only between dequeues, so the start cell's own
            // expansion always finishes first: exactly its 4 cardinal neighbours get added (5 total), never
            // the diagonal (1,1)/(1,-1)/etc - an 8-connected fill would have added those too and reached 9.
            static bool CanMove( int x, int y, int nx, int ny ) => true;
            int reached = GridFlood.CountReachableCardinal( 0, 0, CanMove, limit: 1 );
            Assert.AreEqual( 5, reached, "start cell plus its 4 cardinal neighbours, no diagonals" );
        }

        [ Test ]
        public void CountReachableCardinal_StopsAtABlockedEdge( ) {
            // A single wall directly east of the start: every other cardinal direction stays open.
            static bool CanMove( int x, int y, int nx, int ny ) => !( x == 0 && y == 0 && nx == 1 && ny == 0 );
            int reached = GridFlood.CountReachableCardinal( 0, 0, CanMove, limit: 1 );
            Assert.AreEqual( 4, reached, "start plus the three open neighbours, but not the blocked east one" );
        }

        [ Test ]
        public void CountReachableCardinal_CountsTheStartCellEvenWithNoMoves( ) {
            static bool CanMove( int x, int y, int nx, int ny ) => false;
            Assert.AreEqual( 1, GridFlood.CountReachableCardinal( 5, 5, CanMove, limit: 500 ) );
        }
    }
}
