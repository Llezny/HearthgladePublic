using System.Collections.Generic;
using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemContainerTests {

        private static readonly ItemId Wood = new ItemId( "Wood" );
        private static readonly ItemId Stone = new ItemId( "Stone" );

        [ Test ]
        public void Add_IntoEmptyContainer_UsesTheFirstSlot( ) {
            var container = new ItemContainer( 3 );
            var result = container.Add( TestItems.Wood(), 4 );
            Assert.AreEqual( 4, result.Added );
            Assert.IsTrue( result.IsComplete );
            Assert.AreEqual( 4, container[ 0 ].Stack.Count );
        }

        [ Test ]
        public void Add_MoreThanOneStackHolds_SplitsAcrossSlots( ) {
            var container = new ItemContainer( 3 );
            var result = container.Add( TestItems.Wood(), 25 ); // max 10
            Assert.AreEqual( 25, result.Added );
            Assert.AreEqual( 10, container[ 0 ].Stack.Count );
            Assert.AreEqual( 10, container[ 1 ].Stack.Count );
            Assert.AreEqual( 5, container[ 2 ].Stack.Count );
        }

        [ Test ]
        public void Add_ToppingUpAPartialStack_BeatsAnEarlierEmptySlot( ) {
            var container = new ItemContainer( 3 );
            container[ 1 ].Set( ItemStack.Of( TestItems.Wood(), 5 ) );
            container.Add( TestItems.Wood(), 3 );
            Assert.IsTrue( container[ 0 ].IsEmpty );
            Assert.AreEqual( 8, container[ 1 ].Stack.Count );
        }

        [ Test ]
        public void Add_WhenFull_ReportsTheRemainderAndChangesNothingElse( ) {
            var container = new ItemContainer( 1 );
            container.Add( TestItems.Wood(), 8 );
            var result = container.Add( TestItems.Wood(), 5 ); // only 2 more fit
            Assert.AreEqual( 2, result.Added );
            Assert.AreEqual( 3, result.Remainder );
            Assert.IsFalse( result.IsComplete );
            Assert.AreEqual( 10, container[ 0 ].Stack.Count );
        }

        [ Test ]
        public void Add_NoRoomAtAll_AddsNothing( ) {
            var container = new ItemContainer( 1 );
            container.Add( TestItems.Stone(), 1 );
            var result = container.Add( TestItems.Wood(), 1 );
            Assert.AreEqual( 0, result.Added );
            Assert.AreEqual( 1, result.Remainder );
        }

        [ Test ]
        public void Add_NullItem_AddsNothing( ) {
            var result = new ItemContainer( 2 ).Add( null, 3 );
            Assert.AreEqual( 0, result.Added );
            Assert.AreEqual( 3, result.Remainder );
        }

        [ Test ]
        public void Add_ToAStackThatWasWorn_DoesNotResetItsDurability( ) {
            var container = new ItemContainer( 1 );
            var bow = TestItems.Make( "Bow", maxStack: 5, maxDurability: 50f );
            container[ 0 ].Set( ItemStack.Restore( bow, 1, 10f, null ) );
            container.Add( bow, 1 );
            Assert.AreEqual( 2, container[ 0 ].Stack.Count );
            Assert.AreEqual( 10f, container[ 0 ].Stack.Durability );
        }

        [ Test ]
        public void Add_SkipsSlotsThatRefuseTheItem( ) {
            var container = new ItemContainer( new[] { new ItemSlot( ItemType.Helmet ), new ItemSlot() } );
            container.Add( TestItems.Wood(), 2 );
            Assert.IsTrue( container[ 0 ].IsEmpty );
            Assert.AreEqual( 2, container[ 1 ].Stack.Count );
        }

        [ Test ]
        public void Count_SumsAcrossStacks( ) {
            var container = new ItemContainer( 3 );
            container.Add( TestItems.Wood(), 12 );
            container.Add( TestItems.Stone(), 2 );
            Assert.AreEqual( 12, container.Count( Wood ) );
            Assert.AreEqual( 2, container.Count( Stone ) );
            Assert.AreEqual( 0, container.Count( new ItemId( "Iron" ) ) );
        }

        [ Test ]
        public void CountFood_MatchesByFoodTypeFlags( ) {
            var container = new ItemContainer( 3 );
            container.Add( TestItems.Carrot(), 3 );
            container.Add( TestItems.Make( "Apple", type: ItemType.Food, food: FoodType.Fruit ), 2 );
            container.Add( TestItems.Wood(), 9 );
            Assert.AreEqual( 3, container.CountFood( FoodType.Vegetable ) );
            Assert.AreEqual( 5, container.CountFood( FoodType.Vegetable | FoodType.Fruit ) );
            Assert.AreEqual( 5, container.CountFood( FoodType.Any ) );
        }

        [ Test ]
        public void TryRemove_AcrossSeveralStacks_TakesInSlotOrder( ) {
            var container = new ItemContainer( 3 );
            container.Add( TestItems.Wood(), 15 ); // 10 + 5
            Assert.IsTrue( container.TryRemove( Wood, 12 ) );
            Assert.IsTrue( container[ 0 ].IsEmpty );
            Assert.AreEqual( 3, container[ 1 ].Stack.Count );
        }

        [ Test ]
        public void TryRemove_NotEnough_LeavesTheContainerUntouched( ) {
            var container = new ItemContainer( 2 );
            container.Add( TestItems.Wood(), 4 );
            Assert.IsFalse( container.TryRemove( Wood, 5 ) );
            Assert.AreEqual( 4, container.Count( Wood ) );
        }

        [ Test ]
        public void TryRemove_ZeroCount_SucceedsWithoutChanges( ) {
            var container = new ItemContainer( 1 );
            Assert.IsTrue( container.TryRemove( Wood, 0 ) );
        }

        [ Test ]
        public void TryRemoveCost_AllAvailable_PaysEverything( ) {
            var container = new ItemContainer( 3 );
            container.Add( TestItems.Wood(), 6 );
            container.Add( TestItems.Stone(), 3 );
            Assert.IsTrue( container.TryRemove( new[] { new ItemAmount( Wood, 4 ), new ItemAmount( Stone, 3 ) } ) );
            Assert.AreEqual( 2, container.Count( Wood ) );
            Assert.AreEqual( 0, container.Count( Stone ) );
        }

        [ Test ]
        public void TryRemoveCost_OneMissing_PaysNothing( ) {
            var container = new ItemContainer( 3 );
            container.Add( TestItems.Wood(), 6 );
            container.Add( TestItems.Stone(), 1 );
            Assert.IsFalse( container.TryRemove( new[] { new ItemAmount( Wood, 4 ), new ItemAmount( Stone, 3 ) } ) );
            Assert.AreEqual( 6, container.Count( Wood ) );
            Assert.AreEqual( 1, container.Count( Stone ) );
        }

        [ Test ]
        public void TryRemoveCost_SameItemListedTwice_IsSummedBeforeChecking( ) {
            var container = new ItemContainer( 2 );
            container.Add( TestItems.Wood(), 5 );
            Assert.IsFalse( container.TryRemove( new[] { new ItemAmount( Wood, 3 ), new ItemAmount( Wood, 3 ) } ) );
            Assert.AreEqual( 5, container.Count( Wood ) );
        }

        [ Test ]
        public void HasAll_ReflectsTheCost( ) {
            var container = new ItemContainer( 2 );
            container.Add( TestItems.Wood(), 5 );
            Assert.IsTrue( container.HasAll( new[] { new ItemAmount( Wood, 5 ) } ) );
            Assert.IsFalse( container.HasAll( new[] { new ItemAmount( Wood, 6 ) } ) );
            Assert.IsTrue( container.HasAll( new List<ItemAmount>() ) );
        }

        [ Test ]
        public void CanFit_ChecksRoomAcrossSlots( ) {
            var container = new ItemContainer( 2 );
            container.Add( TestItems.Wood(), 8 );
            Assert.IsTrue( container.CanFit( TestItems.Wood(), 12 ) ); // 2 + 10
            Assert.IsFalse( container.CanFit( TestItems.Wood(), 13 ) );
        }

        [ Test ]
        public void SlotChanged_ReportsTheIndexOfTheChangedSlot( ) {
            var container = new ItemContainer( 3 );
            var seen = new List<int>();
            container.SlotChanged += ( index, _ ) => seen.Add( index );
            container[ 2 ].Add( ItemStack.Of( TestItems.Wood(), 1 ) );
            container[ 0 ].Add( ItemStack.Of( TestItems.Wood(), 1 ) );
            CollectionAssert.AreEqual( new[] { 2, 0 }, seen );
        }

        [ Test ]
        public void IndexOf_FindsTheSlot( ) {
            var container = new ItemContainer( 3 );
            Assert.AreEqual( 1, container.IndexOf( container[ 1 ] ) );
            Assert.AreEqual( -1, container.IndexOf( new ItemSlot() ) );
        }

        [ Test ]
        public void Grow_AddsEmptySlotsAtTheEndAndKeepsWhatIsThere( ) {
            var container = new ItemContainer( 2 );
            container[ 1 ].Add( ItemStack.Of( TestItems.Wood(), 3 ) );
            int resized = 0;
            container.Resized += ( ) => resized++;

            container.Grow( 5 );

            Assert.AreEqual( 5, container.Size );
            Assert.AreEqual( 3, container.Count( new ItemId( "Wood" ) ) );
            Assert.IsTrue( container[ 4 ].IsEmpty );
            Assert.AreEqual( 1, resized );
        }

        [ Test ]
        public void Grow_NeverShrinksAndRaisesNothingWhenThereIsNothingToAdd( ) {
            var container = new ItemContainer( 4 );
            int resized = 0;
            container.Resized += ( ) => resized++;
            container.Grow( 4 );
            container.Grow( 2 );
            Assert.AreEqual( 4, container.Size );
            Assert.AreEqual( 0, resized );
        }

        [ Test ]
        public void Grow_NewSlotsReportTheirOwnIndexAndTakeNewItems( ) {
            var container = new ItemContainer( 1 );
            container.Grow( 3 );
            var seen = new List<int>();
            container.SlotChanged += ( index, _ ) => seen.Add( index );
            container[ 2 ].Add( ItemStack.Of( TestItems.Wood(), 1 ) );
            CollectionAssert.AreEqual( new[] { 2 }, seen );
            Assert.IsTrue( container.Add( ItemStack.Of( TestItems.Stone(), 1 ) ).IsComplete );
        }
    }
}
