using System.IO;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // The content of hunting (docs/EQUIPMENT_PLAN.md, phase 7): raw and roasted poultry with the icons of Tools/Agent Tools/Blender/poultry_icons.py,
    // the fireplace recipe that roasts the one into the other, and the hunt data of the animals: the hen can be hunted, the deer not yet (it runs
    // away before the player gets near; sneaking or throwing comes later, and venison with it). Existing assets are updated in place.
    public static class HuntingAssetBuilder
    {
        private const string IconFolder = "Assets/Arts/Sprites/ItemIcons/Usable";
        private const string ItemFolder = "Assets/ScriptableObjects/Items/Usable";
        private const string RecipeFolder = "Assets/ScriptableObjects/CookingRecipes";
        private const string HenPrefabPath = "Assets/_Prefabs/Entities/Hen.prefab";
        private const string DeerPrefabPath = "Assets/_Prefabs/Entities/Deer.prefab";
        // Another cooked dish: the item type and the sound of eating are taken from it.
        private const string ModelDishPath = ItemFolder + "/BakedCarrot.asset";

        // Two thrusts of the stone spear (AttackPower 6).
        private const float HenHealth = 10f;

        [ MenuItem( "Tools/Agent Tools/Items/Build hunting content" ) ]
        public static void BuildAll()
        {
            var model = AssetDatabase.LoadAssetAtPath<UsableItem>( ModelDishPath );
            if( model == null )
            {
                throw new System.InvalidOperationException( $"No dish at {ModelDishPath} to take the item type and the eating sound from" );
            }
            // Raw poultry fills a little and upsets the stomach; roasted it is a proper meal.
            var raw = BuildFood( "RawPoultry", "Raw Poultry", "Meat of a hen. Better roasted over a fire.", model, FoodType.Meat, hunger: 6f, thirst: 0f, health: -4f,
                quality: 0.5f, value: 6 );
            // A dish is not an ingredient again (like the baked carrot), or the fireplace would roast it once more.
            var roasted = BuildFood( "RoastedPoultry", "Roasted Poultry", "A hen leg roasted over the fire. Filling.", model, FoodType.None, hunger: 30f, thirst: 0f,
                health: 6f, quality: 1f, value: 16 );
            BuildRecipe( "RoastedPoultryRecipe", roasted );
            SetHunt( HenPrefabPath, huntable: true, raw, meatCount: 1, message: "" );
            SetHunt( DeerPrefabPath, huntable: false, null, meatCount: 0, message: "It's too quick for me to catch." );
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( "[HuntingAssetBuilder] poultry, its recipe and the hunt data of the animals are ready" );
        }

        private static UsableItem BuildFood( string name, string title, string description, UsableItem model, FoodType foodType, float hunger, float thirst, float health,
            float quality, int value )
        {
            var path = $"{ItemFolder}/{name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<UsableItem>( path );
            if( item == null )
            {
                item = ScriptableObject.CreateInstance<UsableItem>();
                AssetDatabase.CreateAsset( item, path );
                item.ItemRarity = ItemRarity.Common;
            }
            item.icon = StoneToolAssetBuilder.ImportIcon( $"{IconFolder}/{name}.png" );
            item.itemName = title;
            item.itemDescription = description;
            item.itemType = model.itemType;
            item.maxItemsInStack = model.maxItemsInStack;
            item.BaseValue = value;
            item.SfxOnUse = model.SfxOnUse;
            var serialized = new SerializedObject( item );
            serialized.FindProperty( "foodType" ).intValue = ( int ) foodType;
            serialized.FindProperty( "NutritionQuality" ).floatValue = quality;
            serialized.FindProperty( "hungerHealing" ).floatValue = hunger;
            serialized.FindProperty( "thirstHealing" ).floatValue = thirst;
            serialized.FindProperty( "healthHealing" ).floatValue = health;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty( item );
            return item;
        }

        // One piece of meat on the fireplace (it has one ingredient slot).
        private static void BuildRecipe( string name, ItemSO dish )
        {
            Directory.CreateDirectory( RecipeFolder );
            var path = $"{RecipeFolder}/{name}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipeSO>( path );
            if( recipe == null )
            {
                recipe = ScriptableObject.CreateInstance<CookingRecipeSO>();
                AssetDatabase.CreateAsset( recipe, path );
            }
            var serialized = new SerializedObject( recipe );
            serialized.FindProperty( "TargetItem" ).objectReferenceValue = dish;
            serialized.FindProperty( "BaseTimeToCookInSeconds" ).floatValue = 12f;
            serialized.FindProperty( "MinCookingStationLevel" ).intValue = 1; // CookingStationLevel.Fireplace
            var ingredients = serialized.FindProperty( "Ingredients" );
            ingredients.arraySize = 1;
            ingredients.GetArrayElementAtIndex( 0 ).FindPropertyRelative( "Item" ).intValue = ( int ) FoodType.Meat;
            ingredients.GetArrayElementAtIndex( 0 ).FindPropertyRelative( "Quantity" ).intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty( recipe );
        }

        private static void SetHunt( string prefabPath, bool huntable, ItemSO meat, int meatCount, string message )
        {
            var root = PrefabUtility.LoadPrefabContents( prefabPath );
            try
            {
                var animal = root.GetComponentInChildren<PassiveAnimal>( true );
                if( animal == null )
                {
                    throw new System.InvalidOperationException( $"{prefabPath} has no PassiveAnimal" );
                }
                var hunt = new SerializedObject( animal ).FindProperty( "hunt" );
                hunt.FindPropertyRelative( "Huntable" ).boolValue = huntable;
                hunt.FindPropertyRelative( "Health" ).floatValue = HenHealth;
                hunt.FindPropertyRelative( "Meat" ).objectReferenceValue = meat;
                hunt.FindPropertyRelative( "MeatCount" ).intValue = meatCount;
                hunt.FindPropertyRelative( "NotHuntableMessage" ).stringValue = message;
                hunt.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset( root, prefabPath );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }
    }
}
