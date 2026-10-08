using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemStatTextTests {

        [ Test ]
        public void AnItemWithoutStats_HasNoLines( ) {
            CollectionAssert.IsEmpty( ItemStatText.Lines( TestItems.Wood() ) );
            CollectionAssert.IsEmpty( ItemStatText.Lines( null ) );
        }

        [ Test ]
        public void ATool_ListsItsSpeedsAndAttack( ) {
            var axe = TestItems.Tool( "Axe", new ItemStats( chopSpeed: 2f, attackPower: 3f ), ToolGroup.Axe );
            CollectionAssert.AreEqual( new[] { "Chopping x2", "Attack 3" }, ItemStatText.Lines( axe ) );
        }

        [ Test ]
        public void Numbers_AreShortAndIndependentOfTheCulture( ) {
            var sickle = TestItems.Tool( "Sickle", new ItemStats( harvestSpeed: 1.5f ) );
            CollectionAssert.AreEqual( new[] { "Harvesting x1.5" }, ItemStatText.Lines( sickle ) );
        }

        [ Test ]
        public void Clothing_ListsItsProtection( ) {
            var coat = TestItems.Clothing( "Coat", ItemType.Chestplate, new Protection( cold: 0.5f, damage: 0.15f ) );
            CollectionAssert.AreEqual( new[] { "Cold protection 0.5", "Damage -15%" }, ItemStatText.Lines( coat ) );
        }

        [ Test ]
        public void TheSummary_ShowsTheCappedDamage( ) {
            Assert.AreEqual( "Cold 1.2   Heat 0   Damage -80%", ItemStatText.Summary( new Protection( cold: 1.2f, damage: 2f ) ) );
        }
    }
}
