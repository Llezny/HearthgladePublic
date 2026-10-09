using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class InteriorWalkAreaTests {

        // A room of 4 x 3 metres with one table in the middle, the player's pivot kept 0.1 m from everything.
        private static InteriorWalkArea Room( float margin = 0.1f ) {
            var room = new WalkRect( -2f, -1.5f, 2f, 1.5f );
            var table = new WalkRect( -0.5f, -0.3f, 0.5f, 0.3f );
            return new InteriorWalkArea( room, new[] { table }, margin );
        }

        [ Test ]
        public void FreeFloor_CanBeStoodOn() {
            Assert.IsTrue( Room().CanStand( 1f, 1f ) );
        }

        [ Test ]
        public void OutsideTheRoom_CannotBeStoodOn() {
            Assert.IsFalse( Room().CanStand( 2.5f, 0f ) );
            Assert.IsFalse( Room().CanStand( 0f, -1.6f ) );
        }

        [ Test ]
        public void TheMargin_KeepsThePivotAwayFromTheWalls() {
            var area = Room( 0.1f );
            Assert.IsTrue( area.CanStand( 1.89f, 0f ) );
            Assert.IsFalse( area.CanStand( 1.95f, 0f ) );
        }

        [ Test ]
        public void Furniture_BlocksTheSpotsInsideItAndTheMarginAroundIt() {
            var area = Room( 0.1f );
            Assert.IsFalse( area.CanStand( 0f, 0f ) );
            Assert.IsFalse( area.CanStand( 0.55f, 0f ) );
            Assert.IsTrue( area.CanStand( 0.65f, 0f ) );
        }

        [ Test ]
        public void AStep_IsAllowedOnlyWhenItEndsOnFreeFloor() {
            var area = Room();
            Assert.IsTrue( area.CanMoveTo( 1f, 0f ) );
            Assert.IsFalse( area.CanMoveTo( 0.2f, 0f ) );
        }

        [ Test ]
        public void APlayerInsideFurniture_CanStillWalkOut() {
            var area = Room();
            // The start spot is not looked at: only where the step ends.
            Assert.IsFalse( area.CanStand( 0f, 0f ) );
            Assert.IsTrue( area.CanMoveTo( 0.8f, 0f ) );
        }

        [ Test ]
        public void NoFurniture_LeavesTheWholeRoomFree() {
            var area = new InteriorWalkArea( new WalkRect( 0f, 0f, 1f, 1f ), null, 0f );
            Assert.IsTrue( area.CanStand( 0.5f, 0.5f ) );
        }

        [ Test ]
        public void ARectGivenBackwards_IsNormalised() {
            var rect = new WalkRect( 2f, 3f, -1f, -4f );
            Assert.AreEqual( -1f, rect.MinX );
            Assert.AreEqual( 2f, rect.MaxX );
            Assert.AreEqual( -4f, rect.MinZ );
            Assert.AreEqual( 3f, rect.MaxZ );
        }
    }
}
