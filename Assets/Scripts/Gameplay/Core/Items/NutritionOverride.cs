using System;

namespace Hearthglade.Core.Items {

    // The effective hunger/thirst/health value of one consumable, e.g. a cooked dish whose actual
    // stats depend on which ingredients went into it rather than being fixed on its ItemSO.
    [Serializable]
    public struct NutritionOverride {
        public float Hunger;
        public float Thirst;
        public float Health;

        public NutritionOverride( float hunger, float thirst, float health ) {
            Hunger = hunger;
            Thirst = thirst;
            Health = health;
        }
    }
}
