using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class CropClimateTests {

        private static readonly ItemId Cold = new( "WarmWheat" );
        private static readonly ItemId Tree = new( "WarmTree" );
        private static readonly PlotKey Bed = new( 1, 0, 2 );
        private static readonly PlotKey Orchard = new( 5, 0, 5 );

        // Good between 0.4 and 0.8 degrees and humidity -0.2..0.2.
        private static readonly ClimateRange Warm = new( 0.4f, 0.8f, -0.2f, 0.2f );
        private static readonly Climate Right = new( 0.6f, 0f );
        private static readonly Climate Wrong = new( -1f, -1f );

        // 100 minutes to mature.
        private static CropDefinition AnnualDef() => new CropDefinition(
            Cold, CropLifecycle.Annual, PlotType.Bed, new[] { 40, 60 }, 3, 0,
            new ItemId( "Grain" ), new ItemId( "Seed" ), 0f, ForageKind.None, Warm );

        // 100 minutes to mature, then one fruit per 60 minutes.
        private static CropDefinition TreeDef() => new CropDefinition(
            Tree, CropLifecycle.Perennial, PlotType.Orchard, new[] { 40, 60 }, 4, 60,
            new ItemId( "Fruit" ), new ItemId( "Sapling" ), 0f, ForageKind.None, Warm );

        private static ICropCatalog NewCatalog() => new CropRegistry( new[] { AnnualDef(), TreeDef() } );

        private static FarmModel NewFarm() {
            var farm = new FarmModel( NewCatalog() );
            farm.AddPlot( Bed, PlotType.Bed );
            farm.AddPlot( Orchard, PlotType.Orchard );
            return farm;
        }

        // --- fit and speed ---

        [ Test ]
        public void AxisFit_IsFullInsideTheRange_AndFallsLinearlyToZeroAtTheMargin( ) {
            Assert.AreEqual( 1f, CropClimate.AxisFit( 0.5f, 0.4f, 0.8f ), 1e-5f );
            Assert.AreEqual( 1f, CropClimate.AxisFit( 0.4f, 0.4f, 0.8f ), 1e-5f );
            Assert.AreEqual( 0.5f, CropClimate.AxisFit( 0.4f - CropClimate.Margin / 2f, 0.4f, 0.8f ), 1e-5f );
            Assert.AreEqual( 0.5f, CropClimate.AxisFit( 0.8f + CropClimate.Margin / 2f, 0.4f, 0.8f ), 1e-5f );
            Assert.AreEqual( 0f, CropClimate.AxisFit( -1f, 0.4f, 0.8f ), 1e-5f );
        }

        [ Test ]
        public void SpeedPercent_GoesFromHalfToFull( ) {
            Assert.AreEqual( 100, CropClimate.SpeedPercent( 1f ) );
            Assert.AreEqual( 75, CropClimate.SpeedPercent( 0.5f ) );
            Assert.AreEqual( 50, CropClimate.SpeedPercent( 0f ) );
            Assert.AreEqual( 50, CropClimate.SpeedPercent( -3f ) );
            Assert.AreEqual( 100, CropClimate.SpeedPercent( 7f ) );
        }

        [ Test ]
        public void TheDefaultRange_SuitsAnyClimate( ) {
            foreach( var climate in new[] { new Climate( -1f, -1f ), new Climate( 1f, 1f ), new Climate( 0f, 0.7f ) } ) {
                Assert.AreEqual( 100, CropClimate.SpeedPercent( ClimateRange.Anywhere, climate ) );
                Assert.AreEqual( ClimateIssue.None, CropClimate.Issue( ClimateRange.Anywhere, climate ) );
            }
        }

        [ Test ]
        public void BothAxesCount_AndTheWorseOneIsBlamed( ) {
            // Temperature half a margin off, humidity a full margin off: fit 0.
            Assert.AreEqual( 50, CropClimate.SpeedPercent( Warm, new Climate( 0.4f - CropClimate.Margin / 2f, 0.2f + CropClimate.Margin ) ) );
            // Only the temperature half a margin off: 0.5 x 1.
            Assert.AreEqual( 75, CropClimate.SpeedPercent( Warm, new Climate( 0.4f - CropClimate.Margin / 2f, 0f ) ) );

            Assert.AreEqual( ClimateIssue.None, CropClimate.Issue( Warm, Right ) );
            Assert.AreEqual( ClimateIssue.TooCold, CropClimate.Issue( Warm, new Climate( -0.5f, 0f ) ) );
            Assert.AreEqual( ClimateIssue.TooHot, CropClimate.Issue( Warm, new Climate( 1f, 0f ) ) );
            Assert.AreEqual( ClimateIssue.TooDry, CropClimate.Issue( Warm, new Climate( 0.6f, -0.9f ) ) );
            Assert.AreEqual( ClimateIssue.TooWet, CropClimate.Issue( Warm, new Climate( 0.6f, 0.9f ) ) );
        }

        [ Test ]
        public void ClimateCodes_RoundTrip_AndZeroMeansUnknown( ) {
            Assert.AreEqual( 1, Climate.Encode( -1f ) );
            Assert.AreEqual( 255, Climate.Encode( 1f ) );
            Assert.AreEqual( 1, Climate.Encode( -5f ) );
            for( float value = -1f; value <= 1f; value += 0.1f ) {
                Assert.AreNotEqual( 0, Climate.Encode( value ) );
                Assert.AreEqual( value, Climate.Decode( Climate.Encode( value ) ), 1f / 127f );
            }
        }

        // --- growth in a model ---

        [ Test ]
        public void RightClimate_GrowsAtNormalSpeed( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Cold, 0, Right );
            var view = farm.Describe( Bed, 0 );
            Assert.AreEqual( 100, view.SpeedPercent );
            Assert.AreEqual( 100, view.MinutesUntilRipe );
            Assert.AreEqual( 3, farm.Describe( Bed, 100 ).ReadyYield );
        }

        [ Test ]
        public void UnknownClimate_MeansNormalSpeed( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Cold, 0 );
            Assert.AreEqual( 100, farm.Describe( Bed, 0 ).MinutesUntilRipe );
        }

        [ Test ]
        public void WrongClimate_TakesTwiceAsLong_ButYieldsTheSame( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Cold, 0, Wrong );
            Assert.AreEqual( 50, farm.Describe( Bed, 0 ).SpeedPercent );
            Assert.AreEqual( 200, farm.Describe( Bed, 0 ).MinutesUntilRipe );
            Assert.AreEqual( 0, farm.Describe( Bed, 199 ).ReadyYield );
            Assert.AreEqual( 3, farm.Describe( Bed, 200 ).ReadyYield );
        }

        [ Test ]
        public void SlowCrop_StagesScaleToo_AndNextChangeAgrees( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Cold, 0, Wrong );
            // Stages of 40 and 60 minutes become 80 and 120.
            Assert.AreEqual( 0, farm.Describe( Bed, 79 ).Stage );
            Assert.AreEqual( 1, farm.Describe( Bed, 80 ).Stage );
            Assert.AreEqual( ( long? ) 80, farm.NextChangeAt( Bed, 0 ) );
            Assert.AreEqual( ( long? ) 200, farm.NextChangeAt( Bed, 80 ) );
            Assert.AreEqual( 2, farm.Describe( Bed, 200 ).Stage );
        }

        [ Test ]
        public void SlowTree_FruitClockScalesToo( ) {
            var farm = NewFarm();
            farm.TryPlant( Orchard, Tree, 0, Wrong );
            // Mature at 200, then one fruit per 120 minutes.
            Assert.AreEqual( 0, farm.Describe( Orchard, 319 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 320 ).ReadyYield );
            Assert.AreEqual( 2, farm.Describe( Orchard, 440 ).ReadyYield );
            Assert.AreEqual( ( long? ) 440, farm.NextChangeAt( Orchard, 320 ) );

            var harvest = farm.TryHarvest( Orchard, 440, new DeterministicRandom( 1 ) );
            Assert.AreEqual( 2, harvest.Quantity );
            Assert.AreEqual( 0, farm.Describe( Orchard, 440 ).ReadyYield );
            Assert.AreEqual( 1, farm.Describe( Orchard, 560 ).ReadyYield );
        }

        // --- saves ---

        [ Test ]
        public void Snapshot_KeepsTheSpeed( ) {
            var farm = NewFarm();
            farm.TryPlant( Bed, Cold, 0, Wrong );
            var snapshot = farm.ToSnapshot();
            Assert.AreEqual( 2, snapshot.Version );

            var restored = FarmModel.FromSnapshot( NewCatalog(), snapshot );
            Assert.AreEqual( 50, restored.Describe( Bed, 0 ).SpeedPercent );
            Assert.AreEqual( 200, restored.Describe( Bed, 0 ).MinutesUntilRipe );
        }

        [ Test ]
        public void VersionOneSave_LoadsAtNormalSpeed( ) {
            var snapshot = new FarmSnapshot { Version = 1 };
            snapshot.Plots.Add( new PlotSnapshot {
                X = Bed.X, Y = Bed.Y, Z = Bed.Z, Type = PlotType.Bed, CropId = Cold.Value, PlantedAtMinute = 0, FruitClockStartMinute = 100,
            } );
            var restored = FarmModel.FromSnapshot( NewCatalog(), snapshot );
            Assert.AreEqual( 100, restored.Describe( Bed, 0 ).SpeedPercent );
            Assert.AreEqual( 100, restored.Describe( Bed, 0 ).MinutesUntilRipe );
        }
    }
}
