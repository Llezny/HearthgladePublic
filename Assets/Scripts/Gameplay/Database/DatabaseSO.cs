using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hearthglade.Gameplay.Database
{
   public class DatabaseSO : ScriptableObject {
      [field: SerializeField] public ItemSO [] Items;
      [field: SerializeField] public GameObject [] Prefabs;
      [field: SerializeField] public CookingRecipeSO[] CookingRecipes { get; set; }
      [field: SerializeField] public CropSO[] Crops { get; set; }
   }
}
