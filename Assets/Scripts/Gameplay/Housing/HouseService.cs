using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Housing
{
    [ Serializable ]
    public class HouseFurnitureData
    {
        public string Item;
        public int X;
        public int Z;
        public int Turns;
    }

    [ Serializable ]
    public class HouseSaveData
    {
        public int Level = 1;
        public List<HouseFurnitureData> Furniture = new List<HouseFurnitureData>();
    }

    /// <summary>
    /// Takes the player into a house and back out (docs/HOME_ISLAND_PLAN.md), and keeps the furniture the player put in it. The inside is a
    /// prefab spawned high above the island; going in is a blink: the screen fades to black, the player is moved, the screen fades back.
    /// The island stays exactly as it was (chunk streaming is paused, so it does not follow the player up there).
    /// A registered entry point so it exists from the start of the game: a saveable that nothing resolved would be missing from the save.
    /// </summary>
    public class HouseService : IStartable, ISaveable
    {
        // Far above the island: clear of the water, the ground colliders and every chunk. Half a cell off the build grid, so that the
        // walls of the room fall on the edges of cells (see HouseInterior).
        private static readonly Vector3 InteriorOrigin = new Vector3( MapGenerator.TILE_X_OFFSET * 0.5f, 300f, MapGenerator.TILE_Z_OFFSET * 0.5f );

        // Frames to wait behind the black so the camera has followed the player before the screen clears.
        private const int SettleFrames = 3;

        private readonly PlayerController player;
        private readonly MapManager mapManager;
        private readonly ScreenFade screenFade;
        private readonly SaveManager saveManager;
        private readonly ItemCatalog catalog;
        private readonly InventoryService inventory;
        private readonly MessagePopup messagePopup;

        // The furniture of the house, as saved. While the player is inside, `layout` is the truth and this is a copy of it.
        private List<HouseFurnitureData> furniture = new List<HouseFurnitureData>();
        private readonly Dictionary<int, GameObject> spawned = new Dictionary<int, GameObject>();
        private InteriorLayout layout;

        private HouseInterior current;
        private HouseProp currentHouse;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private bool busy;

        /// <summary>How far the house has grown (1 at the start).</summary>
        public int Level { get; private set; } = 1;

        public const int MaxLevel = 3;

        /// <summary>Raised with the new level when the house has grown.</summary>
        public event Action<int> LevelChanged;

        public bool IsInside => current != null;

        public HouseInterior Interior => current;

        [ Inject ]
        public HouseService( PlayerController player, MapManager mapManager, ScreenFade screenFade, SaveManager saveManager, ItemCatalog catalog, InventoryService inventory, MessagePopup messagePopup )
        {
            this.player = player;
            this.mapManager = mapManager;
            this.screenFade = screenFade;
            this.saveManager = saveManager;
            this.catalog = catalog;
            this.inventory = inventory;
            this.messagePopup = messagePopup;
            saveManager.RegisterISavable( this );
            if ( saveManager.TryGetState<HouseService>( out var saved ) )
            {
                RestoreState( saved );
            }
        }

        public void Start( )
        {
        }

        // ---- going in and out ----

        public void Enter( HouseDoor door )
        {
            if ( busy || IsInside || door == null || door.InteriorPrefab == null )
            {
                return;
            }
            BlinkAsync( ( ) => GoInAsync( door ) ).Forget();
        }

        public void Exit( )
        {
            if ( busy || !IsInside )
            {
                return;
            }
            BlinkAsync( GoOutAsync ).Forget();
        }

        // One blink at a time: a second tap during the fade must not start another trip.
        private async UniTaskVoid BlinkAsync( Func<UniTask> work )
        {
            busy = true;
            try
            {
                await screenFade.LoadAsync( work );
            }
            finally
            {
                busy = false;
            }
        }

        private async UniTask GoInAsync( HouseDoor door )
        {
            returnPosition = player.transform.position;
            returnRotation = player.transform.rotation;
            player.StopMoving();

            currentHouse = door.GetComponentInParent<HouseProp>();
            mapManager.CurrentMap.ChunkManager.StreamingPaused = true;
            current = UnityEngine.Object.Instantiate( door.InteriorPrefab, InteriorOrigin, Quaternion.identity );
            current.ExitRequested = Exit;
            Furnish();

            player.EnterInterior( current );
            var spawn = current.SpawnPoint;
            player.TeleportTo( spawn.position, spawn.rotation );
            await UniTask.DelayFrame( SettleFrames );
        }

        private async UniTask GoOutAsync( )
        {
            player.StopMoving();
            player.ExitInterior();
            player.TeleportTo( returnPosition, returnRotation );

            UnityEngine.Object.Destroy( current.gameObject );
            current = null;
            currentHouse = null;
            layout = null;
            spawned.Clear();
            mapManager.CurrentMap.ChunkManager.StreamingPaused = false;
            await UniTask.DelayFrame( SettleFrames );
        }

        // ---- levels ----

        /// <summary>
        /// Grows the house by one level for the price of the recipe, from inside it: the walls move out behind a blink and the room is
        /// furnished again (the furniture keeps its cells, because the north-east corner of the room stays where it was).
        /// </summary>
        public bool TryUpgrade( HouseUpgradeItemSO upgrade, IEnumerable<Requirement> price )
        {
            if ( busy || !IsInside || currentHouse == null || upgrade == null || upgrade.level != Level + 1 || upgrade.level > MaxLevel )
            {
                return false;
            }
            var bigger = currentHouse.InteriorOf( upgrade.level );
            if ( bigger == null || !inventory.RemoveItems( price ) )
            {
                return false;
            }
            BlinkAsync( ( ) => GrowAsync( upgrade.level, bigger ) ).Forget();
            return true;
        }

        private async UniTask GrowAsync( int level, HouseInterior bigger )
        {
            player.StopMoving();
            UnityEngine.Object.Destroy( current.gameObject );
            spawned.Clear();
            Level = level;
            current = UnityEngine.Object.Instantiate( bigger, InteriorOrigin, Quaternion.identity );
            current.ExitRequested = Exit;
            Furnish();
            LevelChanged?.Invoke( Level );

            player.EnterInterior( current );
            var spawn = current.SpawnPoint;
            player.TeleportTo( spawn.position, spawn.rotation );
            await UniTask.DelayFrame( SettleFrames );
        }

        // ---- furniture ----

        // Builds the layout of the room that was just spawned and puts the saved furniture back in it.
        private void Furnish( )
        {
            layout = new InteriorLayout( current.CellMin.x, current.CellMin.y, current.CellMax.x, current.CellMax.y, current.FixedCells(), ( current.EntryCell.x, current.EntryCell.y ) );
            spawned.Clear();
            foreach ( var piece in furniture )
            {
                if ( !( catalog.GetAsset( new ItemId( piece.Item ) ) is BuildableItemSO item ) || item.buildingPrefab == null )
                {
                    UnityEngine.Debug.LogWarning( $"[House] the furniture {piece.Item} is not known any more and is left out" );
                    continue;
                }
                int id = layout.Place( piece.Item, piece.X, piece.Z, item.sizeInCells.x, item.sizeInCells.z, piece.Turns, item.lyingFlat );
                if ( id == 0 )
                {
                    UnityEngine.Debug.LogWarning( $"[House] the furniture {piece.Item} at ({piece.X}, {piece.Z}) does not fit the room and is left out" );
                    continue;
                }
                Spawn( item, FindPiece( id ) );
            }
            RefreshWalkArea();
        }

        /// <summary>Whether a piece of this item could stand with its lowest corner (after turning) on this cell.</summary>
        public bool CanPlace( BuildableItemSO item, int x, int z, int turns )
        {
            return layout != null && item != null && layout.CanPlace( x, z, item.sizeInCells.x, item.sizeInCells.z, turns, item.lyingFlat );
        }

        /// <summary>Puts a piece of furniture down (the caller has taken the item from the backpack). False when it does not fit.</summary>
        public bool TryPlace( BuildableItemSO item, int x, int z, int turns )
        {
            if ( layout == null || item == null || item.buildingPrefab == null )
            {
                return false;
            }
            int id = layout.Place( item.name, x, z, item.sizeInCells.x, item.sizeInCells.z, turns, item.lyingFlat );
            if ( id == 0 )
            {
                return false;
            }
            Spawn( item, FindPiece( id ) );
            RefreshWalkArea();
            CaptureFurniture();
            return true;
        }

        /// <summary>
        /// Takes a piece of furniture into the backpack, as the item it is, so that it can be put down somewhere else. It stays where it is
        /// when the backpack has no room.
        /// </summary>
        public void PickUp( HouseFurniture furniture )
        {
            if ( layout == null || furniture == null )
            {
                return;
            }
            var piece = FindPiece( furniture.PieceId );
            var item = catalog.GetAsset( new ItemId( piece.Item ) );
            if ( item == null )
            {
                return;
            }
            if ( !inventory.CanFit( item ) )
            {
                inventory.ReportNoRoom( item, 1 );
                return;
            }
            inventory.AddItem( item );
            Remove( furniture );
        }

        /// <summary>Throws a piece of furniture away: nothing comes back.</summary>
        public void Destroy( HouseFurniture furniture )
        {
            if ( layout == null || furniture == null )
            {
                return;
            }
            Remove( furniture );
        }

        private void Remove( HouseFurniture furniture )
        {
            layout.Remove( furniture.PieceId );
            spawned.Remove( furniture.PieceId );
            UnityEngine.Object.Destroy( furniture.gameObject );
            RefreshWalkArea();
            CaptureFurniture();
        }

        private PlacedPiece FindPiece( int id )
        {
            foreach ( var piece in layout.Pieces )
            {
                if ( piece.Id == id )
                {
                    return piece;
                }
            }
            throw new InvalidOperationException( $"no piece {id} in the layout" );
        }

        private void Spawn( BuildableItemSO item, PlacedPiece piece )
        {
            var position = current.FloorCentre( piece.X, piece.Z, piece.SizeX, piece.SizeZ );
            var instance = UnityEngine.Object.Instantiate( item.buildingPrefab, position, Quaternion.Euler( 0f, piece.QuarterTurns * 90f, 0f ), current.transform );
            instance.name = item.name;
            if ( !instance.TryGetComponent<HouseFurniture>( out var handle ) )
            {
                handle = instance.AddComponent<HouseFurniture>();
            }
            float reach = Mathf.Max( piece.SizeX, piece.SizeZ ) * MapGenerator.TILE_X_OFFSET * 0.5f + 0.25f;
            handle.Init( this, piece.Id, item.itemName, reach );
            spawned[ piece.Id ] = instance;
        }

        // The solid furniture is an obstacle for the player; a rug is not.
        private void RefreshWalkArea( )
        {
            var rects = new List<WalkRect>();
            foreach ( var piece in layout.Pieces )
            {
                if ( !piece.Flat )
                {
                    rects.Add( HouseInterior.CellRect( piece.X, piece.Z, piece.SizeX, piece.SizeZ ) );
                }
            }
            current.SetFurniture( rects );
        }

        private void CaptureFurniture( )
        {
            furniture = new List<HouseFurnitureData>();
            foreach ( var piece in layout.Pieces )
            {
                furniture.Add( new HouseFurnitureData { Item = piece.Item, X = piece.X, Z = piece.Z, Turns = piece.QuarterTurns } );
            }
        }

        // ---- saving ----

        public object CaptureState( )
        {
            return new HouseSaveData { Level = Level, Furniture = new List<HouseFurnitureData>( furniture ) };
        }

        public void RestoreState( object state )
        {
            var data = state is JToken token
                ? token.ToObject<HouseSaveData>()
                : JsonConvert.DeserializeObject<HouseSaveData>( state.ToString() );
            Level = Mathf.Clamp( data?.Level ?? 1, 1, MaxLevel );
            furniture = data?.Furniture ?? new List<HouseFurnitureData>();
        }
    }
}
