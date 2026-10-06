using System.IO;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class EditorScripts : UnityEditor.Editor {

        public const string SO_PATH = "Assets/ScriptableObjects/";
        public const string PREFAB_PATH = "Assets/_Prefabs/";

        public static T[] GetAllInstances<T>( string [] searchInPaths = null ) where T : Object {
            string[] guids = AssetDatabase.FindAssets("t:"+ typeof(T).Name, searchInPaths ?? new [] { "Assets/" } );

            T[] a = new T[guids.Length];
            for(int i =0;i<guids.Length;i++)         //probably could get optimized 
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                a[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return a;
        }

        [MenuItem("LuakszTools/References/Refresh items database")]
        public static void RefreshItemsDatabase() {
            var itemDatabaseSO = CreateInstance<DatabaseSO>();
            FindAllItemSO( itemDatabaseSO );
            FindAllItemPrefabs( itemDatabaseSO );
            FindAllCookingRecipes( itemDatabaseSO );
            FindAllCrops( itemDatabaseSO );
            CreateOrReplaceAsset( itemDatabaseSO, SO_PATH + "Configs/DatabaseSO.asset" );
        }

        public static void FindAllItemPrefabs( DatabaseSO itemDatabase ) {
            itemDatabase.Prefabs = GetAllInstances<GameObject>( new [] { PREFAB_PATH } );
        }

        public static void FindAllCrops( DatabaseSO itemDatabase ) {
            itemDatabase.Crops = GetAllInstances<CropSO>( new [] { SO_PATH } );
        }

        public static void FindAllCookingRecipes( DatabaseSO itemDatabase ) {
            itemDatabase.CookingRecipes = GetAllInstances<CookingRecipeSO>( new [] { SO_PATH } );
        }
        
        public static void FindAllItemSO( DatabaseSO itemDatabase ) {
            itemDatabase.Items = GetAllInstances<ItemSO>( new [] { SO_PATH } );
        }
        
        private static T CreateOrReplaceAsset<T>(T asset, string path) where T : Object
        {
            T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
 
            if (existingAsset == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                asset.name = Path.GetFileNameWithoutExtension(path);
            }
            else
            {
                EditorUtility.CopySerialized(asset, existingAsset);
                existingAsset.name = Path.GetFileNameWithoutExtension(path);
            }

            return existingAsset;
        }

    }
}
