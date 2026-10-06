using Hearthglade.Core.Entities;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ForageTests {

        private static readonly ItemId Wheat = new( "Wheat" );
        private static readonly ItemId Apple = new( "AppleTree" );

        private static readonly PlotKey WheatBed = new( 2, 0, 0 );
        private static readonly PlotKey AppleOrchard = new( 0, 0, 3 );

        // Wheat: ripe 30 minutes after planting, annual, grain. Apple: ripe fruit from minute 100, one per 60, fruit.
        private static CropDefinition WheatDef() => new CropDefinition(
            Wheat, CropLifecycle.Annual, PlotType.Bed, new[] { 10, 20 }, 3, 0, new ItemId( "WheatGrain" ), default, 0f, ForageKind.Grain );

        private static CropDefinition AppleDef() => new CropDefinition(
            Apple, CropLifecycle.Perennial, PlotType.Orchard, new[] { 40, 60 }, 4, 60, new ItemId( "Apple" ), default, 0f, ForageKind.Fruit );

        private static FarmModel NewFarm() {
            var farm = new FarmModel( new CropRegistry( new[] { WheatDef(), AppleDef() } ) );
            farm.AddPlot( WheatBed, PlotType.Bed );
            farm.AddPlot( AppleOrchard, PlotType.Orchard );
            farm.TryPlant( WheatBed, Wheat, 0 );
            farm.TryPlant( AppleOrchard, Apple, 0 );
            return farm;
        }

        private static ForageBrain NewBrain( out WanderBrain wander, ForageKind diet = ForageKind.Any, float chance = 1f, float radius = 10f ) {
            wander = new WanderBrain( 0f );
            return new ForageBrain( wander, new ForageSettings( diet, radius, 2f, chance, 60f, 120f ), new DeterministicRandom( 7 ) );
        }

        // --- the farm side ---

        [ Test ]
        public void FindTarget_ReturnsTheNearestRipePlotOfTheDiet( ) {
            var farm = NewFarm();
            Assert.IsTrue( farm.TryFindForageTarget( 0f, 0f, 10f, ForageKind.Any, 500, null, out var target ) );
            Assert.AreEqual( WheatBed, target, "the bed is 2 cells away, the orchard 3" );
            Assert.IsTrue( farm.TryFindForageTarget( 0f, 0f, 10f, ForageKind.Fruit, 500, null, out target ) );
            Assert.AreEqual( AppleOrchard, target, "only the apple tree is fruit" );
        }

        [ Test ]
        public void FindTarget_IgnoresUnripeFarAndNotAllowedPlots( ) {
            var farm = NewFarm();
            Assert.IsFalse( farm.TryFindForageTarget( 0f, 0f, 10f, ForageKind.Any, 5, null, out _ ), "nothing is ripe yet" );
            Assert.IsFalse( farm.TryFindForageTarget( 0f, 0f, 1f, ForageKind.Any, 500, null, out _ ), "everything is further than the radius" );
            Assert.IsFalse( farm.TryFindForageTarget( 0f, 0f, 10f, ForageKind.Berry, 500, null, out _ ), "no berries around" );
            Assert.IsTrue( farm.TryFindForageTarget( 0f, 0f, 10f, ForageKind.Any, 500, key => key != WheatBed, out var target ) );
            Assert.AreEqual( AppleOrchard, target, "the caller can rule a plot out" );
        }

        [ Test ]
        public void Eat_RaisesCropEaten_WithWhoAteWhat( ) {
            var farm = NewFarm();
            string seenCrop = null, seenEater = null;
            int seenCount = 0;
            farm.CropEaten += ( _, crop, count, eater ) => { seenCrop = crop; seenCount = count; seenEater = eater; };
            Assert.AreEqual( 1, farm.Eat( WheatBed, 500, 1, "Deer" ) );
            Assert.AreEqual( "Wheat", seenCrop );
            Assert.AreEqual( 1, seenCount );
            Assert.AreEqual( "Deer", seenEater );
        }

        // --- the brain ---

        [ Test ]
        public void Brain_WithoutADiet_NeverForages( ) {
            var brain = NewBrain( out var wander, ForageKind.None );
            Assert.IsFalse( brain.Enabled );
            Assert.IsFalse( brain.TryStartForaging( NewFarm(), 500, 0f, 0f ) );
            Assert.AreEqual( WanderState.Idle, wander.State );
        }

        [ Test ]
        public void Brain_ForageChanceDecidesWhetherItGoes( ) {
            var never = NewBrain( out _, chance: 0f );
            Assert.IsFalse( never.TryStartForaging( NewFarm(), 500, 0f, 0f ) );
            var always = NewBrain( out var wander, chance: 1f );
            Assert.IsTrue( always.TryStartForaging( NewFarm(), 500, 0f, 0f ) );
            Assert.AreEqual( WanderState.Seeking, wander.State );
            Assert.AreEqual( WheatBed, always.Target );
        }

        [ Test ]
        public void Brain_SeekArriveEat_TakesOnePieceAndRests( ) {
            var farm = NewFarm();
            var brain = NewBrain( out var wander );
            brain.TryStartForaging( farm, 500, 0f, 0f );

            brain.Arrived();
            Assert.AreEqual( WanderState.Eating, wander.State );
            Assert.IsFalse( brain.MealDone( 1f ) );
            Assert.IsTrue( brain.MealDone( 1.5f ) );

            int eaten = brain.FinishMeal( farm, 500, 4f, "Hen" );
            Assert.AreEqual( 1, eaten );
            Assert.AreEqual( WanderState.Idle, wander.State );
            Assert.IsNull( brain.Target );
            Assert.IsTrue( farm.Describe( WheatBed, 500 ).IsEmpty, "an annual eaten while ripe empties the bed" );
        }

        [ Test ]
        public void Brain_AfterAMeal_LeavesThePlotAloneForTheCooldown( ) {
            var farm = NewFarm();
            var brain = NewBrain( out var wander );
            brain.TryStartForaging( farm, 500, 0f, 0f );
            brain.Arrived();
            brain.FinishMeal( farm, 500, 0f, "Deer" );

            Assert.IsTrue( brain.IsOnCooldown( WheatBed ) );
            Assert.IsTrue( brain.TryStartForaging( farm, 500, 0f, 0f ) );
            Assert.AreEqual( AppleOrchard, brain.Target, "goes for the next plot instead" );

            brain.Update( 61f );
            Assert.IsFalse( brain.IsOnCooldown( WheatBed ), "the cooldown runs out" );
        }

        [ Test ]
        public void Brain_GivingUp_BlocksThatPlotForLonger( ) {
            var farm = NewFarm();
            var brain = NewBrain( out var wander );
            brain.TryStartForaging( farm, 500, 0f, 0f );
            brain.GiveUp( 5f );

            Assert.AreEqual( WanderState.Idle, wander.State );
            Assert.IsTrue( brain.IsOnCooldown( WheatBed ) );
            brain.Update( 61f );
            Assert.IsTrue( brain.IsOnCooldown( WheatBed ), "a blocked plot is ignored longer than one just eaten from" );
            brain.Update( 60f );
            Assert.IsFalse( brain.IsOnCooldown( WheatBed ) );
        }

        [ Test ]
        public void Brain_FoodThatIsGoneBeforeTheMeal_GivesNothing( ) {
            var farm = NewFarm();
            var brain = NewBrain( out var wander );
            brain.TryStartForaging( farm, 500, 0f, 0f );
            farm.TryHarvest( WheatBed, 500, new DeterministicRandom( 1 ) );
            brain.Arrived();
            Assert.AreEqual( 0, brain.FinishMeal( farm, 500, 3f, "Hen" ) );
            Assert.AreEqual( WanderState.Idle, wander.State );
        }

        [ Test ]
        public void Interrupt_ForgetsTheTargetWithoutACooldown( ) {
            var farm = NewFarm();
            var brain = NewBrain( out var wander );
            brain.TryStartForaging( farm, 500, 0f, 0f );
            wander.StartRunning();
            brain.Interrupt();
            Assert.IsNull( brain.Target );
            Assert.IsFalse( brain.IsOnCooldown( WheatBed ) );
            Assert.IsFalse( wander.FinishedEating( 10f ), "a running animal is not eating" );
        }
    }
}
