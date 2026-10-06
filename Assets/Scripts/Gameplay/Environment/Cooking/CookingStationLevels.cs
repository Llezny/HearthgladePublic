namespace Hearthglade.Gameplay.Environment.Cooking {
    // Fireplace and Pot are the same physical CookingStation upgraded in place (drop a Pot on a lit
    // fireplace); StoneOven is a separate placeable structure built at that level from the start.
    public enum CookingStationLevel {
        Fireplace = 1,
        Pot = 2,
        StoneOven = 3,
    }

    public static class CookingStationLevelExtensions {
        public const int MaxIngredientSlots = 4;

        // How many ingredient slots are usable at each tier - see docs discussion: campfire only cooks
        // single-ingredient recipes, adding a Pot unlocks the remaining slots for combined dishes.
        public static int IngredientSlotCount( this CookingStationLevel level ) {
            return level switch {
                CookingStationLevel.Fireplace => 1,
                CookingStationLevel.Pot => MaxIngredientSlots,
                CookingStationLevel.StoneOven => MaxIngredientSlots,
                _ => 1,
            };
        }
    }
}