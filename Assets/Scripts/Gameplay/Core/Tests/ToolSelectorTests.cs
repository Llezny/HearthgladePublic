using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ToolSelectorTests {

        private static readonly ItemDefinition StoneAxe = TestItems.Tool( "StoneAxe", new ItemStats( chopSpeed: 2f, attackPower: 3f ), ToolGroup.Axe );
        private static readonly ItemDefinition IronAxe = TestItems.Tool( "IronAxe", new ItemStats( chopSpeed: 3f ), ToolGroup.Axe );
        private static readonly ItemDefinition Pickaxe = TestItems.Tool( "Pickaxe", new ItemStats( mineSpeed: 2f ), ToolGroup.Pickaxe );
        private static readonly ItemDefinition Sickle = TestItems.Tool( "Sickle", new ItemStats( harvestSpeed: 2.5f ), ToolGroup.Sickle );

        private static ItemSlot HandWith( ItemDefinition item = null ) {
            var slot = new ItemSlot( ItemType.Tool | ItemType.Weapon );
            if( item != null ) {
                slot.Set( ItemStack.Of( item ) );
            }
            return slot;
        }

        private static ItemContainer BackpackWith( params ItemDefinition[] items ) {
            var backpack = new ItemContainer( 6 );
            foreach( var item in items ) {
                backpack.Add( item );
            }
            return backpack;
        }

        [ Test ]
        public void NothingToWorkWith_FindsNoTool( ) {
            var choice = ToolSelector.Pick( GatherSkill.Chop, HandWith(), BackpackWith( TestItems.Wood() ) );
            Assert.IsFalse( choice.Found );
            Assert.IsNull( choice.Slot );
        }

        [ Test ]
        public void AToolInTheBackpack_IsUsedWithoutBeingInTheHand( ) {
            var backpack = BackpackWith( TestItems.Wood(), StoneAxe );
            var choice = ToolSelector.Pick( GatherSkill.Chop, HandWith(), backpack );
            Assert.IsTrue( choice.Found );
            Assert.AreEqual( "StoneAxe", choice.Definition.Id.Value );
            Assert.AreSame( backpack[ 1 ], choice.Slot );
            Assert.AreEqual( 2f, choice.Speed );
        }

        [ Test ]
        public void TheHighestSpeedWins( ) {
            var backpack = BackpackWith( StoneAxe, IronAxe );
            var choice = ToolSelector.Pick( GatherSkill.Chop, HandWith(), backpack );
            Assert.AreEqual( "IronAxe", choice.Definition.Id.Value );
        }

        [ Test ]
        public void ATieGoesToTheHandItem( ) {
            var twin = TestItems.Tool( "TwinAxe", new ItemStats( chopSpeed: 2f ), ToolGroup.Axe );
            var hand = HandWith( twin );
            var choice = ToolSelector.Pick( GatherSkill.Chop, hand, BackpackWith( StoneAxe ) );
            Assert.AreSame( hand, choice.Slot );
        }

        [ Test ]
        public void ATieInTheBackpack_GoesToTheEarlierSlot( ) {
            var twin = TestItems.Tool( "TwinAxe", new ItemStats( chopSpeed: 2f ), ToolGroup.Axe );
            var backpack = BackpackWith( StoneAxe, twin );
            var choice = ToolSelector.Pick( GatherSkill.Chop, HandWith(), backpack );
            Assert.AreSame( backpack[ 0 ], choice.Slot );
        }

        [ Test ]
        public void AFasterToolInTheBackpack_BeatsAWeakerOneInTheHand( ) {
            var backpack = BackpackWith( IronAxe );
            var choice = ToolSelector.Pick( GatherSkill.Chop, HandWith( StoneAxe ), backpack );
            Assert.AreEqual( "IronAxe", choice.Definition.Id.Value );
        }

        [ Test ]
        public void EachSkillLooksAtItsOwnStat( ) {
            var backpack = BackpackWith( StoneAxe, Pickaxe, Sickle );
            var hand = HandWith();
            Assert.AreEqual( "StoneAxe", ToolSelector.Pick( GatherSkill.Chop, hand, backpack ).Definition.Id.Value );
            Assert.AreEqual( "Pickaxe", ToolSelector.Pick( GatherSkill.Mine, hand, backpack ).Definition.Id.Value );
            Assert.AreEqual( "Sickle", ToolSelector.Pick( GatherSkill.Harvest, hand, backpack ).Definition.Id.Value );
        }

        [ Test ]
        public void AnAxeIsNoPickaxe( ) {
            var choice = ToolSelector.Pick( GatherSkill.Mine, HandWith( StoneAxe ), BackpackWith( StoneAxe ) );
            Assert.IsFalse( choice.Found );
        }

        [ Test ]
        public void NoSkill_FindsNothing( ) {
            Assert.IsFalse( ToolSelector.Pick( GatherSkill.None, HandWith( StoneAxe ), BackpackWith( Pickaxe ) ).Found );
        }

        [ Test ]
        public void MissingHandOrBackpack_IsFine( ) {
            Assert.IsFalse( ToolSelector.Pick( GatherSkill.Chop, null, null ).Found );
            Assert.IsTrue( ToolSelector.Pick( GatherSkill.Chop, HandWith( StoneAxe ), null ).Found );
            Assert.IsTrue( ToolSelector.Pick( GatherSkill.Chop, null, BackpackWith( StoneAxe ) ).Found );
        }

        [ Test ]
        public void PickingDoesNotTouchAnySlot( ) {
            var backpack = BackpackWith( StoneAxe );
            var hand = HandWith( Pickaxe );
            ToolSelector.Pick( GatherSkill.Chop, hand, backpack );
            Assert.AreEqual( "Pickaxe", hand.Stack.Id.Value );
            Assert.AreEqual( "StoneAxe", backpack[ 0 ].Stack.Id.Value );
            Assert.AreEqual( 1, backpack[ 0 ].Stack.Count );
        }

        private static ItemDefinition Weapon( string id, float attackPower ) {
            return TestItems.Make( id, maxStack: 1, type: ItemType.Weapon, maxDurability: 60f, stats: new ItemStats( attackPower: attackPower ),
                toolGroup: ToolGroup.Spear );
        }

        [ Test ]
        public void Hunting_UsesTheStrongestWeapon_FromTheBackpackToo( ) {
            var backpack = BackpackWith( Weapon( "StoneSpear", 6f ), Weapon( "FlintSpear", 9f ) );
            var choice = ToolSelector.PickWeapon( HandWith(), backpack );
            Assert.AreEqual( "FlintSpear", choice.Definition.Id.Value );
            Assert.AreEqual( 9f, choice.Speed );
        }

        [ Test ]
        public void Hunting_IgnoresToolsThatCanHit( ) {
            Assert.IsFalse( ToolSelector.PickWeapon( HandWith( StoneAxe ), BackpackWith( Pickaxe ) ).Found );
        }

        [ Test ]
        public void Hunting_ATie_GoesToTheHand( ) {
            var hand = HandWith( Weapon( "HandSpear", 6f ) );
            Assert.AreSame( hand, ToolSelector.PickWeapon( hand, BackpackWith( Weapon( "BackpackSpear", 6f ) ) ).Slot );
        }
    }
}
