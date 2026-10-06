using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class ItemStackTests {

        [ Test ]
        public void ItemId_NullAndEmpty_AreEqual( ) {
            Assert.AreEqual( new ItemId( null ), new ItemId( "" ) );
            Assert.IsTrue( new ItemId( null ).IsEmpty );
        }

        [ Test ]
        public void ItemId_Equality_IsCaseSensitiveAndOrdinal( ) {
            Assert.IsTrue( new ItemId( "Wood" ) == new ItemId( "Wood" ) );
            Assert.IsTrue( new ItemId( "Wood" ) != new ItemId( "wood" ) );
            Assert.AreEqual( new ItemId( "Wood" ).GetHashCode(), new ItemId( "Wood" ).GetHashCode() );
        }

        [ Test ]
        public void Definition_WithoutId_Throws( ) {
            Assert.Throws<System.ArgumentException>( ( ) => TestItems.Make( "" ) );
        }

        [ Test ]
        public void Definition_MaxStackBelowOne_IsClampedToOne( ) {
            Assert.AreEqual( 1, TestItems.Make( "Thing", maxStack: 0 ).MaxStack );
        }

        [ Test ]
        public void Of_NullOrNonPositive_IsEmpty( ) {
            Assert.IsTrue( ItemStack.Of( null, 3 ).IsEmpty );
            Assert.IsTrue( ItemStack.Of( TestItems.Wood(), 0 ).IsEmpty );
            Assert.IsTrue( default( ItemStack ).IsEmpty );
        }

        [ Test ]
        public void Of_DurableItem_StartsAtMaxDurability( ) {
            Assert.AreEqual( 100f, ItemStack.Of( TestItems.Axe() ).Durability );
            Assert.AreEqual( 0f, ItemStack.Of( TestItems.Wood() ).Durability );
        }

        [ Test ]
        public void WithCount_Zero_BecomesEmpty( ) {
            Assert.IsTrue( ItemStack.Of( TestItems.Wood(), 3 ).WithCount( 0 ).IsEmpty );
        }

        [ Test ]
        public void WithCount_KeepsDurabilityAndNutrition( ) {
            var stack = ItemStack.Restore( TestItems.Carrot(), 4, 0f, new NutritionOverride( 1, 2, 3 ) ).WithCount( 2 );
            Assert.AreEqual( 2, stack.Count );
            Assert.IsTrue( stack.HasNutritionOverride );
            Assert.AreEqual( 2f, stack.Nutrition.Thirst );
        }

        [ Test ]
        public void EffectiveNutrition_WithoutOverride_UsesTheItemsOwnValues( ) {
            Assert.AreEqual( 10f, ItemStack.Of( TestItems.Carrot() ).EffectiveNutrition.Hunger );
        }

        [ Test ]
        public void WithAdded_PlainStacks_JustSumsCounts( ) {
            var grown = ItemStack.Of( TestItems.Wood(), 3 ).WithAdded( ItemStack.Of( TestItems.Wood(), 4 ), 4 );
            Assert.AreEqual( 7, grown.Count );
            Assert.IsFalse( grown.HasNutritionOverride );
        }

        [ Test ]
        public void WithAdded_PartOfIncoming_OnlyAddsTheRequestedAmount( ) {
            var grown = ItemStack.Of( TestItems.Wood(), 3 ).WithAdded( ItemStack.Of( TestItems.Wood(), 9 ), 2 );
            Assert.AreEqual( 5, grown.Count );
        }

        [ Test ]
        public void WithAdded_DifferentQuality_AveragesWeightedByCount( ) {
            var own = ItemStack.Restore( TestItems.Carrot(), 3, 0f, new NutritionOverride( 20, 0, 0 ) );
            var incoming = ItemStack.Restore( TestItems.Carrot(), 1, 0f, new NutritionOverride( 40, 0, 0 ) );
            var merged = own.WithAdded( incoming, 1 );
            Assert.AreEqual( 4, merged.Count );
            Assert.AreEqual( 25f, merged.Nutrition.Hunger, 0.0001f );
        }

        [ Test ]
        public void WithAdded_OneSideHasOverride_TheOtherCountsAtItsBaseValue( ) {
            var own = ItemStack.Of( TestItems.Carrot(), 1 );  // base hunger 10
            var incoming = ItemStack.Restore( TestItems.Carrot(), 1, 0f, new NutritionOverride( 30, 0, 0 ) );
            Assert.AreEqual( 20f, own.WithAdded( incoming, 1 ).Nutrition.Hunger, 0.0001f );
        }

        [ Test ]
        public void WithAdded_KeepsTheExistingStacksDurability( ) {
            var worn = ItemStack.Restore( TestItems.Make( "Bow", maxStack: 5, maxDurability: 50f ), 1, 10f, null );
            var grown = worn.WithAdded( ItemStack.Of( TestItems.Make( "Bow", maxStack: 5, maxDurability: 50f ), 1 ), 1 );
            Assert.AreEqual( 10f, grown.Durability );
        }
    }
}
