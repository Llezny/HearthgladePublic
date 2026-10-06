using System.Collections.Generic;
using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemTransferTests {

        private static ItemSlot Slot( ItemDefinition item = null, int count = 0, ItemType allowed = ItemType.Everything, int max = int.MaxValue ) {
            var slot = new ItemSlot( allowed, max );
            if( item != null ) {
                slot.Set( ItemStack.Of( item, count ) );
            }
            return slot;
        }

        [ Test ]
        public void Move_ToEmptySlot_MovesEverything( ) {
            var from = Slot( TestItems.Wood(), 4 );
            var to = Slot();
            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 4, to.Stack.Count );
            Assert.IsTrue( from.IsEmpty );
        }

        [ Test ]
        public void Move_ToEmptySlotWithSmallerCapacity_MovesWhatFitsAndKeepsTheRest( ) {
            var from = Slot( TestItems.Wood(), 4 );
            var to = Slot( max: 3 );
            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 3, to.Stack.Count );
            Assert.AreEqual( 1, from.Stack.Count );
        }

        [ Test ]
        public void Move_FromEmptySlot_IsRejected( ) {
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( Slot(), Slot() ) );
        }

        [ Test ]
        public void Move_ToTheSameSlot_IsRejected( ) {
            var slot = Slot( TestItems.Wood(), 2 );
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( slot, slot ) );
            Assert.AreEqual( 2, slot.Stack.Count );
        }

        [ Test ]
        public void Move_ToEmptySlotThatRefusesTheType_IsRejected( ) {
            var from = Slot( TestItems.Wood(), 2 );
            var to = Slot( allowed: ItemType.Helmet );
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 2, from.Stack.Count );
            Assert.IsTrue( to.IsEmpty );
        }

        [ Test ]
        public void Move_ToOutputOnlySlot_IsRejected( ) {
            var to = Slot();
            to.OutputOnly = true;
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( Slot( TestItems.Wood(), 1 ), to ) );
        }

        [ Test ]
        public void Move_OutOfOutputOnlySlot_IsAllowed( ) {
            var from = Slot( TestItems.Wood(), 2 );
            from.OutputOnly = true;
            var to = Slot();
            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( from, to ) );
        }

        [ Test ]
        public void Move_SameItem_StacksUpToTheLimit( ) {
            var from = Slot( TestItems.Wood(), 7 );
            var to = Slot( TestItems.Wood(), 6 ); // max 10
            Assert.AreEqual( MoveResult.Stacked, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 10, to.Stack.Count );
            Assert.AreEqual( 3, from.Stack.Count );
        }

        [ Test ]
        public void Move_SameItemFitsCompletely_EmptiesTheSource( ) {
            var from = Slot( TestItems.Wood(), 3 );
            var to = Slot( TestItems.Wood(), 4 );
            ItemTransfer.Move( from, to );
            Assert.AreEqual( 7, to.Stack.Count );
            Assert.IsTrue( from.IsEmpty );
        }

        [ Test ]
        public void Move_SameItemOntoAFullStack_IsRejectedWithoutEvents( ) {
            var from = Slot( TestItems.Wood(), 3 );
            var to = Slot( TestItems.Wood(), 10 );
            var events = 0;
            from.Changed += _ => events++;
            to.Changed += _ => events++;
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 0, events );
            Assert.AreEqual( 3, from.Stack.Count );
        }

        [ Test ]
        public void Move_DifferentItems_Swaps( ) {
            var from = Slot( TestItems.Wood(), 3 );
            var to = Slot( TestItems.Stone(), 2 );
            Assert.AreEqual( MoveResult.Swapped, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( "Stone", from.Stack.Id.Value );
            Assert.AreEqual( 2, from.Stack.Count );
            Assert.AreEqual( "Wood", to.Stack.Id.Value );
            Assert.AreEqual( 3, to.Stack.Count );
        }

        [ Test ]
        public void Move_SwapBlockedByTheSourceSlotsRules_IsRejected( ) {
            // e.g. dragging a weapon onto a helmet slot that currently holds a helmet: the helmet cannot go back to a weapon slot.
            var from = Slot( TestItems.Axe(), 1, allowed: ItemType.Tool );
            var to = Slot( TestItems.Helmet(), 1, allowed: ItemType.Helmet | ItemType.Tool );
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( "Axe", from.Stack.Id.Value );
            Assert.AreEqual( "Helmet", to.Stack.Id.Value );
        }

        [ Test ]
        public void Move_SwapBlockedByTheTargetSlotsRules_IsRejected( ) {
            var from = Slot( TestItems.Wood(), 1 );
            var to = Slot( TestItems.Helmet(), 1, allowed: ItemType.Helmet );
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( from, to ) );
        }

        [ Test ]
        public void Move_SwapThatWouldOverfillASlot_IsRejected( ) {
            var from = Slot( TestItems.Wood(), 5 );
            var to = Slot( TestItems.Stone(), 1, max: 1 );
            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( from, to ) );
            Assert.AreEqual( 5, from.Stack.Count );
        }

        [ Test ]
        public void Move_UpdatesBothSlotsBeforeEitherRaisesChanged( ) {
            var from = Slot( TestItems.Wood(), 3 );
            var to = Slot();
            var seenFromWhenToChanged = -1;
            to.Changed += _ => seenFromWhenToChanged = from.Stack.Count;
            ItemTransfer.Move( from, to );
            Assert.AreEqual( 0, seenFromWhenToChanged, "the item must never be visible in both slots" );
        }

        [ Test ]
        public void Move_ListenerThatConsumesFromTheTarget_IsNotOverwritten( ) {
            // A fuel slot burns one unit as soon as it is filled; the move must not restore the pre-burn count.
            var from = Slot( TestItems.Make( "Log", fuel: 5f ), 3 );
            var to = Slot();
            to.Changed += slot => {
                if( slot.Stack.Count == 3 ) {
                    slot.Remove( 1 );
                }
            };
            ItemTransfer.Move( from, to );
            Assert.AreEqual( 2, to.Stack.Count );
        }

        [ Test ]
        public void Move_StackedNutrition_IsAveragedByCount( ) {
            var dish = TestItems.Carrot();
            var from = new ItemSlot();
            from.Set( ItemStack.Restore( dish, 1, 0f, new NutritionOverride( 40, 0, 0 ) ) );
            var to = new ItemSlot();
            to.Set( ItemStack.Restore( dish, 3, 0f, new NutritionOverride( 20, 0, 0 ) ) );
            ItemTransfer.Move( from, to );
            Assert.AreEqual( 25f, to.Stack.Nutrition.Hunger, 0.0001f );
        }

        [ Test ]
        public void ContainerMove_WorksByIndex( ) {
            var container = new ItemContainer( 2 );
            container.Add( TestItems.Wood(), 2 );
            Assert.AreEqual( MoveResult.Moved, container.Move( 0, 1 ) );
            Assert.IsTrue( container[ 0 ].IsEmpty );
            Assert.AreEqual( 2, container[ 1 ].Stack.Count );
        }

        [ Test ]
        public void Move_BetweenTwoContainers_Works( ) {
            var backpack = new ItemContainer( 2 );
            var chest = new ItemContainer( 2 );
            backpack.Add( TestItems.Wood(), 2 );
            var chestEvents = new List<int>();
            chest.SlotChanged += ( index, _ ) => chestEvents.Add( index );
            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( backpack[ 0 ], chest[ 1 ] ) );
            CollectionAssert.AreEqual( new[] { 1 }, chestEvents );
        }
    }
}
