using System.Collections.Generic;
using Hearthglade.Gameplay.Environment.Cooking;
using Hearthglade.Gameplay.Items;
using UnityEngine;

namespace Hearthglade.Gameplay.Database {
    
    [CreateAssetMenu(fileName = "CookingRecipe", menuName = "ScriptableObjects/CookingRecipe")]
    public class CookingRecipeSO : ScriptableObject {
        public ItemSO TargetItem;
        public float BaseTimeToCookInSeconds;
        public CookingStationLevel MinCookingStationLevel;
        public List<Ingredient> Ingredients;
    }
}
