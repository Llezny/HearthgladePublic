using Hearthglade.Core.Entities;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class EntityTests {

        [Test]
        public void Lod_ActivatesNearFreezesInBetweenAndDespawnsFar() {
            Assert.AreEqual( EntityLodAction.Activate, EntityLod.Decide( 3f, false, 4f, 7f ) );
            Assert.AreEqual( EntityLodAction.None, EntityLod.Decide( 3f, true, 4f, 7f ) );
            Assert.AreEqual( EntityLodAction.Deactivate, EntityLod.Decide( 5f, true, 4f, 7f ) );
            Assert.AreEqual( EntityLodAction.None, EntityLod.Decide( 5f, false, 4f, 7f ) );
            Assert.AreEqual( EntityLodAction.Despawn, EntityLod.Decide( 8f, true, 4f, 7f ) );
            Assert.AreEqual( EntityLodAction.Despawn, EntityLod.Decide( 8f, false, 4f, 7f ) );
        }

        [Test]
        public void Quota_AllowsTheFirstSpawnAtOnceThenWaitsForTheCooldown() {
            var quota = new SpawnQuota( 2, 5f );
            Assert.IsTrue( quota.CanSpawn( 0 ) );
            quota.Spawned();
            Assert.IsFalse( quota.CanSpawn( 1 ), "cooling down" );
            quota.Advance( 4f );
            Assert.IsFalse( quota.CanSpawn( 1 ) );
            quota.Advance( 1f );
            Assert.IsTrue( quota.CanSpawn( 1 ) );
        }

        [Test]
        public void Quota_StopsAtTheLimitEvenWhenNotCoolingDown() {
            var quota = new SpawnQuota( 2, 0f );
            Assert.IsTrue( quota.CanSpawn( 1 ) );
            Assert.IsFalse( quota.CanSpawn( 2 ) );
        }

        [Test]
        public void Brain_RestsThenWalksThenRestsAgain() {
            var brain = new WanderBrain( 2f );
            Assert.IsFalse( brain.WantsToWalk( 1f ) );
            Assert.IsTrue( brain.WantsToWalk( 1.5f ) );
            brain.StartWalking();
            Assert.AreEqual( WanderState.Walking, brain.State );
            Assert.IsFalse( brain.WantsToWalk( 10f ), "a walking animal does not pick a new destination" );
            brain.ReachedDestination( 3f );
            Assert.AreEqual( WanderState.Idle, brain.State );
            Assert.IsFalse( brain.WantsToWalk( 2f ) );
            Assert.IsTrue( brain.WantsToWalk( 1f ) );
        }

        [Test]
        public void Brain_RunsWhateverItWasDoing() {
            var brain = new WanderBrain( 100f );
            brain.StartRunning();
            Assert.AreEqual( WanderState.Running, brain.State );
            brain.ReachedDestination( 1f );
            Assert.AreEqual( WanderState.Idle, brain.State );
        }

        [Test]
        public void SpawnTime_DayEntitiesWaitForTheDayAndNightEntitiesForTheNight() {
            Assert.IsTrue( SpawnTimeRules.Allows( SpawnTime.Always, true ) );
            Assert.IsTrue( SpawnTimeRules.Allows( SpawnTime.Always, false ) );
            Assert.IsTrue( SpawnTimeRules.Allows( SpawnTime.Day, true ) );
            Assert.IsFalse( SpawnTimeRules.Allows( SpawnTime.Day, false ) );
            Assert.IsTrue( SpawnTimeRules.Allows( SpawnTime.Night, false ) );
            Assert.IsFalse( SpawnTimeRules.Allows( SpawnTime.Night, true ) );
        }

        [Test]
        public void Threat_NeedsAnAlertDistanceAndSomethingInsideIt() {
            Assert.IsTrue( WanderBrain.IsThreatened( 0.5f, 0.9f ) );
            Assert.IsFalse( WanderBrain.IsThreatened( 1.5f, 0.9f ) );
            Assert.IsFalse( WanderBrain.IsThreatened( 0.1f, 0f ), "an animal without alert distance is never scared" );
        }
    }
}
