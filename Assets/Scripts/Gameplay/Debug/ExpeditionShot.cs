#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Debug
{
    // Dev tool (editor only): when EXPEDITION_SHOT=<folder> is set, the Game scene plays a new game on its own, sails to expeditions in two
    // directions through the ship service, writes a screenshot of each island, comes home and checks that the progress was saved. Then quits.
    public static class ExpeditionShot
    {
        private const int SHOT_WIDTH = 1600;
        private const int SHOT_HEIGHT = 900;

        private static string Folder => System.Environment.GetEnvironmentVariable( "EXPEDITION_SHOT" );

        [ RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad ) ]
        private static void BeforeScene()
        {
            if( string.IsNullOrEmpty( Folder ) )
            {
                return;
            }
            ResourceLoader.LoadSaveContainer().SaveHeader = new SaveHeader { NewGame = true, PlayerName = "shot", Seed = 12345, SaveDate = "" };
        }

        [ RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.AfterSceneLoad ) ]
        private static void AfterScene()
        {
            if( !string.IsNullOrEmpty( Folder ) )
            {
                Run().Forget();
            }
        }

        private static async UniTaskVoid Run()
        {
            try
            {
                Directory.CreateDirectory( Folder );
                await UniTask.WaitUntil( ( ) => UnityEngine.Object.FindAnyObjectByType<GameplayScope>() != null );
                var scope = UnityEngine.Object.FindAnyObjectByType<GameplayScope>();
                var mapManager = scope.Container.Resolve<MapManager>();
                var shipService = scope.Container.Resolve<ShipService>();
                var expeditions = scope.Container.Resolve<ExpeditionService>();
                var ports = scope.Container.Resolve<PortService>();
                await UniTask.WaitUntil( ( ) => mapManager.IsMapReady );

                CheckPayment( scope, shipService, mapManager );
                CheckShipTree( scope, mapManager );

                foreach( var direction in new[] { Direction.North, Direction.South } )
                {
                    expeditions.SetMaxDepth( direction, 2 );
                    var ship = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();
                    shipService.OnShipInteraction( ship );
                    var destinations = ship.Destinations;
                    int index = -1;
                    for( int i = 0; i < destinations.Count; i++ )
                    {
                        UnityEngine.Debug.Log( $"[ExpeditionShot] destination {i}: {destinations[ i ].displayName} cost {destinations[ i ].cost.Count} entries" );
                        if( destinations[ i ].trip?.Direction == direction )
                        {
                            index = i;
                        }
                    }
                    ship.SelectDestination( index );
                    UnityEngine.Debug.Log( $"[ExpeditionShot] sailing {ship.SelectedDestination.displayName}" );
                    shipService.OnShipUse();
                    await UniTask.WaitUntil( ( ) => mapManager.IsMapReady && !mapManager.CurrentMap.MapType.IsHome );
                    await UniTask.Delay( 1500 );

                    var map = mapManager.CurrentMap;
                    UnityEngine.Debug.Log( $"[ExpeditionShot] arrived on {map.MapType.mapName}, trip {map.ExpeditionTrip}" );
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    var start = map.FindNodeToPlacePlayer().WorldPosition;
                    float centre = ChunkManager.ChunkSize * map.MapType.SizeInChunks / 2f * MapGenerator.TILE_X_OFFSET;
                    player.transform.position = new Vector3( centre, start.y, centre );
                    Physics.SyncTransforms();
                    Camera.main.orthographicSize = 17f / ( SHOT_WIDTH / ( float ) SHOT_HEIGHT );
                    await UniTask.Delay( 3000 );
                    await Capture( Path.Combine( Folder, direction + "3.png" ) );

                    player.transform.position = start;
                    Physics.SyncTransforms();
                    await UniTask.Delay( 500 );
                    shipService.OnShipInteraction( map.mapEntry.GetComponent<Ship>() );
                    shipService.OnReturnHome();
                    await UniTask.WaitUntil( ( ) => mapManager.IsMapReady && mapManager.CurrentMap.MapType.IsHome );
                    await UniTask.Delay( 1000 );
                    UnityEngine.Debug.Log( $"[ExpeditionShot] home again: {direction} depth done {expeditions.Progress.MaxDepthReached( direction )} (expected 3), completed expeditions {ports.CompletedExpeditions}, expedition points {scope.Container.Resolve<ShipTreeService>().Points}" );
                }
            }
            catch( Exception exception )
            {
                UnityEngine.Debug.LogError( "[ExpeditionShot] " + exception );
            }
            finally
            {
                UnityEngine.Debug.Log( "[ExpeditionShot] done" );
                EditorApplication.Exit( 0 );
            }
        }

        // Phase 7: points, ore, buying branches, and what they change (the price of a trip, the backpack, gathering).
        private static void CheckShipTree( GameplayScope scope, MapManager mapManager )
        {
            var tree = scope.Container.Resolve<ShipTreeService>();
            var inventory = scope.Container.Resolve<Hearthglade.Gameplay.UI.Menu.Inventory.InventoryService>();
            var catalog = scope.Container.Resolve<Hearthglade.Gameplay.Database.ItemCatalog>();
            var ship = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();
            var before = ship.Destinations[ 0 ];
            UnityEngine.Debug.Log( $"[ExpeditionShot] tree: {tree.Entries.Count} branches, {tree.Points} points, trip costs {before.foodPoints} food, backpack {inventory.Container.Size} slots, harvest chance {tree.HarvestChance}" );

            tree.SetPoints( 40 );
            foreach( var ore in new[] { "Iron", "Gold", "Crystal" } )
            {
                catalog.TryGet( new Hearthglade.Core.Items.ItemId( ore ), out var definition );
                inventory.Container.Add( definition, 20 );
            }
            foreach( var entry in tree.Entries )
            {
                bool bought = tree.TryBuy( entry );
                UnityEngine.Debug.Log( $"[ExpeditionShot] tree: bought {entry.Asset.DisplayName}: {bought}, level {tree.LevelOf( entry )}/{entry.Node.Tiers.Count}, points left {tree.Points}" );
            }
            var after = ship.Destinations[ 0 ];
            UnityEngine.Debug.Log( $"[ExpeditionShot] tree: trip now costs {after.foodPoints} food (was {before.foodPoints}) and {after.cost.Count} item kinds, backpack {inventory.Container.Size} slots (expected 55), harvest chance {tree.HarvestChance} (expected 0.1), cost share {tree.CostShare}" );

            var menu = UnityEngine.Object.FindFirstObjectByType<Hearthglade.Gameplay.UI.Menu.Ship.ShipTreeMenu>( FindObjectsInactive.Include );
            menu.OpenMenu();
            UnityEngine.Debug.Log( "[ExpeditionShot] tree: menu opened" );
            menu.CloseMenu();
            tree.SetPoints( 0 );
        }

        // Phase 2: the food budget is paid out of the backpack, nothing is taken when it is short.
        private static void CheckPayment( GameplayScope scope, ShipService shipService, MapManager mapManager )
        {
            var inventory = scope.Container.Resolve<Hearthglade.Gameplay.UI.Menu.Inventory.InventoryService>();
            var catalog = scope.Container.Resolve<Hearthglade.Gameplay.Database.ItemCatalog>();
            var ship = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();
            var trip = ship.Destinations[ 0 ];
            UnityEngine.Debug.Log( $"[ExpeditionShot] payment: {trip.displayName} costs {trip.foodPoints} food + {trip.cost.Count} item kinds; food in the backpack {inventory.FoodPointsAvailable}, can pay {inventory.CanPay( trip.foodPoints, trip.cost )}" );

            var food = catalog.All.FirstOrDefault( item => item.Nutrition.Hunger >= 3f && item.Nutrition.Hunger <= 8f );
            catalog.TryGet( new Hearthglade.Core.Items.ItemId( "Wood" ), out var wood );
            inventory.Container.Add( food, 12 );
            inventory.Container.Add( wood, 5 );
            UnityEngine.Debug.Log( $"[ExpeditionShot] payment: added 12 x {food.Id} ({food.Nutrition.Hunger} hunger each) and 5 wood; food now {inventory.FoodPointsAvailable}, can pay {inventory.CanPay( trip.foodPoints, trip.cost )}" );
            bool paid = inventory.TryPay( trip.foodPoints, trip.cost );
            UnityEngine.Debug.Log( $"[ExpeditionShot] payment: paid {paid}, food left {inventory.FoodPointsAvailable} (expected {12 * food.Nutrition.Hunger - trip.foodPoints} or a little more), {food.Id} left {inventory.Count( food.Id )}, wood left {inventory.Count( wood.Id )} (expected 3)" );
        }

        private static async UniTask Capture( string path )
        {
            var cam = Camera.main;
            var texture = new RenderTexture( SHOT_WIDTH, SHOT_HEIGHT, 24 );
            cam.targetTexture = texture;
            await UniTask.Delay( 300 );
            // Batch mode has no Game View, so end-of-frame never comes; render by hand.
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D( texture.width, texture.height, TextureFormat.RGB24, false );
            image.ReadPixels( new Rect( 0, 0, texture.width, texture.height ), 0, 0 );
            image.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            File.WriteAllBytes( path, image.EncodeToPNG() );
            UnityEngine.Debug.Log( "[ExpeditionShot] saved " + path );
        }
    }
}
#endif
