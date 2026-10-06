using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// A port is a persistent island: discovered by completed expeditions, generated once, kept when the player leaves and
    /// sailed back to by the same ship destination. Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class PortTravelTests {

        private const int Seed = 12345;
        private const string PortId = "Mosshollow";

        private static async UniTask WaitUntil( Func<bool> condition, float timeoutSeconds, string what ) {
            float start = Time.realtimeSinceStartup;
            while( !condition() ) {
                if( Time.realtimeSinceStartup - start > timeoutSeconds ) {
                    Assert.Fail( $"Timed out after {timeoutSeconds}s waiting for: {what}" );
                }
                await UniTask.Yield();
            }
        }

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "playmode-test", Seed = Seed, SaveDate = "" };
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            await WaitUntil( () => mapManager.IsMapReady, 120f, "MapManager.IsMapReady" );
            return scope;
        }

        // Sails from Home to the port the way the ship menu does: interact with the ship, pick the port destination, set sail.
        private static async UniTask SailToPort( GameplayScope scope ) {
            var mapManager = scope.Container.Resolve<MapManager>();
            var shipService = scope.Container.Resolve<ShipService>();
            var ship = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();
            shipService.OnShipInteraction( ship );
            var destinations = ship.Destinations;
            int index = Enumerable.Range( 0, destinations.Count ).First( i => destinations[ i ].portId == PortId );
            ship.SelectDestination( index );
            shipService.OnShipUse();
            await WaitUntil( () => mapManager.IsMapReady && mapManager.CurrentMap.MapType.mapName == PortId, 120f, "arrival at the port" );
        }

        private static async UniTask SailHome( GameplayScope scope ) {
            var mapManager = scope.Container.Resolve<MapManager>();
            var shipService = scope.Container.Resolve<ShipService>();
            shipService.OnShipInteraction( mapManager.CurrentMap.mapEntry.GetComponent<Ship>() );
            shipService.OnReturnHome();
            await WaitUntil( () => mapManager.IsMapReady && mapManager.CurrentMap.MapType.IsHome, 120f, "arrival at home" );
        }

        [ UnityTest, Timeout( 600000 ) ]
        public IEnumerator Port_IsDiscovered_Visited_Kept_AndReachedAgainAsTheSameMap( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var mapManager = scope.Container.Resolve<MapManager>();
            var portService = scope.Container.Resolve<PortService>();
            var shipService = scope.Container.Resolve<ShipService>();
            var homeShip = mapManager.CurrentMap.mapEntry.GetComponent<Ship>();

            Assert.IsFalse( homeShip.Destinations.Any( d => d.IsPort ), "no port before the expeditions are done" );
            portService.SetCompletedExpeditions( 2 );
            Assert.IsTrue( homeShip.Destinations.Any( d => d.portId == PortId ), "the port is offered once discovered" );

            await SailToPort( scope );
            var portMap = mapManager.CurrentMap;
            var port = portService.Find( PortId );
            Assert.IsTrue( portMap.MapType.IsPersistent );
            Assert.AreEqual( portMap.MapId, port.State.MapId, "the id of the port's map is remembered" );
            Assert.AreEqual( MapGenerator.GetMapSeed( Seed, PortId, 0 ), portMap.Seed, "a port has a fixed seed" );
            Assert.IsFalse( shipService.CanExplore, "away from Home the only way is back" );
            Assert.IsFalse( portMap.mapEntry.GetComponent<Ship>().Destinations.Any(), "no other destinations from the port" );
            Assert.LessOrEqual( portMap.Models.Values.Sum( m => m.blockObjects?.Count ?? 0 ), 1, "nothing grows on the island but the ship" );

            await SailHome( scope );
            Assert.AreEqual( 3, portService.CompletedExpeditions, "the trip counts as an expedition once the player is home" );
            Assert.IsTrue( mapManager.MapsDictionary.ContainsKey( portMap.MapId ), "the port island is kept" );

            int mapsBefore = mapManager.MapsDictionary.Count;
            await SailToPort( scope );
            Assert.AreEqual( portMap.MapId, mapManager.CurrentMapId, "the same island, not a new one" );
            Assert.AreEqual( mapsBefore, mapManager.MapsDictionary.Count, "no map was generated" );
        } );

        [ UnityTest, Timeout( 600000 ) ]
        public IEnumerator Trader_StandsInThePort_AndABarterSwapsTheGoods( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var portService = scope.Container.Resolve<PortService>();
            var tradeService = scope.Container.Resolve<TradeService>();
            var inventory = scope.Container.Resolve<InventoryService>();
            var catalog = scope.Container.Resolve<ItemCatalog>();
            portService.SetCompletedExpeditions( 2 );

            await SailToPort( scope );
            await WaitUntil( ( ) => UnityEngine.Object.FindObjectsByType<Trader>( FindObjectsSortMode.None ).Length > 0, 30f, "the trader to appear on the island" );
            Assert.IsTrue( UnityEngine.Object.FindFirstObjectByType<Trader>().CanInteract(), "a trader can be talked to in a port" );

            var port = portService.Find( PortId );
            var iron = catalog.GetAsset( new ItemId( "Iron" ) );
            var pear = catalog.GetAsset( new ItemId( "Pear" ) );
            inventory.AddItem( iron, 10 );
            int pearsBefore = inventory.Count( pear.Id );
            int stockBefore = port.State.StockOf( pear.Id );

            var give = new TradeBasket();
            var take = new TradeBasket();
            give.Add( catalog.Get( iron.Id ), 4 );
            take.Add( catalog.Get( pear.Id ), 3 );
            var quote = tradeService.Quote( port, give, take, out bool fits );
            Assert.IsTrue( quote.IsAcceptable && fits, $"the swap is acceptable ({quote.Problem})" );
            Assert.IsTrue( tradeService.TryTrade( port, give, take ) );

            Assert.AreEqual( 6, inventory.Count( iron.Id ), "the iron was given" );
            Assert.AreEqual( pearsBefore + 3, inventory.Count( pear.Id ), "the pears were received" );
            Assert.AreEqual( stockBefore - 3, port.State.StockOf( pear.Id ), "the port has fewer pears" );
            Assert.Greater( port.State.RelationPoints, 0.0, "trading builds trust" );
        } );
    }
}
