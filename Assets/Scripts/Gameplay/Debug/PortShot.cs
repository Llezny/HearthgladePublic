#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Debug
{
    // Dev tool (editor only, delete when the harbour is judged): when PORT_SHOT=<folder> is set, the Game scene plays a new game on its own,
    // sails to Mosshollow and writes screenshots of the island from the game camera, then quits the editor.
    public static class PortShot
    {
        private const int SHOT_WIDTH = 1600;
        private const int SHOT_HEIGHT = 900;

        private static string Folder => System.Environment.GetEnvironmentVariable( "PORT_SHOT" );

        // PORT_SHOT_PORT picks the port to visit (id of its PortSO, default Mosshollow).
        private static string PortName => System.Environment.GetEnvironmentVariable( "PORT_SHOT_PORT" ) is { Length: > 0 } name ? name : "Mosshollow";

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
                await UniTask.WaitUntil( ( ) => UnityEngine.Object.FindFirstObjectByType<GameplayScope>() != null );
                var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
                var mapManager = scope.Container.Resolve<MapManager>();
                await UniTask.WaitUntil( ( ) => mapManager.IsMapReady );

                scope.Container.Resolve<PortService>().SetCompletedExpeditions( 7 );
                var shipService = scope.Container.Resolve<ShipService>();
                var ship = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();
                shipService.OnShipInteraction( ship );
                var destinations = ship.Destinations;
                for( int i = 0; i < destinations.Count; i++ )
                {
                    if( destinations[ i ].IsPort && destinations[ i ].portId == PortName )
                    {
                        ship.SelectDestination( i );
                    }
                }
                shipService.OnShipUse();
                await UniTask.WaitUntil( ( ) => mapManager.IsMapReady && mapManager.CurrentMap.MapType.mapName == PortName );
                await UniTask.Delay( 1500 );
                CheckContracts( scope );

                var map = mapManager.CurrentMap;
                var start = map.FindNodeToPlacePlayer().WorldPosition;
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                const float cell = 0.3675f;
                Vector3 At( float cellsX, float cellsZ ) => start + new Vector3( ( 14f + cellsX ) * cell, 0f, cellsZ * cell );

                // x, z in cells from the centre of the harbour site; half width of the picture in metres.
                var shots = new[]
                {
                    ( "shore", At( -16f, 0f ), 2.2f ),
                    ( "street", At( -6f, 0f ), 2.2f ),
                    ( "plaza", At( 6f, 0f ), 2.2f ),
                    ( "stalls", At( 6f, 3f ), 1.4f ),
                    ( "trading_house", At( 13f, 0f ), 2.2f ),
                    ( "overview", At( -2f, 0f ), 6.5f ),
                    ( "overview_east", At( 8f, 0f ), 6.5f ),
                };
                foreach( var ( name, position, halfWidth ) in shots )
                {
                    Teleport( player, position );
                    SetHalfWidth( halfWidth );
                    await UniTask.Delay( 3000 );
                    await Capture( Path.Combine( Folder, name + ".png" ) );
                }

                // The player walks up to the orchardist from the east: they turn to the player and wave.
                var brain = UnityEngine.Object.FindObjectsByType<Hearthglade.Gameplay.Characters.NpcBrain>( FindObjectsSortMode.None )
                    .OrderBy( b => Vector3.Distance( b.transform.position, At( 3.2f, 1.9f ) ) ).First();
                Teleport( player, brain.transform.position + new Vector3( 0.44f, 0f, 0f ) );
                SetHalfWidth( 0.9f );
                await UniTask.Delay( 700 );
                await Capture( Path.Combine( Folder, "greeting.png" ) );
                await UniTask.Delay( 1500 );
                var body = brain.transform.Find( "Model" );
                var toPlayer = player.transform.position - body.position;
                toPlayer.y = 0f;
                UnityEngine.Debug.Log( $"[PortShot] trader faces the player: angle {Vector3.Angle( body.forward, toPlayer ):F0} degrees" );
            }
            catch( Exception exception )
            {
                UnityEngine.Debug.LogError( "[PortShot] " + exception );
            }
            finally
            {
                UnityEngine.Debug.Log( "[PortShot] done" );
                EditorApplication.Exit( 0 );
            }
        }

        // Phase 6: the orders of the port, handing one in, and the trade menu with its orders list opening without errors.
        private static void CheckContracts( GameplayScope scope )
        {
            var ports = scope.Container.Resolve<PortService>();
            var trade = scope.Container.Resolve<TradeService>();
            var inventory = scope.Container.Resolve<Hearthglade.Gameplay.UI.Menu.Inventory.InventoryService>();
            var catalog = scope.Container.Resolve<Hearthglade.Gameplay.Database.ItemCatalog>();
            var port = ports.Find( PortName );
            var orders = trade.ContractsOf( port );
            UnityEngine.Debug.Log( $"[PortShot] {PortName}: {orders.Count} order(s) at relation level {port.Profile.LevelFor( port.State.RelationPoints )}, {ports.CompletedExpeditions} expeditions done" );
            foreach( var order in orders )
            {
                UnityEngine.Debug.Log( $"[PortShot] order #{order.Serial}: {order.Count} x {order.Item} for {order.RewardCount} x {order.RewardItem} (+{order.RelationPoints:F0} trust), open until expedition {order.ExpiresAt}" );
            }
            var first = orders[ 0 ];
            catalog.TryGet( first.Item, out var asked );
            inventory.Container.Add( asked, first.Count );
            double trustBefore = port.State.RelationPoints;
            int rewardBefore = inventory.Count( first.RewardItem );
            bool done = trade.TryFulfil( port, first );
            UnityEngine.Debug.Log( $"[PortShot] handed in order #{first.Serial}: {done}, reward {first.RewardItem} {rewardBefore} -> {inventory.Count( first.RewardItem )}, {first.Item} left {inventory.Count( first.Item )}, trust {trustBefore:F0} -> {port.State.RelationPoints:F0}, orders now {port.State.Contracts.Active.Count}" );

            var menu = UnityEngine.Object.FindFirstObjectByType<Hearthglade.Gameplay.UI.Menu.Trade.PortMenu>( FindObjectsInactive.Include );
            menu.Open( port );
            UnityEngine.Debug.Log( "[PortShot] trade menu opened with the orders list" );
            menu.CloseMenu();
        }

        private static void Teleport( PlayerController player, Vector3 position )
        {
            player.transform.position = position;
            if( player.TryGetComponent<Rigidbody>( out var body ) )
            {
                body.position = position;
                if( !body.isKinematic )
                {
                    body.linearVelocity = Vector3.zero;
                }
            }
            Physics.SyncTransforms();
        }

        private static void SetHalfWidth( float halfWidth )
        {
            Camera.main.orthographicSize = halfWidth / ( SHOT_WIDTH / ( float )SHOT_HEIGHT );
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
            UnityEngine.Debug.Log( "[PortShot] saved " + path );
        }
    }
}
#endif
