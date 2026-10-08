using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemStatsTests {

        [ Test ]
        public void SpeedFor_ReadsTheStatOfTheSkill( ) {
            var stats = new ItemStats( chopSpeed: 1f, harvestSpeed: 2f, mineSpeed: 3f, attackPower: 4f );
            Assert.AreEqual( 1f, stats.SpeedFor( GatherSkill.Chop ) );
            Assert.AreEqual( 2f, stats.SpeedFor( GatherSkill.Harvest ) );
            Assert.AreEqual( 3f, stats.SpeedFor( GatherSkill.Mine ) );
            Assert.AreEqual( 0f, stats.SpeedFor( GatherSkill.None ) );
        }

        [ Test ]
        public void NegativeValues_BecomeZero( ) {
            var stats = new ItemStats( chopSpeed: -1f, attackPower: -5f );
            Assert.AreEqual( 0f, stats.ChopSpeed );
            Assert.AreEqual( 0f, stats.AttackPower );
            Assert.IsTrue( stats.IsEmpty );
        }

        [ Test ]
        public void DefaultStats_AreEmpty( ) {
            Assert.IsTrue( default( ItemStats ).IsEmpty );
            Assert.IsTrue( default( Protection ).IsEmpty );
        }

        [ Test ]
        public void Protection_AddsUpPerKind( ) {
            var total = new Protection( cold: 1f, heat: 0f, damage: 0.1f ) + new Protection( cold: 0.5f, heat: 2f, damage: 0.2f );
            Assert.AreEqual( 1.5f, total.Cold, 1e-4f );
            Assert.AreEqual( 2f, total.Heat, 1e-4f );
            Assert.AreEqual( 0.3f, total.Damage, 1e-4f );
        }

        [ Test ]
        public void DamageReduction_IsCapped( ) {
            Assert.AreEqual( 0.25f, new Protection( damage: 0.25f ).DamageReduction, 1e-4f );
            Assert.AreEqual( Protection.MaxDamageReduction, new Protection( damage: 3f ).DamageReduction, 1e-4f );
        }

        [ Test ]
        public void Definition_CarriesTheStatsAndTheGroup( ) {
            var axe = TestItems.Tool( "Axe2", new ItemStats( chopSpeed: 2f ), ToolGroup.Axe );
            Assert.AreEqual( 2f, axe.Stats.ChopSpeed );
            Assert.AreEqual( ToolGroup.Axe, axe.ToolGroup );
            Assert.IsTrue( TestItems.Wood().Stats.IsEmpty );
            Assert.AreEqual( ToolGroup.None, TestItems.Wood().ToolGroup );
        }
    }
}
