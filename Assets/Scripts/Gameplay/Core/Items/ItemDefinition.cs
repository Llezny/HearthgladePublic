using System;

namespace Hearthglade.Core.Items
{
    // Immutable, engine-free description of an item kind. Baked once from the ItemSO asset (see docs/ITEMS_REFACTOR_PLAN.md).
    public sealed class ItemDefinition
    {
        public ItemId Id { get; }
        public ItemType Type { get; }
        public ItemRarity Rarity { get; }
        public int MaxStack { get; }
        public FoodType FoodType { get; }
        public float NutritionQuality { get; }
        public float FuelValue { get; }
        public bool IsUsable { get; }
        public NutritionOverride Nutrition { get; }
        public bool HasDurability { get; }
        public float MaxDurability { get; }
        public ItemTag Tags { get; }

        // What barter prices the item from; 0 = cannot be traded.
        public int BaseValue { get; }

        public ItemDefinition(
            ItemId id,
            ItemType type,
            int maxStack,
            ItemRarity rarity = ItemRarity.Common,
            FoodType foodType = FoodType.None,
            float nutritionQuality = 1f,
            float fuelValue = 0f,
            bool isUsable = false,
            NutritionOverride nutrition = default,
            bool hasDurability = false,
            float maxDurability = 0f,
            ItemTag tags = ItemTag.None,
            int baseValue = 0 ) {
            if( id.IsEmpty ) {
                throw new ArgumentException( "An item needs an id", nameof( id ) );
            }
            Id = id;
            Type = type;
            Rarity = rarity;
            MaxStack = Math.Max( 1, maxStack );
            FoodType = foodType;
            NutritionQuality = nutritionQuality;
            FuelValue = fuelValue;
            IsUsable = isUsable;
            Nutrition = nutrition;
            HasDurability = hasDurability;
            MaxDurability = maxDurability;
            Tags = tags;
            BaseValue = Math.Max( 0, baseValue );
        }

        public bool IsTradable => BaseValue > 0;

        public bool IsFuel => FuelValue > 0f;

        public bool HasTag( ItemTag tag ) => tag != ItemTag.None && ( Tags & tag ) == tag;

        public override string ToString() => Id.ToString();
    }
}
