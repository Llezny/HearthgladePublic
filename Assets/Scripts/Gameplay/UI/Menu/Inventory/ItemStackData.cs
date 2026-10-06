using System;
using Hearthglade.Core.Items;
using Newtonsoft.Json;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    // The saved shape of one stack (inventory, equipment, chest, POI loot): the item id, how many, and the per-instance state.
    [ Serializable ]
    public class ItemStackData {

        public class NutritionData {
            public float Hunger;
            public float Thirst;
            public float Health;
        }

        // The item id (asset name).
        public string Id { get; set; }
        public int Count { get; set; }
        [ JsonProperty( NullValueHandling = NullValueHandling.Ignore ) ] public float? Durability { get; set; }
        [ JsonProperty( NullValueHandling = NullValueHandling.Ignore ) ] public NutritionData Nutrition { get; set; }

        public static ItemStackData From( in ItemStack stack ) {
            if( stack.IsEmpty ) {
                return null;
            }
            return new ItemStackData {
                Id = stack.Id.Value,
                Count = stack.Count,
                Durability = stack.Definition.HasDurability ? stack.Durability : null,
                Nutrition = stack.HasNutritionOverride
                    ? new NutritionData { Hunger = stack.Nutrition.Hunger, Thirst = stack.Nutrition.Thirst, Health = stack.Nutrition.Health }
                    : null,
            };
        }

        // False for an empty entry and for an item the catalog no longer knows.
        public bool TryToStack( IItemCatalog catalog, out ItemStack stack ) {
            stack = default;
            if( string.IsNullOrEmpty( Id ) || Count <= 0 ) {
                return false;
            }
            if( !catalog.TryGet( new ItemId( Id ), out var definition ) ) {
                UnityEngine.Debug.LogError( $"Saved item '{Id}' does not exist any more, skipping it" );
                return false;
            }
            NutritionOverride? nutrition = null;
            if( Nutrition != null ) {
                nutrition = new NutritionOverride( Nutrition.Hunger, Nutrition.Thirst, Nutrition.Health );
            }
            stack = ItemStack.Restore( definition, Count, Durability ?? 0f, nutrition );
            return true;
        }
    }
}
