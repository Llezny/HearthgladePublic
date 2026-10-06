using System.Collections.Generic;
using CaptureTheFlag.Common;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.BuildableItems;
using UnityEngine;

namespace Hearthglade.Gameplay.Database
{
    public class References : MonoBehaviour {

        [ SerializeField ] DatabaseSO itemsDatabaseSO;
        Dictionary<string, GameObject> objectDatabase = new Dictionary<string, GameObject>();
        Dictionary<string, BuildableItemSO> buildableByPrefabName = new Dictionary<string, BuildableItemSO>();
        private ItemCatalog itemCatalog;
        private CropCatalog cropCatalog;
        private Dictionary<ItemSO, float> grassClearRadii;
        private bool isSetup = false;

        public ItemCatalog Catalog {
            get {
                EnsureSetup();
                return itemCatalog;
            }
        }

        public CropCatalog Crops {
            get {
                EnsureSetup();
                return cropCatalog;
            }
        }

        [field:SerializeField]
        public List<CookingRecipeSO> CookingRecipeDatabase { get; private set; }

        private void Awake() {
            Setup();
        }

        void Setup() {
            if( isSetup ) {
                return;
            }
            isSetup = true;

            foreach( var obj in itemsDatabaseSO.Prefabs )
                objectDatabase.Add( obj.name, obj );

            itemCatalog = new ItemCatalog( itemsDatabaseSO.Items );

            // So a placed piece can be found again from its saved prefab name alone (docs/BUILDING_SYSTEM_PLAN.md
            // Phase 4: restoring Map.BuildGrid occupancy from save data, without a dedicated save field).
            foreach( var item in itemsDatabaseSO.Items ) {
                if( item is BuildableItemSO buildable && buildable.buildingPrefab != null ) {
                    buildableByPrefabName.TryAdd( buildable.buildingPrefab.name, buildable );
                }
            }

            CookingRecipeDatabase = new List<CookingRecipeSO>( itemsDatabaseSO.CookingRecipes );
            cropCatalog = new CropCatalog( itemsDatabaseSO.Crops ?? new CropSO[ 0 ] );
        }

        // Awake() on this component should always run first (DI consumers depend on that).
        // If a query lands here before that happened, resolution order broke somewhere -
        // log it loudly instead of failing silently, then self-heal so the game keeps working.
        private void EnsureSetup() {
            if( isSetup ) {
                return;
            }
            UnityEngine.Debug.LogWarning(
                $"{nameof( References )} was queried before its Awake() ran - forcing Setup() now. " +
                "This means something resolved References too early (check VContainer registration/resolution order)." );
            Setup();
        }

        /// <summary>True when a prefab is registered under that name; unlike <see cref="TryGetGameObject"/> it does not log an error.</summary>
        public bool HasGameObject( string gameObjectName ) {
            EnsureSetup();
            return gameObjectName != null && ( objectDatabase.ContainsKey( gameObjectName ) || objectDatabase.ContainsKey( gameObjectName + "(Clone)" ) );
        }

        public bool TryGetGameObject(string gameObjetName, out GameObject gameObject) {
            EnsureSetup();
            if( objectDatabase.TryGetValue( gameObjetName, out gameObject ) ){
                return true;
            }
            if( objectDatabase.TryGetValue( gameObjetName + "(Clone)", out gameObject ) ) {
                return true;
            }
            UnityEngine.Debug.LogError("There is no such key: " + gameObjetName);
            return false;
        }

        /// <summary>Whether this building's recipe asks to clear the grass around it (<c>Recipe.clearsGrass</c>), and how far (<c>grassClearRadius</c>).</summary>
        public bool TryGetGrassClearRadius( BuildableItemSO item, out float radius ) {
            EnsureSetup();
            var recipes = RecipiesDatabase.instance != null ? RecipiesDatabase.instance.buildingsDatabase : null;
            if( recipes == null ) {
                radius = 0f;
                return false;
            }
            if( grassClearRadii == null ) {
                grassClearRadii = new Dictionary<ItemSO, float>();
                foreach( var recipe in recipes ) {
                    if( recipe.clearsGrass && itemCatalog.GetAsset( recipe.CraftedItem ) is ItemSO crafted ) {
                        grassClearRadii[ crafted ] = recipe.grassClearRadius;
                    }
                }
            }
            return grassClearRadii.TryGetValue( item, out radius );
        }

        /// <summary>The buildable item whose prefab is named <paramref name="prefabName"/>, if any.</summary>
        public bool TryGetBuildableItem( string prefabName, out BuildableItemSO item ) {
            EnsureSetup();
            if( prefabName == null ) {
                item = null;
                return false;
            }
            return buildableByPrefabName.TryGetValue( prefabName, out item ) || buildableByPrefabName.TryGetValue( prefabName + "(Clone)", out item );
        }


    }
}
