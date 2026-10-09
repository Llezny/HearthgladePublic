using System;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>
    /// The body of a house on the island. It is a scene object so that a point of interest can place it by prefab name (see PoiBuilder).
    /// It holds one look per level of the house and shows the one the player has reached (<see cref="HouseService.Level"/>); each look has
    /// its own door (<see cref="HouseDoor"/>) that leads into the room of that level.
    /// </summary>
    public class HouseProp : SceneObject
    {
        [ Serializable ]
        public class LevelLook
        {
            [ Tooltip( "The model, the solid box and the door of this level; only the current level is active." ) ]
            public GameObject root;
            public HouseDoor door;
        }

        [ Tooltip( "Index 0 is level 1." ) ]
        [ SerializeField ] private LevelLook[] levels = new LevelLook[0];

        [ Header( "Grass" ) ]
        [ Tooltip( "The ground the house covers (with the porch and the path), in metres from the pivot: the island keeps its grass out of it." ) ]
        [ SerializeField ] private Vector2 grassCentre;
        [ SerializeField ] private Vector2 grassHalfSize = Vector2.zero;
        [ Tooltip( "How far past that area the grass is kept away, in cells." ) ]
        [ SerializeField ] private float grassRadius = 0f;

        private HouseService houseService;
        private MapManager mapManager;

        [ Inject ]
        public void Construct( HouseService houseService, MapManager mapManager )
        {
            this.houseService = houseService;
            this.mapManager = mapManager;
            houseService.LevelChanged += OnLevelChanged;
            Show( houseService.Level );
        }

        // The place in the map is known now. Neither the grass zone nor the blocked cells are saved with the game, so they are made again each time
        // the chunk shows the house.
        protected override void OnSceneObjectModelAssigned( )
        {
            if ( mapManager == null )
            {
                return;
            }
            ClearGrass();
            BlockGround( houseService != null ? houseService.Level : 1 );
        }

        private void OnLevelChanged( int level )
        {
            Show( level );
            if ( mapManager != null )
            {
                BlockGround( level );
            }
        }

        // The player is stopped by the terrain data, not by colliders (see PlayerController.Walk): the cells under the solid box of the level
        // (its first BoxCollider) are taken away, a cell whose middle is within a hand's breadth of the box. A cell stays blocked when the house
        // grows, which is right because every level covers more or less the same ground.
        private void BlockGround( int level )
        {
            if ( levels.Length == 0 || levels[ Mathf.Clamp( level, 1, levels.Length ) - 1 ].root == null )
            {
                return;
            }
            var look = levels[ Mathf.Clamp( level, 1, levels.Length ) - 1 ].root.transform;
            var body = look.GetComponent<BoxCollider>();
            if ( body == null )
            {
                return;
            }
            var map = mapManager.CurrentMap;
            var middle = MapHelper.WorldPositionToBlockIndex( transform.position );
            const int reach = 5;
            for ( int x = middle.x - reach; x <= middle.x + reach; x++ )
            {
                for ( int z = middle.y - reach; z <= middle.y + reach; z++ )
                {
                    var local = look.InverseTransformPoint( new Vector3( x * MapGenerator.TILE_X_OFFSET, transform.position.y, z * MapGenerator.TILE_Z_OFFSET ) ) - body.center;
                    if ( Mathf.Abs( local.x ) <= body.size.x * 0.5f + BlockMargin && Mathf.Abs( local.z ) <= body.size.z * 0.5f + BlockMargin )
                    {
                        map.BlockWalkingAt( x, z );
                    }
                }
            }
        }

        // How far past the box the middle of a cell may be for the cell to count as covered, in metres.
        private const float BlockMargin = 0.1f;

        // The grass zone id is the place of the house, so it is made once.
        private void ClearGrass( )
        {
            if ( grassHalfSize == Vector2.zero )
            {
                return;
            }
            var centre = transform.TransformPoint( new Vector3( grassCentre.x, 0f, grassCentre.y ) );
            bool turned = Mathf.Abs( Mathf.Sin( transform.eulerAngles.y * Mathf.Deg2Rad ) ) > 0.7f;
            var half = turned ? new Vector2( grassHalfSize.y, grassHalfSize.x ) : grassHalfSize;
            float cellX = MapGenerator.TILE_X_OFFSET, cellZ = MapGenerator.TILE_Z_OFFSET;
            int id = HashCode.Combine( "HomeHouseGrass", Mathf.RoundToInt( centre.x / cellX ), Mathf.RoundToInt( centre.z / cellZ ) );
            mapManager.CurrentMap.ClearGrassUnderArea( id, centre.x / cellX, centre.z / cellZ, half.x / cellX, half.y / cellZ, grassRadius );
        }

        private void OnDestroy( )
        {
            if ( houseService != null )
            {
                houseService.LevelChanged -= OnLevelChanged;
            }
        }

        public override bool CanInteract( )
        {
            return false;
        }

        /// <summary>The room of a level, or null when this house has no such level.</summary>
        public HouseInterior InteriorOf( int level )
        {
            return level >= 1 && level <= levels.Length && levels[ level - 1 ].door != null ? levels[ level - 1 ].door.InteriorPrefab : null;
        }

        private void Show( int level )
        {
            for ( int i = 0; i < levels.Length; i++ )
            {
                if ( levels[ i ].root != null )
                {
                    levels[ i ].root.SetActive( i + 1 == Mathf.Clamp( level, 1, levels.Length ) );
                }
            }
        }
    }
}
