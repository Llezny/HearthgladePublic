using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ToolWearTests {

        private static ItemSlot SlotWith( ItemDefinition item, int count = 1 ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Of( item, count ) );
            return slot;
        }

        private static ItemDefinition Axe( float durability = 3f ) {
            return TestItems.Make( "Axe", maxStack: 1, type: ItemType.Tool, maxDurability: durability );
        }

        [ Test ]
        public void UsingATool_WearsItDown( ) {
            var slot = SlotWith( Axe( 3f ) );
            Assert.AreEqual( WearResult.Worn, ToolWear.Apply( slot ) );
            Assert.AreEqual( 2f, slot.Stack.Durability, 1e-4f );
        }

        [ Test ]
        public void TheLastUse_BreaksTheToolAndEmptiesTheSlot( ) {
            var slot = SlotWith( Axe( 2f ) );
            Assert.AreEqual( WearResult.Worn, ToolWear.Apply( slot ) );
            Assert.AreEqual( WearResult.Broken, ToolWear.Apply( slot ) );
            Assert.IsTrue( slot.IsEmpty );
        }

        [ Test ]
        public void ABrokenToolInTheHand_RaisesUnequipped( ) {
            var model = new EquipmentModel();
            model[ SlotType.Hand ].Set( ItemStack.Of( Axe( 1f ) ) );
            var unequipped = 0;
            model.Unequipped += ( _, _ ) => unequipped++;

            ToolWear.Apply( model[ SlotType.Hand ] );

            Assert.AreEqual( 1, unequipped );
        }

        [ Test ]
        public void ItemsWithoutDurability_NeverWear( ) {
            var slot = SlotWith( TestItems.Wood(), 3 );
            Assert.AreEqual( WearResult.None, ToolWear.Apply( slot ) );
            Assert.AreEqual( 3, slot.Stack.Count );
        }

        [ Test ]
        public void NoSlotOrEmptySlot_IsNothing( ) {
            Assert.AreEqual( WearResult.None, ToolWear.Apply( null ) );
            Assert.AreEqual( WearResult.None, ToolWear.Apply( new ItemSlot() ) );
        }

        [ Test ]
        public void AStackOfTools_LosesOneAndStartsTheNextNew( ) {
            var slot = SlotWith( TestItems.Make( "Knife", maxStack: 5, type: ItemType.Tool, maxDurability: 2f ), 2 );
            ToolWear.Apply( slot );
            Assert.AreEqual( WearResult.Broken, ToolWear.Apply( slot ) );
            Assert.AreEqual( 1, slot.Stack.Count );
            Assert.AreEqual( 2f, slot.Stack.Durability, 1e-4f );
        }

        [ Test ]
        public void WearHappensOnlyForAToolThatDoesTheWork( ) {
            var weak = new ToolChoice( new ItemSlot(), Axe(), 0.5f );
            var strong = new ToolChoice( new ItemSlot(), Axe(), 2f );
            Assert.IsFalse( GatherTime.UsesTool( ToolChoice.None, requiresTool: true ) );
            Assert.IsTrue( GatherTime.UsesTool( weak, requiresTool: true ) );
            Assert.IsFalse( GatherTime.UsesTool( weak, requiresTool: false ) );
            Assert.IsTrue( GatherTime.UsesTool( strong, requiresTool: false ) );
        }
    }
}
