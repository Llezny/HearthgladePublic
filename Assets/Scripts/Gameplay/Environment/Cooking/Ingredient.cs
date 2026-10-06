using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items.UsableItems;

namespace Hearthglade.Gameplay.Environment.Cooking {
    
    [Serializable]
    public struct Ingredient {
        public FoodType Item;
        public int Quantity;
    }
}