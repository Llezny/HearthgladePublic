using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;

namespace Hearthglade.Gameplay.Database
{
    // ItemSO is the authoring asset; the game logic only ever sees the immutable ItemDefinition baked from it.
    public static class ItemDefinitionBaker
    {
        public static ItemDefinition Bake( ItemSO item ) {
            var nutrition = default( NutritionOverride );
            var isUsable = false;
            if( item is IUsableItem usable ) {
                isUsable = true;
                nutrition = new NutritionOverride( usable.HungerHealing, usable.ThirstHealing, usable.HealthHealing );
            }
            var food = item as FoodItemSO;
            return new ItemDefinition(
                new ItemId( item.name ),
                item.itemType,
                item.maxItemsInStack,
                item.ItemRarity,
                food != null ? food.foodType : FoodType.None,
                food != null ? food.NutritionQuality : 1f,
                item.FuelValue,
                isUsable,
                nutrition,
                item.HasDurability,
                item.MaxDurability,
                item.Tags,
                item.BaseValue,
                new ItemStats( item.ChopSpeed, item.HarvestSpeed, item.MineSpeed, item.AttackPower ),
                new Protection( item.ColdProtection, item.HeatProtection, item.DamageProtection ),
                item.ToolGroup );
        }
    }
}
