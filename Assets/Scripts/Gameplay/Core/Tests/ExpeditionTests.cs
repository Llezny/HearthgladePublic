using Hearthglade.Core.Expedition;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ExpeditionTests {

        [Test]
        public void NewProgress_OpensOnlyDepthOneInEveryDirection() {
            var progress = new ExpeditionProgress();
            foreach( Direction direction in System.Enum.GetValues( typeof( Direction ) ) ) {
                Assert.IsTrue( progress.CanSail( new ExpeditionTarget( direction, 1 ) ) );
                Assert.IsFalse( progress.CanSail( new ExpeditionTarget( direction, 2 ) ) );
            }
        }

        [Test]
        public void Complete_OpensTheNextDepthOfThatDirectionOnly() {
            var progress = new ExpeditionProgress();
            Assert.IsTrue( progress.Complete( new ExpeditionTarget( Direction.North, 1 ) ) );
            Assert.IsTrue( progress.CanSail( new ExpeditionTarget( Direction.North, 2 ) ) );
            Assert.IsFalse( progress.CanSail( new ExpeditionTarget( Direction.East, 2 ) ) );
        }

        [Test]
        public void Complete_AnAlreadyDoneDepthIsNoRecordAndDeeperIsRefused() {
            var progress = new ExpeditionProgress();
            progress.Complete( new ExpeditionTarget( Direction.West, 1 ) );
            Assert.IsFalse( progress.Complete( new ExpeditionTarget( Direction.West, 1 ) ) );
            Assert.IsFalse( progress.Complete( new ExpeditionTarget( Direction.West, 3 ) ) );
            Assert.AreEqual( 1, progress.MaxDepthReached( Direction.West ) );
        }

        [Test]
        public void ShallowerDepthsStaySailable() {
            var progress = new ExpeditionProgress();
            for( int depth = 1; depth <= 3; depth++ ) {
                progress.Complete( new ExpeditionTarget( Direction.South, depth ) );
            }
            Assert.IsTrue( progress.CanSail( new ExpeditionTarget( Direction.South, 1 ) ) );
            Assert.IsTrue( progress.CanSail( new ExpeditionTarget( Direction.South, 4 ) ) );
            Assert.IsFalse( progress.CanSail( new ExpeditionTarget( Direction.South, 5 ) ) );
            Assert.IsFalse( progress.CanSail( new ExpeditionTarget( Direction.South, 0 ) ) );
        }

        [Test]
        public void Depth_StopsAtTheCap() {
            var progress = new ExpeditionProgress();
            for( int depth = 1; depth <= ExpeditionProgress.DepthCap; depth++ ) {
                progress.Complete( new ExpeditionTarget( Direction.East, depth ) );
            }
            Assert.AreEqual( ExpeditionProgress.DepthCap, progress.NextDepth( Direction.East ) );
            Assert.IsFalse( progress.CanSail( new ExpeditionTarget( Direction.East, ExpeditionProgress.DepthCap + 1 ) ) );
        }

        [Test]
        public void Save_RoundTripsAndAnOldSaveMeansNothingSailed() {
            var progress = new ExpeditionProgress();
            progress.Complete( new ExpeditionTarget( Direction.North, 1 ) );
            progress.Complete( new ExpeditionTarget( Direction.North, 2 ) );
            progress.Complete( new ExpeditionTarget( Direction.West, 1 ) );

            var loaded = new ExpeditionProgress();
            loaded.Load( progress.ToData() );
            Assert.AreEqual( 2, loaded.MaxDepthReached( Direction.North ) );
            Assert.AreEqual( 1, loaded.MaxDepthReached( Direction.West ) );
            Assert.AreEqual( 0, loaded.MaxDepthReached( Direction.South ) );

            loaded.Load( null );
            Assert.AreEqual( 0, loaded.MaxDepthReached( Direction.North ) );
            loaded.Load( new ExpeditionProgressData { MaxDepth = new[] { 3, 99 } } );
            Assert.AreEqual( 3, loaded.MaxDepthReached( Direction.North ) );
            Assert.AreEqual( ExpeditionProgress.DepthCap, loaded.MaxDepthReached( Direction.East ) );
            Assert.AreEqual( 0, loaded.MaxDepthReached( Direction.West ) );
        }

        [Test]
        public void Climate_PushesEachDirectionTheAgreedWayAndGrowsWithDepth() {
            ExpeditionClimate.Offsets( new ExpeditionTarget( Direction.North, 2 ), out double nt, out double nh );
            Assert.Less( nt, 0 );
            Assert.AreEqual( 0, nh );
            ExpeditionClimate.Offsets( new ExpeditionTarget( Direction.South, 2 ), out double st, out double sh );
            Assert.Greater( st, 0 );
            Assert.Less( sh, 0 );
            ExpeditionClimate.Offsets( new ExpeditionTarget( Direction.East, 2 ), out _, out double eh );
            Assert.Greater( eh, 0 );
            ExpeditionClimate.Offsets( new ExpeditionTarget( Direction.West, 2 ), out _, out double wh );
            Assert.Less( wh, 0 );

            ExpeditionClimate.Offsets( new ExpeditionTarget( Direction.North, 4 ), out double deeper, out _ );
            Assert.Less( deeper, nt );
        }

        [Test]
        public void Climate_NeverLeavesTheEqualisedRange() {
            foreach( Direction direction in System.Enum.GetValues( typeof( Direction ) ) ) {
                ExpeditionClimate.Offsets( new ExpeditionTarget( direction, 99 ), out double t, out double h );
                Assert.That( t, Is.InRange( -1.0, 1.0 ) );
                Assert.That( h, Is.InRange( -1.0, 1.0 ) );
            }
        }
    }
}
