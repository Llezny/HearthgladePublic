using System.Linq;
using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class FoodBudgetTests {

        private static ItemDefinition Food( string id, float hunger, int baseValue ) =>
            TestItems.Make( id, type: ItemType.Food, food: FoodType.Vegetable, nutrition: new NutritionOverride( hunger, 0, 0 ), baseValue: baseValue );

        private static readonly ItemDefinition Apple = Food( "Apple", 4, 8 );        // 2 value per point
        private static readonly ItemDefinition Bread = Food( "Bread", 8, 40 );       // 5 per point
        private static readonly ItemDefinition Stew = Food( "Stew", 15, 30 );        // 2 per point
        private static readonly ItemDefinition Stone = TestItems.Make( "Stone" );

        private static ItemContainer Backpack( params ItemStack[] stacks ) {
            var container = new ItemContainer( 10 );
            foreach( var stack in stacks ) {
                container.Add( stack );
            }
            return container;
        }

        private static int Taken( FoodPlan plan, ItemContainer container, ItemDefinition item ) =>
            plan.Takes.Where( t => container[ t.SlotIndex ].Stack.Id == item.Id ).Sum( t => t.Count );

        [Test]
        public void Available_CountsOnlyFoodByItsHunger() {
            var container = Backpack( ItemStack.Of( Apple, 3 ), ItemStack.Of( Bread, 2 ), ItemStack.Of( Stone, 9 ) );
            Assert.AreEqual( 3 * 4 + 2 * 8, FoodBudget.Available( container ), 1e-3 );
        }

        [Test]
        public void TryPlan_NotEnoughFood_FailsAndTouchesNothing() {
            var container = Backpack( ItemStack.Of( Apple, 2 ) );
            Assert.IsFalse( FoodBudget.TryPlan( container, 20, out var plan ) );
            Assert.IsNull( plan );
            Assert.AreEqual( 2, container.Count( Apple.Id ) );
        }

        [Test]
        public void TryPlan_ZeroBudget_IsAnEmptyPlan() {
            Assert.IsTrue( FoodBudget.TryPlan( Backpack(), 0, out var plan ) );
            Assert.AreEqual( 0, plan.Takes.Count );
        }

        [Test]
        public void TryPlan_ExactBudget_WastesNothing() {
            var container = Backpack( ItemStack.Of( Apple, 5 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 20, out var plan ) );
            Assert.AreEqual( 5, Taken( plan, container, Apple ) );
            Assert.AreEqual( 0f, plan.Waste, 1e-3 );
        }

        [Test]
        public void TryPlan_TheLastUnitIsTheOneThatWastesLeast() {
            // 10 points: two apples cover 8, then a third apple (12 in all) wastes 2 where the bread (16 in all) would waste 6.
            var container = Backpack( ItemStack.Of( Apple, 3 ), ItemStack.Of( Bread, 1 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 10, out var plan ) );
            Assert.AreEqual( 12f, plan.Covered, 1e-3 );
            Assert.AreEqual( 3, Taken( plan, container, Apple ) );
            Assert.AreEqual( 0, Taken( plan, container, Bread ) );
        }

        [Test]
        public void TryPlan_SpendsTheCheapestFoodPerPointFirst() {
            // Bread costs 5 per point, apples and stew 2: the bread stays unless it is needed.
            var container = Backpack( ItemStack.Of( Bread, 5 ), ItemStack.Of( Apple, 6 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 24, out var plan ) );
            Assert.AreEqual( 6, Taken( plan, container, Apple ) );
            Assert.AreEqual( 0, Taken( plan, container, Bread ) );
        }

        [Test]
        public void TryPlan_UsesMoreThanOneStackWhenOneIsNotEnough() {
            var container = Backpack( ItemStack.Of( Apple, 2 ), ItemStack.Of( Bread, 3 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 30, out var plan ) );
            Assert.GreaterOrEqual( plan.Covered, 30f );
            Assert.AreEqual( 2, Taken( plan, container, Apple ) );
            Assert.AreEqual( 3, Taken( plan, container, Bread ) );
        }

        [Test]
        public void TryPlan_ACookedDishCountsByItsOwnValue() {
            var plain = ItemStack.Of( Apple, 2 );
            var cooked = ItemStack.Of( Apple, 1 ).WithNutrition( new NutritionOverride( 12, 0, 0 ) );
            var container = new ItemContainer( 4 );
            container[ 0 ].Set( plain );
            container[ 1 ].Set( cooked );
            Assert.AreEqual( 2 * 4 + 12, FoodBudget.Available( container ), 1e-3 );
            Assert.IsTrue( FoodBudget.TryPlan( container, 12, out var plan ) );
            Assert.AreEqual( 12f, plan.Covered, 1e-3 );
        }

        [Test]
        public void Apply_RemovesExactlyThePlannedUnits() {
            var container = Backpack( ItemStack.Of( Apple, 5 ), ItemStack.Of( Bread, 2 ), ItemStack.Of( Stone, 4 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 20, out var plan ) );
            FoodBudget.Apply( container, plan );
            Assert.AreEqual( 0, container.Count( Apple.Id ) );
            Assert.AreEqual( 2, container.Count( Bread.Id ) );
            Assert.AreEqual( 4, container.Count( Stone.Id ) );
        }

        [Test]
        public void TryPlan_IsDeterministic() {
            var container = Backpack( ItemStack.Of( Stew, 2 ), ItemStack.Of( Apple, 4 ), ItemStack.Of( Bread, 3 ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 33, out var a ) );
            Assert.IsTrue( FoodBudget.TryPlan( container, 33, out var b ) );
            CollectionAssert.AreEqual( a.Takes.Select( t => ( t.SlotIndex, t.Count ) ), b.Takes.Select( t => ( t.SlotIndex, t.Count ) ) );
        }
    }
}
