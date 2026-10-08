using Hearthglade.Core.Items;
using Hearthglade.Core.Stats;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ExposureTests {

        private static readonly Protection None = new Protection();

        [ Test ]
        public void InsideTheBand_IsComfortable( ) {
            var state = Exposure.Assess( 12f, None, nearFire: false, indoors: false );
            Assert.AreEqual( Discomfort.None, state.Kind );
            Assert.IsFalse( state.IsHarmful );
        }

        [ Test ]
        public void BelowTheBand_IsCold( ) {
            var state = Exposure.Assess( -8f, None, false, false );
            Assert.AreEqual( Discomfort.Cold, state.Kind );
            Assert.AreEqual( 8f, state.Excess, 1e-4f );
            Assert.IsTrue( state.IsHarmful );
        }

        [ Test ]
        public void AboveTheBand_IsHot( ) {
            var state = Exposure.Assess( 30f, None, false, false );
            Assert.AreEqual( Discomfort.Heat, state.Kind );
            Assert.AreEqual( 6f, state.Excess, 1e-4f );
        }

        [ Test ]
        public void EachPointOfClothing_CoversProtectionCover( ) {
            var state = Exposure.Assess( -8f, new Protection( cold: 0.5f ), false, false );
            Assert.AreEqual( 8f - 0.5f * Exposure.ProtectionCover, state.Excess, 1e-4f );
            Assert.AreEqual( Discomfort.None, Exposure.Assess( -8f, new Protection( cold: 0.8f ), false, false ).Kind );
        }

        [ Test ]
        public void ColdProtection_DoesNothingAgainstHeat( ) {
            var state = Exposure.Assess( 30f, new Protection( cold: 2f ), false, false );
            Assert.AreEqual( Discomfort.Heat, state.Kind );
            Assert.AreEqual( 6f, state.Excess, 1e-4f );
        }

        [ Test ]
        public void AFire_CancelsTheCold_ButNotTheHeat( ) {
            Assert.AreEqual( Discomfort.None, Exposure.Assess( -20f, None, nearFire: true, indoors: false ).Kind );
            Assert.AreEqual( Discomfort.Heat, Exposure.Assess( 30f, None, nearFire: true, indoors: false ).Kind );
        }

        [ Test ]
        public void ARoof_TakesTheEdgeOffBothWays( ) {
            Assert.AreEqual( 2f, Exposure.Assess( -8f, None, false, indoors: true ).Excess, 1e-4f );
            Assert.AreEqual( 0f, Exposure.Assess( 30f, None, false, indoors: true ).Excess, 1e-4f );
        }

        [ Test ]
        public void JustOverTheBorder_DoesNotHurt( ) {
            var state = Exposure.Assess( Exposure.ComfortMin - 0.5f, None, false, false );
            Assert.AreEqual( Discomfort.Cold, state.Kind );
            Assert.IsFalse( state.IsHarmful );
        }

        [ Test ]
        public void TheMapRange_SpreadsTheClimateScale( ) {
            Assert.AreEqual( -20f, Exposure.Celsius( -1f, -20f, 10f ), 1e-4f );
            Assert.AreEqual( 10f, Exposure.Celsius( 1f, -20f, 10f ), 1e-4f );
            Assert.AreEqual( -5f, Exposure.Celsius( 0f, -20f, 10f ), 1e-4f );
            Assert.AreEqual( 10f, Exposure.Celsius( 1.5f, -20f, 10f ), 1e-4f );
        }

        [ Test ]
        public void TheNight_IsColder_AndCavesAreAlwaysMild( ) {
            Assert.AreEqual( -9f, Exposure.AmbientTemperature( -5f, isNight: true, underground: false ), 1e-4f );
            Assert.AreEqual( -5f, Exposure.AmbientTemperature( -5f, isNight: false, underground: false ), 1e-4f );
            Assert.AreEqual( Exposure.UndergroundTemperature, Exposure.AmbientTemperature( -20f, isNight: true, underground: true ), 1e-4f );
        }

        [ Test ]
        public void TheFullTravellerSet_KeepsTheTaigaNightBearable( ) {
            // Cap 0.3 + coat 0.5 + boots 0.3 (ClothingAssetBuilder) on the coldest cell of a map with the default range at night.
            var traveller = new Protection( cold: 1.1f );
            var coldest = Exposure.Celsius( -1f, Exposure.DefaultMinTemperature, Exposure.DefaultMaxTemperature );
            var night = Exposure.AmbientTemperature( coldest, isNight: true, underground: false );
            Assert.IsFalse( Exposure.Assess( night, traveller, false, false ).IsHarmful );
            Assert.IsTrue( Exposure.Assess( night, None, false, false ).IsHarmful );
        }

        [ Test ]
        public void Armour_ReducesDamage_UpToTheCap( ) {
            Assert.AreEqual( 7.5f, new Protection( damage: 0.25f ).Reduce( 10f ), 1e-4f );
            Assert.AreEqual( 2f, new Protection( damage: 5f ).Reduce( 10f ), 1e-4f );
            Assert.AreEqual( 0f, new Protection().Reduce( -3f ), 1e-4f );
        }
    }
}
