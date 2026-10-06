using Hearthglade.Core.Items;
using UnityEngine;

namespace Hearthglade.Gameplay.Items.UsableItems {
    [CreateAssetMenu(fileName = "NewFoodItem", menuName = "ScriptableObjects/Items/Food")]
    public class FoodItemSO : ItemSO {
        public FoodType foodType;

        [Tooltip( "Relative nutritional quality of this specific ingredient (e.g. chicken > venison). " +
                   "Used as a multiplier on a recipe's base healing when this item is consumed as an ingredient." )]
        public float NutritionQuality = 1f;
    }

}
