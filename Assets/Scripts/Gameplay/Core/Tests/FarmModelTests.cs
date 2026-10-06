using System;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class FarmModelTests {

        private const int Day = 1440;

        private static readonly ItemId Wheat = new( "Wheat" );
        private static readonly ItemId Apple = new( "AppleTree" );

        private static readonly PlotKey Bed = new( 1, 0, 2 );
        private static readonly PlotKey Orchard = new( 5, 0, 5 );

        private static CropDefinition WheatDef( float seedChance = 0f ) => new CropDefinition(
            Wheat, CropLifecycle.Annual, PlotType.Bed, new[] { 10, 20 }, 3, 0,
            new ItemId( "WheatGrain" ), new ItemId( "WheatSeed" ), seedChance
        );

        // 100 minutes to mature, then one fruit per 60 minutes, at most 4.
        private static CropDefinition AppleDef() => new CropDefinition(
            Apple, CropLifecycle.Perennial, PlotType.Orchard, new[] { 40, 60 }, 4, 60,
            new ItemId( "Apple" ), new ItemId( "AppleSapling" ), 0f
        );

        private static FarmModel NewFarm( float seedChance = 0f ) {
            var farm = new FarmModel( new CropRegistry( new[] { WheatDef( seedChance ), AppleDef() } ) );
            farm.AddPlot( Bed, PlotType.Bed );
            farm.AddPlot( Orchard, PlotType.Orchard );
            return farm;
        }

        // --- plots and planting ---

        [ Test ]
        public void AddPlot_RejectsDuplicates_AndEnsurePlotKeepsTheExistingOne( ) {
            var farm = NewFarm();
            Assert.IsFalse( farm.AddPlot( Bed, PlotType.Orchard ) );
            farm.EnsurePlot( Bed, PlotType.Orchard );
            Assert.AreEqual( PlotType.Bed, farm.Describe( Bed, 0 ).Type );
            Assert.AreEqual( 2, farm.PlotCount );
        }

        [ Test ]
        public void Describe_UnknownPlot_DoesNotExist( ) {
            Assert.IsFalse( NewFarm().Describe( new PlotKey( 9, 9, 9 ), 0 ).Exists );
        }

        [ Test ]
        public void TryPlant_ChecksPlotCropAndPlace( ) {
            var farm = NewFarm();
            Assert.AreEqual( PlantResult.NoPlot, farm.TryPlant( new PlotKey( 9, 9, 9 ), Wheat, 0 ) );
            Assert.AreEqual( PlantResult.UnknownCrop, farm.TryPlant( Bed, new ItemId( "Nope" ), 0 ) );
            Assert.AreEqual( PlantResult.WrongPlotType, farm.TryPlant( Orchard, Wheat, 0 ) );
            Assert.AreEqual( PlantResult.Planted, farm.TryPlant( Bed, Wheat, 0 ) );
            Assert.AreEqual( PlantResult.Occupied, farm.TryPlant( Bed, Wheat, 0 ) );
        }

        [ Test ]
        public void RemovePlot_DropsItsCrop( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.IsTrue( farm.RemovePlot( Bed ) );
            Assert.IsFalse( farm.Describe( Bed, 0 ).Exists );
            Assert.IsFalse( farm.RemovePlot( Bed ) );
        }

        // --- growth ---

        [ Test ]
        public void StageBoundaries_AreExact( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 100 );

            Assert.AreEqual( 0, farm.Describe( Bed, 100 ).Stage );
            Assert.AreEqual( 0, farm.Describe( Bed, 109 ).Stage );
            Assert.AreEqual( 1, farm.Describe( Bed, 110 ).Stage );
            Assert.AreEqual( 1, farm.Describe( Bed, 129 ).Stage );
            Assert.AreEqual( 2, farm.Describe( Bed, 130 ).Stage );
            Assert.AreEqual( 3, farm.Describe( Bed, 130 ).StageCount );
        }

        [ Test ]
        public void Progress_RunsFromZeroToOneInsideAStage( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 0f, farm.Describe( Bed, 0 ).Progress, 1e-4f );
            Assert.AreEqual( 0.5f, farm.Describe( Bed, 5 ).Progress, 1e-4f );
            Assert.AreEqual( 0.5f, farm.Describe( Bed, 20 ).Progress, 1e-4f );
            Assert.AreEqual( 1f, farm.Describe( Bed, 500 ).Progress, 1e-4f );
        }

        [ Test ]
        public void Annual_IsOnlyRipeWhenMature( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 0, farm.Describe( Bed, 29 ).ReadyYield );
            Assert.AreEqual( 3, farm.Describe( Bed, 30 ).ReadyYield );
        }

        [ Test ]
        public void ClockGoingBackwards_NeverGivesNegativeValues( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 1000 );
            var view = farm.Describe( Orchard, 0 );
            Assert.AreEqual( 0, view.Stage );
            Assert.AreEqual( 0f, view.Progress );
            Assert.AreEqual( 0, view.ReadyYield );
        }

        [ Test ]
        public void LongAbsence_CatchesUpInOneStep( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            farm.TryPlant( Orchard, Apple, 0 );

            var wheat = farm.Describe( Bed, 100 * Day );
            Assert.AreEqual( 2, wheat.Stage );
            Assert.AreEqual( 3, wheat.ReadyYield );

            var apple = farm.Describe( Orchard, 100 * Day );
            Assert.AreEqual( 2, apple.Stage );
            Assert.AreEqual( 4, apple.ReadyYield, "fruit is capped at MaxYield" );
        }

        // --- perennial fruit ---

        [ Test ]
        public void Perennial_FruitRipensOneByOneAfterMaturity( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 0 );
            Assert.AreEqual( 0, farm.Describe( Orchard, 159 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 160 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 219 ).ReadyYield );
            Assert.AreEqual( 2, farm.Describe( Orchard, 220 ).ReadyYield );
            Assert.AreEqual( 4, farm.Describe( Orchard, 340 ).ReadyYield );
            Assert.AreEqual( 4, farm.Describe( Orchard, 10000 ).ReadyYield );
        }

        [ Test ]
        public void Perennial_HarvestKeepsThePlantAndProgressOfTheNextFruit( ) {
            var farm = NewFarm();
            var rng = new DeterministicRandom( 1 );
            farm.TryPlant( Orchard, Apple, 0 );

            // The fruit clock starts at 100: 2 ripe at 250, 30 minutes into the third.
            var harvest = farm.TryHarvest( Orchard, 250, rng );
            Assert.IsTrue( harvest.Success );
            Assert.AreEqual( new ItemId( "Apple" ), harvest.Produce );
            Assert.AreEqual( 2, harvest.Quantity );

            var after = farm.Describe( Orchard, 250 );
            Assert.IsFalse( after.IsEmpty, "the tree stays" );
            Assert.AreEqual( 0, after.ReadyYield );
            Assert.AreEqual( 0, farm.Describe( Orchard, 279 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 280 ).ReadyYield, "the third fruit was already 30 minutes along" );
        }

        [ Test ]
        public void Perennial_HarvestFromFullDoesNotBankSurplusTime( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 0 );
            farm.TryHarvest( Orchard, 10000, new DeterministicRandom( 1 ) );

            Assert.AreEqual( 0, farm.Describe( Orchard, 10000 ).ReadyYield );
            Assert.AreEqual( 0, farm.Describe( Orchard, 10059 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 10060 ).ReadyYield );
        }

        [ Test ]
        public void Perennial_PartialEatFromFullKeepsTheRest( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 0 );

            Assert.AreEqual( 1, farm.Eat( Orchard, 10000, 1 ) );
            Assert.AreEqual( 3, farm.Describe( Orchard, 10000 ).ReadyYield );
            Assert.AreEqual( 3, farm.Describe( Orchard, 10059 ).ReadyYield );
            Assert.AreEqual( 4, farm.Describe( Orchard, 10060 ).ReadyYield );
        }

        // --- harvest and eating ---

        [ Test ]
        public void Harvest_UnripeFails( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.IsFalse( farm.TryHarvest( Bed, 29, new DeterministicRandom( 1 ) ).Success );
            Assert.IsFalse( farm.Describe( Bed, 29 ).IsEmpty );
        }

        [ Test ]
        public void Harvest_EmptyPlotOrMissingPlotFails( ) {
            var farm = NewFarm();
            var rng = new DeterministicRandom( 1 );
            Assert.IsFalse( farm.TryHarvest( Bed, 0, rng ).Success );
            Assert.IsFalse( farm.TryHarvest( new PlotKey( 9, 9, 9 ), 0, rng ).Success );
        }

        [ Test ]
        public void Harvest_Annual_EmptiesThePlotAndAllowsReplanting( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );

            var harvest = farm.TryHarvest( Bed, 30, new DeterministicRandom( 1 ) );
            Assert.IsTrue( harvest.Success );
            Assert.AreEqual( new ItemId( "WheatGrain" ), harvest.Produce );
            Assert.AreEqual( 3, harvest.Quantity );
            Assert.IsTrue( farm.Describe( Bed, 30 ).IsEmpty );
            Assert.IsTrue( farm.Describe( Bed, 30 ).Exists );
            Assert.AreEqual( PlantResult.Planted, farm.TryPlant( Bed, Wheat, 30 ) );
        }

        [ Test ]
        public void SeedDrop_FollowsChance( ) {
            var never = NewFarm( 0f );
            never.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 0, never.TryHarvest( Bed, 30, new DeterministicRandom( 1 ) ).SeedQuantity );

            var always = NewFarm( 1f );
            always.TryPlant( Bed, Wheat, 0 );
            var harvest = always.TryHarvest( Bed, 30, new DeterministicRandom( 1 ) );
            Assert.AreEqual( 1, harvest.SeedQuantity );
            Assert.AreEqual( new ItemId( "WheatSeed" ), harvest.Seed );
        }

        [ Test ]
        public void Eat_Annual_LosesTheWholeCrop( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 1, farm.Eat( Bed, 30, 1 ) );
            Assert.IsTrue( farm.Describe( Bed, 30 ).IsEmpty );
        }

        [ Test ]
        public void Eat_UnripeOrEmpty_EatsNothing( ) {
            var farm = NewFarm();
            Assert.AreEqual( 0, farm.Eat( Bed, 0, 1 ) );
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 0, farm.Eat( Bed, 29, 1 ) );
            Assert.IsFalse( farm.Describe( Bed, 29 ).IsEmpty );
        }

        [ Test ]
        public void Eat_Perennial_IsCappedAtWhatIsRipe( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 0 );
            Assert.AreEqual( 2, farm.Eat( Orchard, 250, 10 ) );
            Assert.IsFalse( farm.Describe( Orchard, 250 ).IsEmpty );
            Assert.AreEqual( 0, farm.Eat( Orchard, 250, 1 ) );
        }

        [ Test ]
        public void MinutesUntilRipe_CountsDownToTheFirstHarvestAndThenToTheNextFruit( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 0 );
            farm.TryPlant( Orchard, Apple, 0 );

            Assert.AreEqual( 30L, farm.Describe( Bed, 0 ).MinutesUntilRipe );
            Assert.AreEqual( 5L, farm.Describe( Bed, 25 ).MinutesUntilRipe );
            Assert.AreEqual( 0L, farm.Describe( Bed, 30 ).MinutesUntilRipe, "ripe: nothing to wait for" );

            Assert.AreEqual( 160L, farm.Describe( Orchard, 0 ).MinutesUntilRipe, "growing 100, then the first fruit takes 60" );
            Assert.AreEqual( 0L, farm.Describe( Orchard, 160 ).MinutesUntilRipe );
            farm.Eat( Orchard, 160, 1 );
            Assert.AreEqual( 60L, farm.Describe( Orchard, 160 ).MinutesUntilRipe, "the next fruit starts ripening" );
            Assert.AreEqual( 20L, farm.Describe( Orchard, 200 ).MinutesUntilRipe );
        }

        // --- scheduling ---

        [ Test ]
        public void NextChangeAt_WalksStagesThenFruit( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Apple, 0 );
            Assert.AreEqual( 40L, farm.NextChangeAt( Orchard, 0 ) );
            Assert.AreEqual( 100L, farm.NextChangeAt( Orchard, 40 ) );
            Assert.AreEqual( 160L, farm.NextChangeAt( Orchard, 100 ) );
            Assert.AreEqual( 220L, farm.NextChangeAt( Orchard, 160 ) );
            Assert.IsNull( farm.NextChangeAt( Orchard, 10000 ), "full: nothing changes by itself" );
        }

        [ Test ]
        public void NextChangeAt_AnnualEndsAtMaturity_AndEmptyHasNone( ) {
            var farm = NewFarm();
            Assert.IsNull( farm.NextChangeAt( Bed, 0 ) );
            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 10L, farm.NextChangeAt( Bed, 0 ) );
            Assert.AreEqual( 30L, farm.NextChangeAt( Bed, 10 ) );
            Assert.IsNull( farm.NextChangeAt( Bed, 30 ) );
        }

        [ Test ]
        public void PlotChanged_FiresOnCommandsOnly( ) {
            var farm = NewFarm();
            int changes = 0;
            farm.PlotChanged += _ => changes++;

            farm.TryPlant( Bed, Wheat, 0 );
            Assert.AreEqual( 1, changes );
            farm.TryPlant( Bed, Wheat, 0 );
            farm.TryHarvest( Bed, 5, new DeterministicRandom( 1 ) );
            farm.Describe( Bed, 5 );
            Assert.AreEqual( 1, changes, "rejected commands and queries stay silent" );
            farm.TryHarvest( Bed, 30, new DeterministicRandom( 1 ) );
            Assert.AreEqual( 2, changes );
        }

        // --- save ---

        [ Test ]
        public void Snapshot_RoundTrip_GivesTheSameStateAtAnyLaterTime( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Wheat, 7 );
            farm.TryPlant( Orchard, Apple, 3 );
            farm.TryHarvest( Orchard, 280, new DeterministicRandom( 1 ) );

            var restored = FarmModel.FromSnapshot( NewCatalog(), farm.ToSnapshot() );

            foreach( long now in new long[] { 0, 30, 100, 250, 400, 5000, 100 * Day } ) {
                foreach( var key in new[] { Bed, Orchard } ) {
                    var a = farm.Describe( key, now );
                    var b = restored.Describe( key, now );
                    Assert.AreEqual( a.Stage, b.Stage, $"stage {key} @ {now}" );
                    Assert.AreEqual( a.ReadyYield, b.ReadyYield, $"yield {key} @ {now}" );
                    Assert.AreEqual( a.IsEmpty, b.IsEmpty );
                    Assert.AreEqual( farm.NextChangeAt( key, now ), restored.NextChangeAt( key, now ) );
                }
            }
        }

        [ Test ]
        public void Snapshot_KeepsEmptyPlotsAndTheirType( ) {
            var restored = FarmModel.FromSnapshot( NewCatalog(), NewFarm().ToSnapshot() );
            Assert.AreEqual( 2, restored.PlotCount );
            Assert.IsTrue( restored.Describe( Bed, 0 ).IsEmpty );
            Assert.AreEqual( PlotType.Orchard, restored.Describe( Orchard, 0 ).Type );
        }

        [ Test ]
        public void Snapshot_IsOrderedByKey_SoSavesAreStable( ) {
            var farm = NewFarm();
            farm.AddPlot( new PlotKey( 0, 0, 0 ), PlotType.Bed );
            var plots = farm.ToSnapshot().Plots;
            Assert.AreEqual( 0, plots[ 0 ].X );
            Assert.AreEqual( 1, plots[ 1 ].X );
            Assert.AreEqual( 5, plots[ 2 ].X );
        }

        [ Test ]
        public void FromSnapshot_NullGivesEmptyFarm_AndUnknownCropIsDropped( ) {
            Assert.AreEqual( 0, FarmModel.FromSnapshot( NewCatalog(), null ).PlotCount );

            var snapshot = new FarmSnapshot();
            snapshot.Plots.Add( new PlotSnapshot { X = 1, Type = PlotType.Bed, CropId = "RemovedCrop" } );
            var restored = FarmModel.FromSnapshot( NewCatalog(), snapshot );
            Assert.IsTrue( restored.Describe( new PlotKey( 1, 0, 0 ), 0 ).Exists );
            Assert.IsTrue( restored.Describe( new PlotKey( 1, 0, 0 ), 0 ).IsEmpty );
        }

        [ Test ]
        public void Definition_RejectsBadData( ) {
            Assert.Throws<ArgumentException>( ( ) => new CropDefinition(
                default, CropLifecycle.Annual, PlotType.Bed, new int[ 0 ], 1, 0, default, default, 0f ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new CropDefinition(
                Wheat, CropLifecycle.Annual, PlotType.Bed, new[] { -1 }, 1, 0, default, default, 0f ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new CropDefinition(
                Wheat, CropLifecycle.Perennial, PlotType.Bed, new int[ 0 ], 1, 0, default, default, 0f ) );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new CropDefinition(
                Wheat, CropLifecycle.Annual, PlotType.Bed, new int[ 0 ], 0, 0, default, default, 0f ) );
        }

        private static ICropCatalog NewCatalog() => new CropRegistry( new[] { WheatDef(), AppleDef() } );
    }
}
