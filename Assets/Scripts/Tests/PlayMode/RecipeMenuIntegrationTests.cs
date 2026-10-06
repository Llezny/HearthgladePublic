using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Build;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene and drives the crafting and building menus (they share RecipeMenu): the recipe list is built
    /// from the database, the tabs behave, crafting takes the ingredients and delivers the item, and "Build" enters the
    /// building state. Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class RecipeMenuIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "recipe-menu-test", Seed = 12345, SaveDate = "" };
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            float start = Time.realtimeSinceStartup;
            while( !mapManager.IsMapReady ) {
                if( Time.realtimeSinceStartup - start > 120f ) {
                    Assert.Fail( "Timed out waiting for MapManager.IsMapReady" );
                }
                await UniTask.Yield();
            }
            // Menu.Start() (which finds the Content object) runs on the first frame after the scene is loaded
            await UniTask.DelayFrame( 3 );
            return scope;
        }

        private static Button ActionButtonOf( CraftingCard card ) {
            return card.transform.Find( "Fill/Right/CraftButton" ).GetComponent<Button>();
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Crafting_ListsTheRecipes_AndCraftingTakesIngredientsAndDeliversTheItem( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var inventory = scope.Container.Resolve<InventoryService>();
            var crafting = scope.Container.Resolve<Crafting>();

            // opened with an empty inventory: every recipe costs something, so nothing can be crafted
            crafting.OpenMenu();
            await UniTask.DelayFrame( 2 );

            var recipes = RecipiesDatabase.instance.craftingDatabase
                .Where( r => r.isUnlocked && references.Catalog.GetAsset( new Hearthglade.Core.Items.ItemId( r.craftedItemName ) ) != null ).ToList();
            var cards = crafting.GetComponentsInChildren<CraftingCard>( true );
            Assert.AreEqual( recipes.Count, cards.Length, "one card per unlocked crafting recipe" );
            Assert.IsTrue( cards.All( card => !card.CanCraft ), "nothing is affordable with an empty inventory" );

            // give the ingredients of one recipe and reopen (the cards refresh when the menu opens)
            var recipe = recipes[ 0 ];
            crafting.CloseMenu( true );
            await UniTask.Delay( 600 );
            foreach( var requirement in recipe.requirements ) {
                inventory.AddItem( references.Catalog.GetAsset( new Hearthglade.Core.Items.ItemId( requirement.requiredItemName ) ), requirement.requiredQuantity );
            }
            var owned = inventory.Count( recipe.CraftedItem );
            crafting.OpenMenu();
            await UniTask.DelayFrame( 2 );

            var card = cards.First( c => c.Recipe == recipe );
            Assert.IsTrue( card.CanCraft, "the ingredients are in the inventory" );

            ActionButtonOf( card ).onClick.Invoke();
            float start = Time.realtimeSinceStartup;
            while( inventory.Count( recipe.CraftedItem ) < owned + recipe.craftedItemQuantity ) {
                if( Time.realtimeSinceStartup - start > 15f ) {
                    Assert.Fail( "The item was not delivered within 15s (craft time is " + recipe.craftTime + "s)" );
                }
                await UniTask.Yield();
            }
            foreach( var requirement in recipe.requirements ) {
                Assert.AreEqual( 0, inventory.Count( requirement.Item ) - ( requirement.requiredItemName == recipe.craftedItemName ? recipe.craftedItemQuantity : 0 ),
                    requirement.requiredItemName + " was taken as an ingredient" );
            }
        } );

        // Choosing "Build" spawns the building's prefab through the factory, which injects its components (e.g. the chest
        // gets its ChestView): a building that cannot be constructed would throw as soon as the player picks it.
        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator EveryBuilding_CanBeSpawnedThroughTheFactory( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();

            int spawned = 0;
            foreach( var recipe in RecipiesDatabase.instance.buildingsDatabase ) {
                var item = references.Catalog.GetAsset( new Hearthglade.Core.Items.ItemId( recipe.craftedItemName ) ) as BuildableItemSO;
                if( item == null ) {
                    continue; // not listed in the menu either
                }
                Assert.NotNull( item.buildingPrefab, recipe.craftedItemName + " has no building prefab" );
                GameObject instance = null;
                Assert.DoesNotThrow( () => instance = factory.Get( item.buildingPrefab ), recipe.craftedItemName + " could not be spawned" );
                Lean.Pool.LeanPool.Despawn( instance );
                spawned++;
            }
            Assert.Greater( spawned, 0, "no building recipe could be spawned at all" );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Building_ListsTheBuildings_AndBuildEntersTheBuildingState( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var inventory = scope.Container.Resolve<InventoryService>();
            var building = scope.Container.Resolve<BuildingMenu>();
            var gameManager = scope.Container.Resolve<GameManager>();

            var recipes = RecipiesDatabase.instance.buildingsDatabase
                .Where( r => r.isUnlocked && references.Catalog.GetAsset( new Hearthglade.Core.Items.ItemId( r.craftedItemName ) ) != null ).ToList();
            var recipe = recipes[ 0 ];
            foreach( var requirement in recipe.requirements ) {
                inventory.AddItem( references.Catalog.GetAsset( new Hearthglade.Core.Items.ItemId( requirement.requiredItemName ) ), requirement.requiredQuantity );
            }
            building.OpenMenu();
            await UniTask.DelayFrame( 2 );

            var cards = building.GetComponentsInChildren<CraftingCard>( true );
            Assert.AreEqual( recipes.Count, cards.Length, "one card per unlocked building recipe" );
            Assert.IsFalse( building.transform.Find( "Content/Panel/Tabs" ).gameObject.activeSelf,
                "all buildings are one category, so the tab bar is hidden" );

            var card = cards.First( c => c.Recipe == recipe );
            Assert.IsTrue( card.CanCraft, "the ingredients are in the inventory" );
            Assert.AreEqual( "Game", gameManager.CurrentState.StateName );

            ActionButtonOf( card ).onClick.Invoke();
            await UniTask.DelayFrame( 2 );

            Assert.AreEqual( "Building", gameManager.CurrentState.StateName, "Build switches to the building state" );
            foreach( var requirement in recipe.requirements ) {
                Assert.AreEqual( requirement.requiredQuantity, inventory.Count( requirement.Item ),
                    "the ingredients are only taken when the building is placed" );
            }
        } );
    }
}
