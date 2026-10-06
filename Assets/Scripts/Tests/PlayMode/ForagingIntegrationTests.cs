using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene, puts a deer next to a ripe crop and checks what a fence changes: the deer eats an
    /// unfenced crop, and leaves one that is fenced in alone. The decisions themselves are covered without Unity in
    /// Core/Tests/ForageTests.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class ForagingIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "foraging-test", Seed = 12345, SaveDate = "" };
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

        // A ripe wheat bed on the cell next to the player and a deer one cell away from it.
        private static (PlotKey key, Deer deer, Map map) SetUp( GameplayScope scope ) {
            var map = scope.Container.Resolve<MapManager>().CurrentMap;
            var farming = scope.Container.Resolve<FarmingService>();
            var player = UnityEngine.Object.FindFirstObjectByType<Hearthglade.Gameplay.Player.Controller.PlayerController>();
            var cell = MapHelper.WorldPositionToBlockIndex( player.transform.position );
            var key = new PlotKey( cell.x + 1, 0, cell.y );

            Assert.IsTrue( farming.Crops.TryGetAsset( new ItemId( "Wheat" ), out var wheat ) );
            map.Farm.AddPlot( key, PlotType.Bed );
            // Planted long ago, so it is ripe right away.
            map.Farm.TryPlant( key, wheat.Id, farming.Now - 10000 );
            Assert.Greater( map.Farm.Describe( key, farming.Now ).ReadyYield, 0 );

            Assert.IsTrue( scope.Container.Resolve<References>().TryGetGameObject( "Deer", out var deerPrefab ), "the Deer prefab is not in the database" );
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var home = MapHelper.GridToWorldPosition( new Vector2( cell.x + 2, cell.y ) );
            home.y = player.transform.position.y;
            var deer = factory.Get( deerPrefab, home, Quaternion.identity, null ).GetComponent<Deer>();
            // The player stands far away, so nothing frightens the deer.
            deer.Construct( map, new GameObject( "far-away" ).transform );
            return (key, deer, map);
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Foraging_DeerEatsAnUnfencedRipeCrop( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var farming = scope.Container.Resolve<FarmingService>();
            var ( key, _, map ) = SetUp( scope );

            float start = Time.realtimeSinceStartup;
            while( !map.Farm.Describe( key, farming.Now ).IsEmpty ) {
                if( Time.realtimeSinceStartup - start > 120f ) {
                    Assert.Fail( "the deer never ate the ripe wheat" );
                }
                await UniTask.Yield();
            }
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Foraging_AFencedCropIsLeftAlone( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var farming = scope.Container.Resolve<FarmingService>();
            var ( key, _, map ) = SetUp( scope );

            // Fence all four sides of the bed's cell.
            var edges = map.EdgeGrid;
            edges.Place( 1, new BuildEdgeBox( key.X, key.Z, 0, EdgeSide.PlusX, 1, EdgeKind.Fence ) );
            edges.Place( 2, new BuildEdgeBox( key.X - 1, key.Z, 0, EdgeSide.PlusX, 1, EdgeKind.Fence ) );
            edges.Place( 3, new BuildEdgeBox( key.X, key.Z, 0, EdgeSide.PlusZ, 1, EdgeKind.Fence ) );
            edges.Place( 4, new BuildEdgeBox( key.X, key.Z - 1, 0, EdgeSide.PlusZ, 1, EdgeKind.Fence ) );

            float start = Time.realtimeSinceStartup;
            while( Time.realtimeSinceStartup - start < 45f ) {
                await UniTask.Yield();
            }
            Assert.IsFalse( map.Farm.Describe( key, farming.Now ).IsEmpty, "a fenced crop must not be eaten" );
            Assert.Greater( map.Farm.Describe( key, farming.Now ).ReadyYield, 0 );
        } );
    }
}
