#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Housing;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.UI.Menu.Build;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Lean.Touch;
using Hearthglade.Gameplay.Map;
using Newtonsoft.Json;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Debug
{
    // Dev tool (editor only): when HOUSE_SHOT=<folder> is set, the Game scene plays a new game on its own, finds the house on Home, goes in,
    // checks the walk area, comes out again and writes screenshots from the game camera on the way. Then it quits the editor.
    public static class HouseShot
    {
        private const int SHOT_WIDTH = 1600;
        private const int SHOT_HEIGHT = 900;

        private static string Folder => System.Environment.GetEnvironmentVariable( "HOUSE_SHOT" );

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
                await UniTask.Delay( 2000 );

                var map = mapManager.CurrentMap;
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                var start = player.transform.position;
                var houses = UnityEngine.Object.FindObjectsByType<HouseDoor>( FindObjectsSortMode.None );
                UnityEngine.Debug.Log( $"[HouseShot] map {map.MapType.mapName}, player at {start}, {houses.Length} house door(s) in the scene" );
                if( houses.Length == 0 )
                {
                    UnityEngine.Debug.LogError( "[HouseShot] no HouseDoor spawned: the anchored site was not placed (water or unreachable?) or it is outside the first chunks" );
                    await Capture( Path.Combine( Folder, "no_house.png" ) );
                    return;
                }
                var door = houses.OrderBy( d => Vector3.Distance( d.transform.position, start ) ).First();
                UnityEngine.Debug.Log( $"[HouseShot] door at {door.transform.position} on layer {LayerMask.LayerToName( door.gameObject.layer )}, {( door.transform.position - start ) / 0.3675f} cells from the start" );

                // The way a tap would reach it: a ray from the camera must hit the door's collider on the Clickable layer.
                var front = door.transform.position + new Vector3( 0f, 0f, -0.7f );
                Teleport( player, front );
                SetHalfWidth( 1.4f );
                await UniTask.Delay( 2500 );
                await Capture( Path.Combine( Folder, "1_outside.png" ) );
                var screen = Camera.main.WorldToScreenPoint( door.transform.position + Vector3.up * 0.15f );
                bool hit = Physics.Raycast( Camera.main.ScreenPointToRay( screen ), out var rayHit, 100f, LayerMask.GetMask( "Clickable" ) );
                UnityEngine.Debug.Log( $"[HouseShot] a tap on the door hits {( hit ? rayHit.transform.name : "nothing" )}, interactable: {hit && rayHit.transform.GetComponent<IInteractable>() != null}" );

                // The house is solid: the player is stopped by the terrain data (Map.CanMoveTo), so walk at it from every side in small steps.
                var houseProp = UnityEngine.Object.FindFirstObjectByType<Hearthglade.Gameplay.Housing.HouseProp>();
                var centre = houseProp.transform.position;
                var map2d = new System.Text.StringBuilder( "[HouseShot] walkable cells around the house (# blocked, . free, H house pivot):\n" );
                for( int dz = 6; dz >= -7; dz-- )
                {
                    for( int dx = -7; dx <= 7; dx++ )
                    {
                        var at = centre + new Vector3( dx * 0.3675f, 0f, dz * 0.3675f );
                        map2d.Append( dx == 0 && dz == 0 ? 'H' : map.IsWorldPositionWalkable( at ) ? '.' : '#' );
                    }
                    map2d.Append( '\n' );
                }
                UnityEngine.Debug.Log( map2d.ToString() );
                foreach( var (name, from, direction) in new[]
                {
                    ( "the east", new Vector3( 1.6f, 0f, 0f ), Vector3.left ), ( "the west", new Vector3( -1.6f, 0f, 0f ), Vector3.right ),
                    ( "the north", new Vector3( 0f, 0f, 1.2f ), Vector3.back ), ( "the south, beside the porch", new Vector3( 0.9f, 0f, -1.4f ), new Vector3( -0.5f, 0f, 1f ).normalized ),
                } )
                {
                    var pos = centre + from;
                    for( int i = 0; i < 400; i++ )
                    {
                        var next = pos + direction * 0.01f;
                        if( !map.CanMoveTo( pos, next ) )
                        {
                            break;
                        }
                        pos = next;
                    }
                    var offset = pos - centre;
                    UnityEngine.Debug.Log( $"[HouseShot] walking at the house from {name}: stopped at ({offset.x:F2}, {offset.z:F2}) from the pivot (the walls are at x -0.62 .. 0.62, z -0.40 .. 0.40)" );
                }
                var toDoor = centre + new Vector3( 0f, 0f, -1.2f );
                for( int i = 0; i < 400; i++ )
                {
                    var next = toDoor + Vector3.forward * 0.01f;
                    if( !map.CanMoveTo( toDoor, next ) )
                    {
                        break;
                    }
                    toDoor = next;
                }
                UnityEngine.Debug.Log( $"[HouseShot] walking straight at the door stops {Vector3.Distance( toDoor, door.transform.position ):F2} m from it (a tap needs {door.InteractionDistance:F2})" );

                // Outdoors the building menu lists the buildings of the island and none of the furniture.
                var outsideMenu = UnityEngine.Object.FindFirstObjectByType<BuildingMenu>( FindObjectsInactive.Include );
                outsideMenu.OpenMenu();
                await UniTask.Delay( 400 );
                var outsideCards = UnityEngine.Object.FindObjectsByType<CraftingCard>( FindObjectsInactive.Exclude, FindObjectsSortMode.None ).Select( card => card.Item.name );
                UnityEngine.Debug.Log( $"[HouseShot] the building menu outside lists: {string.Join( ", ", outsideCards )}" );
                outsideMenu.CloseMenu( false );
                await UniTask.Delay( 300 );

                var service = scope.Container.Resolve<HouseService>();
                var chunkManager = map.ChunkManager;
                var before = player.transform.position;
                service.Enter( door );
                await UniTask.Delay( 120 );
                UnityEngine.Debug.Log( $"[HouseShot] during the blink: inside {service.IsInside}" );
                await UniTask.WaitUntil( ( ) => service.IsInside );
                await UniTask.Delay( 1500 );
                SetHalfWidth( 2.2f );
                await UniTask.Delay( 500 );
                await Capture( Path.Combine( Folder, "2_inside.png" ) );
                UnityEngine.Debug.Log( $"[HouseShot] inside at {player.transform.position}, camera at {Camera.main.transform.position}, streaming paused {chunkManager.StreamingPaused}" );

                // The walk area: the spot in front of the door is free, the bed and the table are not, the walls hold.
                var area = player.WalkArea;
                var here = player.transform.position;
                UnityEngine.Debug.Log( $"[HouseShot] walk area: spawn free {area.CanMoveTo( here, here )}, "
                    + $"into the table {area.CanMoveTo( here, here + new Vector3( 0.45f, 0f, -0.9f ) )}, into the bed {area.CanMoveTo( here, here + new Vector3( 1.6f, 0f, -0.1f ) )}, "
                    + $"through the north wall {area.CanMoveTo( here, here + new Vector3( 0f, 0f, 0.6f ) )}, through the west wall {area.CanMoveTo( here, here + new Vector3( -1.2f, 0f, 0f ) )}" );
                var inside = Camera.main.WorldToScreenPoint( UnityEngine.Object.FindFirstObjectByType<HouseExit>().transform.position );
                bool exitHit = Physics.Raycast( Camera.main.ScreenPointToRay( inside ), out var exitRay, 100f, LayerMask.GetMask( "Clickable" ) );
                UnityEngine.Debug.Log( $"[HouseShot] a tap on the inner door hits {( exitHit ? exitRay.transform.name : "nothing" )}" );

                await CheckFurniture( scope, service, player );
                await CheckUpgrades( scope, service, player );

                service.Exit();
                await UniTask.WaitUntil( ( ) => !service.IsInside );
                await UniTask.Delay( 1500 );
                await Capture( Path.Combine( Folder, "3_outside_again.png" ) );
                UnityEngine.Debug.Log( $"[HouseShot] back outside at {player.transform.position} (was {before}), streaming paused {chunkManager.StreamingPaused}, walk area {( player.WalkArea == null ? "none" : "still set" )}" );
            }
            catch( Exception exception )
            {
                UnityEngine.Debug.LogError( "[HouseShot] " + exception );
            }
            finally
            {
                UnityEngine.Debug.Log( "[HouseShot] done" );
                EditorApplication.Exit( 0 );
            }
        }

        // Phase 5: furniture goes into the room, is refused where it does not fit, blocks the player, is picked up again, and is back
        // after the player has been out and in once more.
        private static async UniTask CheckFurniture( GameplayScope scope, HouseService service, PlayerController player )
        {
            var catalog = scope.Container.Resolve<Hearthglade.Gameplay.Database.ItemCatalog>();
            var inventory = scope.Container.Resolve<InventoryService>();
            BuildableItemSO Item( string name ) => ( BuildableItemSO ) catalog.GetAsset( new ItemId( name ) );
            var table = Item( "Table" );
            var stool = Item( "Stool" );
            var rug = Item( "Rug" );
            UnityEngine.Debug.Log( $"[HouseShot] items: table {table != null}, stool {stool != null}, rug {rug != null}" );
            var interior = service.Interior;
            UnityEngine.Debug.Log( $"[HouseShot] room cells {interior.CellMin} .. {interior.CellMax}, entry {interior.EntryCell}, {interior.FixedCells().Count()} cell(s) taken by the house itself" );

            await CheckBuildingUi( catalog, inventory, service );

            bool rugOk = service.TryPlace( rug, -1, -1, 0 );
            bool tableOk = service.TryPlace( table, -1, 0, 0 );
            bool stoolOk = service.TryPlace( stool, -1, -1, 0 );
            bool tableAgain = service.TryPlace( table, 0, 0, 0 );
            bool outside = service.TryPlace( stool, 9, 0, 0 );
            bool atDoor = service.TryPlace( stool, interior.EntryCell.x, interior.EntryCell.y, 0 );
            bool turned = service.TryPlace( table, 2, -1, 1 );
            UnityEngine.Debug.Log( $"[HouseShot] placed: rug {rugOk}, table {tableOk}, stool on the rug {stoolOk} (all true); refused: table on the table {!tableAgain}, outside the room {!outside}, in front of the door {!atDoor} (all true); a turned table {turned}" );

            var pieces = UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None );
            UnityEngine.Debug.Log( $"[HouseShot] {pieces.Length} piece(s) of furniture in the scene: {string.Join( ", ", pieces.Select( p => p.name ) )}" );

            var area = player.WalkArea;
            var tableCentre = interior.FloorCentre( -1, 0, 2, 1 );
            var rugCentre = interior.FloorCentre( -1, -1, 3, 2 );
            UnityEngine.Debug.Log( $"[HouseShot] walk area: into the table {area.CanMoveTo( player.transform.position, tableCentre )} (false), onto the free end of the rug {area.CanMoveTo( player.transform.position, rugCentre + new Vector3( 0.5f, 0f, 0.1f ) )} (true)" );
            SetHalfWidth( 2.2f );
            await UniTask.Delay( 700 );
            await Capture( Path.Combine( Folder, "2b_furnished.png" ) );

            // A tap on a piece does its interaction (nothing yet); holding opens the dropdown with Pick up and Destroy.
            int wood = inventory.Count( new ItemId( "Wood" ) );
            var piece = pieces.First( p => p.name == "Stool" );
            piece.InteractionStart();
            await UniTask.Delay( 200 );
            UnityEngine.Debug.Log( $"[HouseShot] a tap on the stool changes nothing: it is still there ({piece != null}), wood {inventory.Count( new ItemId( "Wood" ) )}" );

            // Holding a finger on the piece opens the bubble of the murmurs as a menu over it.
            var bubbles = scope.Container.Resolve<Hearthglade.Gameplay.UI.HUD.Messages.ContextBubbleService>();
            bubbles.Show( piece.TooltipTitle, piece.ContextActions, piece.transform );
            await UniTask.Delay( 500 );
            var bubble = UnityEngine.Object.FindObjectsByType<Hearthglade.Gameplay.UI.HUD.Messages.MurmurBubble>( FindObjectsInactive.Include, FindObjectsSortMode.None ).First( b => b.HasOptions );
            var buttons = bubble.GetComponentsInChildren<UnityEngine.UI.Button>();
            var bubbleCanvas = bubble.GetComponentInParent<Canvas>();
            var uiCamera = Hearthglade.Gameplay.UI.HUD.Messages.MurmurBubble.UiCamera( bubbleCanvas );
            var pickUpButton = buttons.First( b => b.name == "Pick up" );
            var onButton = RectTransformUtility.WorldToScreenPoint( uiCamera, pickUpButton.transform.position );
            UnityEngine.Debug.Log( $"[HouseShot] the bubble menu is shown: {bubbles.IsShown}, lines: {string.Join( ", ", buttons.Select( b => b.name ) )}, canvas {bubbleCanvas.name} ({bubbleCanvas.renderMode}), layer of a line {LayerMask.LayerToName( pickUpButton.gameObject.layer )}"
                + $", a finger on a line counts as over the GUI: {Lean.Touch.LeanTouch.PointOverGui( onButton )}, away from it: {Lean.Touch.LeanTouch.PointOverGui( new Vector2( 5f, 5f ) )}" );
            var mode = bubbleCanvas.renderMode;
            if( mode != RenderMode.ScreenSpaceOverlay )
            {
                mode = RenderMode.ScreenSpaceOverlay;
            }
            var canvas = bubbleCanvas.rootCanvas;
            var savedMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            await UniTask.Delay( 300 );
            await Capture( Path.Combine( Folder, "2b_bubble_menu.png" ) );
            canvas.renderMode = savedMode;
            await UniTask.Delay( 200 );

            // A finger put down somewhere else closes the menu; one put down on a line does not.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var fingerDown = typeof( Hearthglade.Gameplay.UI.HUD.Messages.MurmurBubble ).GetMethod( "HandleFingerDown", flags );
            fingerDown.Invoke( bubble, new object[] { new Lean.Touch.LeanFinger { ScreenPosition = onButton } } );
            UnityEngine.Debug.Log( $"[HouseShot] a finger on a line leaves the menu open: {bubbles.IsShown}" );
            fingerDown.Invoke( bubble, new object[] { new Lean.Touch.LeanFinger { ScreenPosition = new Vector2( 5f, 5f ) } } );
            await UniTask.Delay( 400 );
            UnityEngine.Debug.Log( $"[HouseShot] a finger elsewhere closes it: {!bubbles.IsShown}" );
            bubbles.Show( piece.TooltipTitle, piece.ContextActions, piece.transform );
            await UniTask.Delay( 400 );
            bubble.GetComponentsInChildren<UnityEngine.UI.Button>().First( b => b.name == "Pick up" ).onClick.Invoke();
            await UniTask.Delay( 300 );
            UnityEngine.Debug.Log( $"[HouseShot] Pick up from the menu: wood {wood} -> {inventory.Count( new ItemId( "Wood" ) )}, the menu is closed: {!bubbles.IsShown}, {UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None ).Count( p => p.gameObject.activeInHierarchy && p != piece )} piece(s) left"
                + $", saved {JsonConvert.SerializeObject( ( ( ISaveable ) service ).CaptureState() )}" );

            // The piece is in the backpack as an item and can be put down again somewhere else, without paying for it twice.
            var stoolId = new ItemId( "Stool" );
            var stoolSlot = inventory.Container.FirstOrDefault( sl => !sl.IsEmpty && sl.Stack.Definition.Id == stoolId );
            UnityEngine.Debug.Log( $"[HouseShot] after Pick up the backpack holds {inventory.Count( stoolId )} stool(s), the slot offers {( stoolSlot != null ? Hearthglade.Core.Items.SlotActions.Available( stoolSlot ).ToString() : "no slot" )}" );
            inventory.PlaceItem( stoolSlot );
            await UniTask.Delay( 600 );
            var buildMenu = UnityEngine.Object.FindFirstObjectByType<BuildingMenu>( FindObjectsInactive.Include );
            var backpackPlacer = buildMenu.GetComponent<BuildingPlacer>();
            var gameState = scope.Container.Resolve<Hearthglade.Gameplay.Common.Service.GameManager>().CurrentState.StateName;
            var placerFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof( BuildingPlacer ).GetMethod( "HandleTouch", placerFlags ).Invoke( backpackPlacer, new object[] { new LeanFinger { ScreenPosition = Camera.main.WorldToScreenPoint( service.Interior.FloorCentre( 0, -2, 1, 1 ) ) } } );
            var ghostItem = ( Transform ) typeof( BuildingPlacer ).GetField( "currentlyAttachedItem", placerFlags ).GetValue( backpackPlacer );
            var pendingCellValue = typeof( BuildingPlacer ).GetField( "pendingCell", placerFlags ).GetValue( backpackPlacer );
            var indoorsFlag = typeof( BuildingPlacer ).GetField( "placingIndoors", placerFlags ).GetValue( backpackPlacer );
            UnityEngine.Debug.Log( $"[HouseShot] placer after the touch: ghost {( ghostItem != null ? ghostItem.name + " at " + ghostItem.position : "none" )}, pending cell {pendingCellValue}, indoors {indoorsFlag}, can place at (0, -2): {service.CanPlace( ( BuildableItemSO ) catalog.GetAsset( stoolId ), 0, -2, 0 )}" );
            bool stoolFits = ( bool ) typeof( BuildingPlacer ).GetField( "canBuildInSelectedPlace", placerFlags ).GetValue( backpackPlacer );
            typeof( BuildingPlacer ).GetMethod( "PlaceBuilding", placerFlags ).Invoke( backpackPlacer, null );
            await UniTask.Delay( 400 );
            UnityEngine.Debug.Log( $"[HouseShot] Place from the backpack: game state {gameState}, fits {stoolFits}, stools in the backpack now {inventory.Count( stoolId )}, stool in the room again {UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None ).Any( p => p.name == "Stool" )}" );

            var rugPiece = UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None ).First( p => p.name == "Rug" );
            int cotton = inventory.Count( new ItemId( "Cotton" ) );
            bubbles.Show( rugPiece.TooltipTitle, rugPiece.ContextActions, rugPiece.transform );
            await UniTask.Delay( 400 );
            bubble.GetComponentsInChildren<UnityEngine.UI.Button>().First( b => b.name == "Destroy" ).onClick.Invoke();
            await UniTask.Delay( 300 );
            UnityEngine.Debug.Log( $"[HouseShot] Destroy from the dropdown: the rug is gone ({UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None ).All( p => p.name != "Rug" )}), cotton {cotton} -> {inventory.Count( new ItemId( "Cotton" ) )} (no refund)" );

            // Out and in again: the furniture comes back from the saved data.
            service.Exit();
            await UniTask.WaitUntil( ( ) => !service.IsInside );
            await UniTask.Delay( 800 );
            var door = UnityEngine.Object.FindFirstObjectByType<HouseDoor>();
            service.Enter( door );
            await UniTask.WaitUntil( ( ) => service.IsInside );
            await UniTask.Delay( 1500 );
            var again = UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None );
            UnityEngine.Debug.Log( $"[HouseShot] back in: {again.Length} piece(s): {string.Join( ", ", again.Select( p => p.name ) )}" );
            await Capture( Path.Combine( Folder, "2c_reentered.png" ) );
        }

        // The way a player builds furniture: the building menu lists it only inside the house, and the placer snaps a ghost to the floor under
        // a finger, checks it against the room and puts it down for the price of the recipe.
        private static async UniTask CheckBuildingUi( Hearthglade.Gameplay.Database.ItemCatalog catalog, InventoryService inventory, HouseService service )
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<BuildingMenu>( FindObjectsInactive.Include );
            var placer = menu.GetComponent<BuildingPlacer>();
            menu.OpenMenu();
            await UniTask.Delay( 400 );
            var listed = UnityEngine.Object.FindObjectsByType<CraftingCard>( FindObjectsInactive.Exclude, FindObjectsSortMode.None ).Select( card => card.Item.name );
            UnityEngine.Debug.Log( $"[HouseShot] the building menu inside the house lists: {string.Join( ", ", listed )}" );
            menu.CloseMenu( false );

            inventory.AddItem( catalog.GetAsset( new ItemId( "Wood" ) ), 6 );
            int woodBefore = inventory.Count( new ItemId( "Wood" ) );
            var recipe = Hearthglade.Gameplay.Database.RecipiesDatabase.instance.buildingsDatabase.First( r => r.craftedItemName == "Table" );
            placer.SetupBuildingMode( recipe );
            var interior = service.Interior;
            var finger = new LeanFinger { ScreenPosition = Camera.main.WorldToScreenPoint( interior.FloorCentre( 2, 1, 1, 1 ) ) };
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof( BuildingPlacer ).GetMethod( "HandleTouch", flags ).Invoke( placer, new object[] { finger } );
            bool canBuild = ( bool ) typeof( BuildingPlacer ).GetField( "canBuildInSelectedPlace", flags ).GetValue( placer );
            var ghost = ( Transform ) typeof( BuildingPlacer ).GetField( "currentlyAttachedItem", flags ).GetValue( placer );
            UnityEngine.Debug.Log( $"[HouseShot] the ghost of the table is at {ghost.position} (floor over cell (2, 1) is {interior.FloorCentre( 2, 1, 2, 1 )}), may be built: {canBuild}" );
            typeof( BuildingPlacer ).GetMethod( "PlaceBuilding", flags ).Invoke( placer, null );
            await UniTask.Delay( 300 );
            UnityEngine.Debug.Log( $"[HouseShot] built the table with the placer: wood {woodBefore} -> {inventory.Count( new ItemId( "Wood" ) )}, {UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None ).Length} piece(s) in the room" );
        }

        // Phase 6: the house grows twice from the building menu, the furniture stays where it was, the room is bigger, and the house outside
        // changes its look.
        private static async UniTask CheckUpgrades( GameplayScope scope, HouseService service, PlayerController player )
        {
            var catalog = scope.Container.Resolve<Hearthglade.Gameplay.Database.ItemCatalog>();
            var inventory = scope.Container.Resolve<InventoryService>();
            var menu = UnityEngine.Object.FindFirstObjectByType<BuildingMenu>( FindObjectsInactive.Include );
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach( var level in new[] { 2, 3 } )
            {
                var recipe = Hearthglade.Gameplay.Database.RecipiesDatabase.instance.buildingsDatabase.First( r => r.craftedItemName == $"HouseLevel{level}" );
                // Short of one item: the card is there but nothing happens.
                var first = recipe.requirements.First();
                foreach( var requirement in recipe.requirements.Skip( 1 ) )
                {
                    inventory.AddItem( catalog.GetAsset( requirement.Item ), requirement.requiredQuantity );
                }
                menu.OpenMenu();
                await UniTask.Delay( 400 );
                var cards = UnityEngine.Object.FindObjectsByType<CraftingCard>( FindObjectsInactive.Exclude, FindObjectsSortMode.None );
                UnityEngine.Debug.Log( $"[HouseShot] level {service.Level}: the building menu lists {string.Join( ", ", cards.Select( card => card.Item.name ) )}" );
                var upgradeCard = cards.First( card => card.Item.name == $"HouseLevel{level}" );
                typeof( BuildingMenu ).GetMethod( "OnCardAction", flags ).Invoke( menu, new object[] { upgradeCard } );
                await UniTask.Delay( 300 );
                UnityEngine.Debug.Log( $"[HouseShot] without {first.requiredItemName} the house stays at level {service.Level}" );
                inventory.AddItem( catalog.GetAsset( first.Item ), first.requiredQuantity );
                menu.CloseMenu( false );
                await UniTask.Delay( 300 );
                menu.OpenMenu();
                await UniTask.Delay( 400 );
                upgradeCard = UnityEngine.Object.FindObjectsByType<CraftingCard>( FindObjectsInactive.Exclude, FindObjectsSortMode.None ).First( card => card.Item.name == $"HouseLevel{level}" );
                typeof( BuildingMenu ).GetMethod( "OnCardAction", flags ).Invoke( menu, new object[] { upgradeCard } );
                await UniTask.WaitUntil( ( ) => service.Level == level );
                await UniTask.Delay( 1500 );
                var interior = service.Interior;
                var furniture = UnityEngine.Object.FindObjectsByType<HouseFurniture>( FindObjectsSortMode.None );
                UnityEngine.Debug.Log( $"[HouseShot] level {service.Level}: room cells {interior.CellMin} .. {interior.CellMax}, entry {interior.EntryCell}, {furniture.Length} piece(s) of furniture back, player at {player.transform.position}, wood left {inventory.Count( new ItemId( "Wood" ) )}" );
                SetHalfWidth( 2.2f + level );
                await UniTask.Delay( 700 );
                await Capture( Path.Combine( Folder, $"2d_level{level}.png" ) );
            }
        }

        private static void Teleport( PlayerController player, Vector3 position )
        {
            player.TeleportTo( position, player.transform.rotation );
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
            UnityEngine.Debug.Log( "[HouseShot] saved " + path );
        }
    }
}
#endif
