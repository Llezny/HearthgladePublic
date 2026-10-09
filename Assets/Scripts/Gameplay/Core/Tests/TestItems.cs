using Hearthglade.Core.Items;

namespace Hearthglade.Core.Tests {
    // Item definitions for tests; no assets or catalog needed.
    internal static class TestItems {

        public static ItemDefinition Make( string id, int maxStack = 10, ItemType type = ItemType.Resource, FoodType food = FoodType.None,
            float fuel = 0f, NutritionOverride? nutrition = null, float maxDurability = 0f, ItemTag tags = ItemTag.None, int baseValue = 0,
            ItemStats stats = default, Protection protection = default, ToolGroup toolGroup = ToolGroup.None ) {
            return new ItemDefinition( new ItemId( id ), type, maxStack, foodType: food, fuelValue: fuel,
                isUsable: nutrition.HasValue, nutrition: nutrition ?? default, hasDurability: maxDurability > 0f, maxDurability: maxDurability, tags: tags, baseValue: baseValue,
                stats: stats, protection: protection, toolGroup: toolGroup );
        }

        public static ItemDefinition Tool( string id, ItemStats stats, ToolGroup group = ToolGroup.None ) {
            return Make( id, maxStack: 1, type: ItemType.Tool, maxDurability: 100f, stats: stats, toolGroup: group );
        }

        public static ItemDefinition Clothing( string id, ItemType type, Protection protection ) {
            return Make( id, maxStack: 1, type: type, protection: protection );
        }

        public static ItemDefinition Wood() => Make( "Wood", maxStack: 10 );
        public static ItemDefinition Stone() => Make( "Stone", maxStack: 5 );
        public static ItemDefinition Carrot() => Make( "Carrot", maxStack: 10, type: ItemType.Food, food: FoodType.Vegetable, nutrition: new NutritionOverride( 10, 0, 0 ) );
        public static ItemDefinition Axe() => Make( "Axe", maxStack: 1, type: ItemType.Tool, maxDurability: 100f );
        public static ItemDefinition Stool() => Make( "Stool", maxStack: 10, type: ItemType.Buildable );
        public static ItemDefinition Helmet() => Make( "Helmet", maxStack: 1, type: ItemType.Helmet );
    }
}
