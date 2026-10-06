
namespace Hearthglade.Core.Items
{
    // What sits in a slot: an item kind plus the per-instance state (count, durability, cooked quality).
    // Immutable; every change returns a new value.
    public readonly struct ItemStack
    {
        public ItemDefinition Definition { get; }
        public int Count { get; }
        public float Durability { get; }
        public bool HasNutritionOverride { get; }
        public NutritionOverride Nutrition { get; }

        private ItemStack( ItemDefinition definition, int count, float durability, bool hasNutritionOverride, NutritionOverride nutrition ) {
            Definition = definition;
            Count = count;
            Durability = durability;
            HasNutritionOverride = hasNutritionOverride;
            Nutrition = nutrition;
        }

        public bool IsEmpty => Definition == null || Count <= 0;

        public ItemId Id => Definition != null ? Definition.Id : default;

        // The cooked quality when the stack carries one, else the item's own values.
        public NutritionOverride EffectiveNutrition => HasNutritionOverride ? Nutrition : Definition != null ? Definition.Nutrition : default;

        public static ItemStack Of( ItemDefinition definition, int count = 1 ) {
            if( definition == null || count <= 0 ) {
                return default;
            }
            return new ItemStack( definition, count, definition.HasDurability ? definition.MaxDurability : 0f, false, default );
        }

        // For restoring saved state, where durability and cooked quality are not the item's defaults.
        public static ItemStack Restore( ItemDefinition definition, int count, float durability, NutritionOverride? nutrition ) {
            if( definition == null || count <= 0 ) {
                return default;
            }
            return new ItemStack( definition, count, durability, nutrition.HasValue, nutrition ?? default );
        }

        public ItemStack WithCount( int count ) {
            return count <= 0 || Definition == null ? default : new ItemStack( Definition, count, Durability, HasNutritionOverride, Nutrition );
        }

        public ItemStack WithNutrition( NutritionOverride nutrition ) {
            return IsEmpty ? this : new ItemStack( Definition, Count, Durability, true, nutrition );
        }

        // Grows this stack by `amount` units taken from `incoming` (same item). The stack keeps its own durability, and when either
        // side carries a cooked quality the result averages them weighted by count instead of dropping the incoming one.
        public ItemStack WithAdded( in ItemStack incoming, int amount ) {
            if( IsEmpty || amount <= 0 ) {
                return this;
            }
            var newCount = Count + amount;
            if( !HasNutritionOverride && !incoming.HasNutritionOverride ) {
                return new ItemStack( Definition, newCount, Durability, false, default );
            }
            var own = EffectiveNutrition;
            var added = incoming.EffectiveNutrition;
            float total = newCount;
            var merged = new NutritionOverride(
                ( own.Hunger * Count + added.Hunger * amount ) / total,
                ( own.Thirst * Count + added.Thirst * amount ) / total,
                ( own.Health * Count + added.Health * amount ) / total );
            return new ItemStack( Definition, newCount, Durability, true, merged );
        }

        public override string ToString() => IsEmpty ? "(empty)" : $"{Id} x {Count}";
    }
}
