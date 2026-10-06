using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemSlotTests {

        [ Test ]
        public void Add_IntoEmptySlot_StoresTheStackAndRaisesChangedOnce( ) {
            var slot = new ItemSlot();
            var changes = 0;
            slot.Changed += _ => changes++;
            var added = slot.Add( ItemStack.Of( TestItems.Wood(), 4 ) );
            Assert.AreEqual( 4, added );
            Assert.AreEqual( 4, slot.Stack.Count );
            Assert.AreEqual( 1, changes );
        }

        [ Test ]
        public void Add_MoreThanTheStackLimit_StoresTheLimitAndReportsWhatFit( ) {
            var slot = new ItemSlot();
            var added = slot.Add( ItemStack.Of( TestItems.Stone(), 8 ) ); // max 5
            Assert.AreEqual( 5, added );
            Assert.AreEqual( 5, slot.Stack.Count );
        }

        [ Test ]
        public void Add_SlotMaxCount_BeatsTheItemsStackLimit( ) {
            var slot = new ItemSlot( ItemType.Everything, maxCount: 2 );
            Assert.AreEqual( 2, slot.Add( ItemStack.Of( TestItems.Wood(), 9 ) ) );
        }

        [ Test ]
        public void Add_DifferentItem_IsRefused( ) {
            var slot = new ItemSlot();
            slot.Add( ItemStack.Of( TestItems.Wood(), 1 ) );
            var changes = 0;
            slot.Changed += _ => changes++;
            Assert.AreEqual( 0, slot.Add( ItemStack.Of( TestItems.Stone(), 1 ) ) );
            Assert.AreEqual( 0, changes );
        }

        [ Test ]
        public void Add_AllowedTypesNotMatching_IsRefused( ) {
            var slot = new ItemSlot( ItemType.Helmet );
            Assert.AreEqual( 0, slot.Add( ItemStack.Of( TestItems.Wood(), 1 ) ) );
            Assert.AreEqual( 1, slot.Add( ItemStack.Of( TestItems.Helmet(), 1 ) ) );
        }

        [ Test ]
        public void Add_FilterRejects_IsRefused( ) {
            var slot = new ItemSlot { Filter = item => item.IsFuel };
            Assert.AreEqual( 0, slot.Add( ItemStack.Of( TestItems.Wood(), 1 ) ) );
            Assert.AreEqual( 1, slot.Add( ItemStack.Of( TestItems.Make( "Log", fuel: 5f ), 1 ) ) );
        }

        [ Test ]
        public void Add_LockedSlot_IsRefused( ) {
            var slot = new ItemSlot { IsLocked = true };
            Assert.AreEqual( 0, slot.Add( ItemStack.Of( TestItems.Wood(), 1 ) ) );
        }

        [ Test ]
        public void OutputOnly_RefusesPlayersButStillTakesCode( ) {
            var slot = new ItemSlot { OutputOnly = true };
            Assert.IsFalse( slot.AcceptsFromPlayer( TestItems.Wood() ) );
            Assert.AreEqual( 2, slot.Add( ItemStack.Of( TestItems.Wood(), 2 ) ) );
        }

        [ Test ]
        public void Remove_SomeOfTheStack_LeavesTheRest( ) {
            var slot = new ItemSlot();
            slot.Add( ItemStack.Of( TestItems.Wood(), 5 ) );
            Assert.AreEqual( 2, slot.Remove( 2 ) );
            Assert.AreEqual( 3, slot.Stack.Count );
        }

        [ Test ]
        public void Remove_MoreThanHeld_EmptiesTheSlotAndReportsWhatWasTaken( ) {
            var slot = new ItemSlot();
            slot.Add( ItemStack.Of( TestItems.Wood(), 2 ) );
            Assert.AreEqual( 2, slot.Remove( 9 ) );
            Assert.IsTrue( slot.IsEmpty );
        }

        [ Test ]
        public void Clear_EmptySlot_RaisesNothing( ) {
            var slot = new ItemSlot();
            var changes = 0;
            slot.Changed += _ => changes++;
            slot.Clear();
            Assert.AreEqual( 0, changes );
        }

        [ Test ]
        public void Set_IgnoresRules( ) {
            var slot = new ItemSlot( ItemType.Helmet ) { IsLocked = true };
            slot.Set( ItemStack.Of( TestItems.Wood(), 3 ) );
            Assert.AreEqual( 3, slot.Stack.Count );
        }

        [ Test ]
        public void SpaceFor_OverfilledSlot_IsZeroNotNegative( ) {
            var slot = new ItemSlot();
            slot.Set( ItemStack.Of( TestItems.Wood(), 99 ) );
            Assert.AreEqual( 0, slot.SpaceFor( TestItems.Wood() ) );
        }
    }
}
