using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene and drives a garden patch: plant, let the world clock run, harvest, and check that
    /// the crop lives in Map.Farm (it survives the view despawning and a save round trip). The growth rules
    /// themselves are covered without Unity in Core/Tests/FarmModelTests.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class FarmingIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "farming-test", Seed = 12345, SaveDate = "" };
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            float start = Time.realtimeSinceStartup;
            while( !mapManager.IsMapReady ) {
                if( Time.realtimeSinceStartup - start > 120f ) {
                    Assert.Fail( "Timed out waiting for MapManager.IsMapReady" );
                }
                await UniTask.Yield();
            }
            return scope;
        }

        private static Plot SpawnPlot( GameplayScope scope ) {
            var map = scope.Container.Resolve<MapManager>().CurrentMap;
            var references = scope.Container.Resolve<References>();
            var item = references.Catalog.GetAsset( new ItemId( "GardenPatch" ) ) as BuildableItemSO;
            Assert.NotNull( item, "GardenPatch item not found in the database" );
            var player = UnityEngine.Object.FindFirstObjectByType<Hearthglade.Gameplay.Player.Controller.PlayerController>();
            var position = player.transform.position;

            var instance = scope.Container.Resolve<GameObjectFactory>().Get( item.buildingPrefab, position, Quaternion.identity, null );
            var plot = instance.GetComponent<Plot>();
            Assert.NotNull( plot, "the GardenPatch prefab has no Plot component" );
            Assert.IsNotNull( map.AddSceneObject( instance.transform ), "no block under the player to attach the plot to" );
            return plot;
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Farming_PlantGrowHarvest_SurvivesTheViewDespawningAndASave( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var farming = scope.Container.Resolve<FarmingService>();
            var inventory = scope.Container.Resolve<InventoryService>();
            var clock = scope.Container.Resolve<ClockManager>();
            var map = scope.Container.Resolve<MapManager>().CurrentMap;

            var plot = SpawnPlot( scope );
            Assert.IsTrue( map.Farm.HasPlot( plot.Key ), "placing the patch registers its plot in Map.Farm" );
            Assert.IsTrue( map.Farm.Describe( plot.Key, farming.Now ).IsEmpty );

            Assert.IsTrue( farming.Crops.TryGetAsset( new ItemId( "Wheat" ), out var wheat ), "the Wheat crop is missing from the database" );
            Assert.AreEqual( PlantResult.UnknownCrop, farming.Plant( plot.Key, wheat ), "planting needs a seed in the inventory" );

            inventory.AddItem( wheat.seed, 1 );
            Assert.AreEqual( PlantResult.Planted, farming.Plant( plot.Key, wheat ) );
            Assert.AreEqual( 0, farming.OwnedSeeds( wheat ), "the seed is consumed" );
            Assert.IsFalse( farming.CanHarvest( plot.Key, wheat ), "an unripe crop cannot be harvested" );

            // The view is disposable: the crop stays in the model.
            Lean.Pool.LeanPool.Despawn( plot.gameObject );
            Assert.IsFalse( map.Farm.Describe( plot.Key, farming.Now ).IsEmpty, "despawning the view must not lose the crop" );

            // A snapshot round trip keeps the crop and its planting time.
            var restored = FarmModel.FromSnapshot( farming.Crops, new MapModel( map ).farm );
            Assert.AreEqual( map.Farm.Describe( plot.Key, farming.Now ).Stage, restored.Describe( plot.Key, farming.Now ).Stage );

            // Run the world clock past the growing time (wheat: 120 game minutes).
            float multiplier = 150f / ( Time.deltaTime * clock.GetTimeScale() );
            clock.AddTime( multiplier );
            var view = map.Farm.Describe( plot.Key, farming.Now );
            Assert.AreEqual( wheat.maxYield, view.ReadyYield, "the crop ripened while its view was gone" );

            int wheatBefore = inventory.Count( wheat.produce.Id );
            Assert.IsTrue( farming.Harvest( plot.Key, wheat ) );
            Assert.AreEqual( wheatBefore + wheat.maxYield, inventory.Count( wheat.produce.Id ) );
            Assert.IsTrue( map.Farm.Describe( plot.Key, farming.Now ).IsEmpty, "an annual harvest empties the plot" );
        } );
    }
}
