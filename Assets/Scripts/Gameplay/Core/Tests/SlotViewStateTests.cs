using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class SlotViewStateTests {

        [ Test ]
        public void EmptySlot_IsEmptyWithNothingToShow( ) {
            var state = SlotViewState.From( new ItemSlot() );
            Assert.IsTrue( state.IsEmpty );
            Assert.IsFalse( state.ShowDurability );
            Assert.AreEqual( 0, state.Count );
            Assert.IsTrue( state.Item.IsEmpty );
        }

        [ Test ]
        public void FilledSlot_ShowsItemAndCount( ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Of( TestItems.Wood(), 4 ) );
            var state = SlotViewState.From( slot );
            Assert.IsFalse( state.IsEmpty );
            Assert.AreEqual( new ItemId( "Wood" ), state.Item );
            Assert.AreEqual( 4, state.Count );
            Assert.IsFalse( state.ShowDurability );
        }

        [ Test ]
        public void DurableItem_ShowsItsDurability( ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Restore( TestItems.Axe(), 1, 40f, null ) );
            var state = SlotViewState.From( slot );
            Assert.IsTrue( state.ShowDurability );
            Assert.AreEqual( 100f, state.MaxDurability );
            Assert.AreEqual( 40f, state.Durability );
        }

        [ Test ]
        public void UsableItem_OffersUse( ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Of( TestItems.Carrot() ) );
            Assert.AreEqual( SlotAction.Use, SlotActions.Available( slot ) );
        }

        [ Test ]
        public void BuildableItem_OffersPlace( ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Of( TestItems.Stool() ) );
            Assert.AreEqual( SlotAction.Place, SlotActions.Available( slot ) );
        }

        [ Test ]
        public void NonUsableOrEmpty_OffersNothing( ) {
            var slot = new ItemSlot();
            Assert.AreEqual( SlotAction.None, SlotActions.Available( slot ) );
            slot.Set( ItemStack.Of( TestItems.Wood() ) );
            Assert.AreEqual( SlotAction.None, SlotActions.Available( slot ) );
        }
    }
}
