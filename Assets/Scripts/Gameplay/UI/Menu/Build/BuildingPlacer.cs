using System.Collections;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Lean.Touch;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Build
{
    public class BuildingPlacer : MonoBehaviour {

        // How many degrees one tap of a rotate button turns the ghost (docs/BUILDING_SYSTEM_PLAN.md section 5:
        // 8 discrete directions instead of the old continuous free rotation).
        private const float ROTATION_STEP_DEGREES = 45f;
        private const float ROTATION_TWEEN_SECONDS = 0.15f;

        [ SerializeField ] Transform currentlyAttachedItem = null;
        [ SerializeField ] Recipe currentRecipe = null;
        [ SerializeField ] GameObject buildButton = null;
        private RaycastHit hit = new RaycastHit();
        private bool canBuildInSelectedPlace = false;

        private BuildableItemSO currentItem = null;
        private int rotationStep = 0;
        private Coroutine rotationTween = null;
        private SerializableVector2Int pendingCell;
        private int pendingY;
        private int pendingEdgeX;
        private int pendingEdgeZ;
        private EdgeSide pendingEdgeSide;
        private BuildGridVisualizer gridVisualizer;

        // Walls/doors are edge-anchored (BuildEdgeGrid): they snap to the boundary between two cells and face
        // whichever way that edge implies, so unlike cell-anchored pieces they have no manual rotation
        // (docs/BUILDING_SYSTEM_PLAN.md section 6, revised after the wall-connection issue).
        private bool IsEdgePiece => currentItem != null && currentItem.category.IsEdgePiece();

        // A footprint bigger than one cell is anchored at its lowest corner cell, so turning the piece around that
        // cell would carry it off the cells that were checked: only single-cell pieces rotate.
        private bool CanRotate => currentItem != null && !IsEdgePiece && currentItem.sizeInCells.x == 1 && currentItem.sizeInCells.z == 1;

        // A floor tile is cell-anchored like furniture (keeps its manual rotation), but never stacks and
        // never looks at BuildOccupancyGrid at all - it only tracks "is there already a tile here"
        // (docs/BUILDING_SYSTEM_PLAN.md section 9), and rests at the terrain's real (possibly grass/sand
        // bumped) height instead of BuildGrid.SupportY, since levelling that bump is the point of placing one.
        private bool IsFloorPiece => currentItem != null && currentItem.category == BuildCategory.Floor;

        [ Header("References") ]
        private GameManager gameManager = null;
        private CameraService cameraService = null;
        private Camera mainCamera;
        [ SerializeField ] Button rotateLeftButton = null;
        [ SerializeField ] Button rotateRightButton = null;

        // Dependencies
        private GameObjectFactory gameObjectFactory;
        private Inventory.InventoryService inventoryService;
        private MapManager mapManager = null;
        private ItemCatalog catalog;
        //

        [ Inject ]
        public void Construct( GameObjectFactory gameObjectFactory, Inventory.InventoryService inventoryService, MapManager mapManager, CameraService cameraService, ItemCatalog catalog, GameManager gameManager ) {
            this.catalog = catalog;
            this.gameObjectFactory = gameObjectFactory;
            this.inventoryService = inventoryService;
            this.mapManager = mapManager;
            this.cameraService = cameraService;
            this.gameManager = gameManager;
        }

        void Awake(){
            buildButton.GetComponent<Button>().onClick.AddListener( PlaceBuilding );
            mainCamera = Camera.main;
            gridVisualizer = BuildGridVisualizer.Create();
        }

        public void SetupBuildingMode( Recipe recipe ) {

            //TODO add check if item is not buildable or smth dont crash
            var targetItem =  catalog.GetAsset( recipe.CraftedItem ) as BuildableItemSO;
            if ( targetItem is null || targetItem.buildingPrefab is null ) {
                UnityEngine.Debug.LogError( "Target item does not contain building prefab!" );
                return;
            }

            // Picking another item while one is still attached swaps the ghost.
            if( currentlyAttachedItem != null ) {
                Lean.Pool.LeanPool.Despawn( currentlyAttachedItem.gameObject );
            }
            buildButton.SetActive( false );
            canBuildInSelectedPlace = false;
            UnsubscribeTouch();

            currentRecipe = recipe;
            currentItem = targetItem;

            if( rotationTween != null ) {
                StopCoroutine( rotationTween );
                rotationTween = null;
            }
            rotationStep = 0;

            currentlyAttachedItem = gameObjectFactory.Get( targetItem.buildingPrefab ).transform;
            currentlyAttachedItem.rotation = Quaternion.identity;

            bool showRotateButtons = CanRotate;
            rotateLeftButton.gameObject.SetActive( showRotateButtons );
            rotateRightButton.gameObject.SetActive( showRotateButtons );
            gridVisualizer.Show();

            LeanTouch.OnFingerUpdate += HandleTouch;
            LeanTouch.OnFingerDown += HideBuildButton;
            LeanTouch.OnFingerUp += ShowBuildButton;
        }

        private void UnsubscribeTouch() {
            LeanTouch.OnFingerUpdate -= HandleTouch;
            LeanTouch.OnFingerDown -= HideBuildButton;
            LeanTouch.OnFingerUp -= ShowBuildButton;
        }

        void Update() {
            if( currentlyAttachedItem != null && Input.GetKey( KeyCode.Escape ) ){
                ExitBuildingPlacer();
                return;
            }
        }

        private void HandleTouch( LeanFinger finger ) {
            if( finger.StartedOverGui || currentlyAttachedItem == null ){
                return;
            }
            var ray = mainCamera.ScreenPointToRay(finger.ScreenPosition);
            if( Physics.Raycast( ray, out hit, 100, LayerMask.GetMask( UnityTags.Block ) ) ) {
                canBuildInSelectedPlace = CheckIfCanBuildInSelectedPlace();
            }
        }

        private void ShowBuildButton( LeanFinger finger ) {
            if( !finger.StartedOverGui && canBuildInSelectedPlace ){
                buildButton.SetActive( true );
                buildButton.transform.position = (finger.ScreenPosition) + new Vector2( 0, 100 );
            }
        }

        private void HideBuildButton( LeanFinger finger ) {
            if( !finger.StartedOverGui ){
                buildButton.SetActive( false );
            }
        }

        private bool CheckIfCanBuildInSelectedPlace() {
            if( IsEdgePiece ) {
                return CheckIfCanBuildOnEdge();
            }
            return IsFloorPiece ? CheckIfCanBuildOnFloorCell() : CheckIfCanBuildOnCell();
        }

        // Snaps the ghost to the build grid cell under the finger (docs/BUILDING_SYSTEM_PLAN.md section 5),
        // rests it on whatever already occupies that column (section "stack w pionie" - Phase 4) and checks
        // its full footprint against the map's BuildOccupancyGrid instead of a physics overlap.
        private bool CheckIfCanBuildOnCell() {
            pendingCell = MapHelper.WorldPositionToBlockIndex( hit.point );
            var snapped = MapHelper.GridToWorldPosition( new Vector2( pendingCell.x, pendingCell.y ) );

            var size = currentItem.sizeInCells;
            var buildGrid = mapManager.CurrentMap.BuildGrid;
            var support = buildGrid.SupportY( pendingCell.x, pendingCell.y, size.x, size.z );
            if( support == null ) {
                // Half on one thing, half on another (or on bare ground): nowhere flat enough to rest on.
                currentlyAttachedItem.position = new Vector3( snapped.x, hit.point.y, snapped.z );
                gridVisualizer.UpdateGrid( new Vector2Int( pendingCell.x, pendingCell.y ), 0,
                    new Vector2Int( pendingCell.x, pendingCell.y ), new Vector2Int( size.x, size.z ), false );
                return false;
            }
            pendingY = support.Value;
            // Lifted over the grass/sand bump (or a floor tile) so the piece does not sink into the ground.
            float rise = mapManager.CurrentMap.BuildBaseRise( pendingCell.x, pendingCell.y, size.x, size.z );
            currentlyAttachedItem.position = new Vector3( snapped.x, pendingY * MapGenerator.TILE_X_OFFSET + rise, snapped.z );


            var box = new BuildCellBox( pendingCell.x, pendingY, pendingCell.y, size.x, size.y, size.z );
            var fits = buildGrid.CanPlace( box );
            gridVisualizer.UpdateGrid( new Vector2Int( pendingCell.x, pendingCell.y ), pendingY,
                new Vector2Int( pendingCell.x, pendingCell.y ), new Vector2Int( size.x, size.z ), fits );

            return fits;
        }

        // Floor tiles are always a single cell (docs/BUILDING_SYSTEM_PLAN.md section 9), rest at the terrain's
        // real height instead of stacking on BuildGrid, and only care whether this exact cell already has a
        // tile - BuildOccupancyGrid never enters into it, so furniture and a floor tile can share a cell.
        private bool CheckIfCanBuildOnFloorCell() {
            pendingCell = MapHelper.WorldPositionToBlockIndex( hit.point );
            var snapped = MapHelper.GridToWorldPosition( new Vector2( pendingCell.x, pendingCell.y ) );
            // The ghost rests on whatever is actually rendered right now (still-bumped grass/sand included),
            // not the post-flatten height (Map.GroundTopY) - otherwise it would preview as buried inside the
            // bump instead of standing on top of it, since flattening only happens on PlaceBuilding.
            float groundY = mapManager.CurrentMap.CurrentGroundTopY( pendingCell.x, pendingCell.y );
            currentlyAttachedItem.position = new Vector3( snapped.x, groundY, snapped.z );


            var fits = mapManager.CurrentMap.FloorGrid.CanPlace( pendingCell.x, pendingCell.y );
            gridVisualizer.UpdateGrid( new Vector2Int( pendingCell.x, pendingCell.y ), 0,
                new Vector2Int( pendingCell.x, pendingCell.y ), new Vector2Int( 1, 1 ), fits );

            return fits;
        }

        // Walls/doors belong to the edge between two cells, not to either cell's interior (section 6): the
        // ghost snaps to whichever edge of the hit cell the finger is closest to and orients itself to match,
        // so straight runs and 90-degree corners are always flush - no manual rotation needed for these.
        private bool CheckIfCanBuildOnEdge() {
            var ( edgeX, edgeZ, side ) = MapHelper.WorldPositionToEdge( hit.point );
            pendingEdgeX = edgeX;
            pendingEdgeZ = edgeZ;
            pendingEdgeSide = side;

            var edgeGrid = mapManager.CurrentMap.EdgeGrid;
            pendingY = edgeGrid.TopY( edgeX, edgeZ, side );

            var snapped = MapHelper.EdgeToWorldPosition( edgeX, edgeZ, side );
            float yaw = side == EdgeSide.PlusX ? 90f : 0f;
            // The edge borders two cells; rest on the higher of them.
            float rise = mapManager.CurrentMap.BuildBaseRise( edgeX, edgeZ,
                side == EdgeSide.PlusX ? 2 : 1, side == EdgeSide.PlusZ ? 2 : 1 );
            currentlyAttachedItem.position = new Vector3( snapped.x, pendingY * MapGenerator.TILE_X_OFFSET + rise, snapped.z );
            currentlyAttachedItem.rotation = Quaternion.Euler( 0f, yaw, 0f );


            var box = new BuildEdgeBox( edgeX, edgeZ, pendingY, side, currentItem.sizeInCells.y, currentItem.category.ToEdgeKind() );
            var fits = edgeGrid.CanPlace( box );
            gridVisualizer.UpdateEdgeHighlight( edgeX, edgeZ, side, pendingY, fits );

            return fits;
        }

        IEnumerator DelayGoToDefaultState() {
            yield return new WaitForSeconds( 0.5f );
            gameManager.GoToDefaultState();
        }

        public void SetRotateLeft() {
            RotateStep( -1 );
        }

        public void SetRotateRight() {
            RotateStep( 1 );
        }

        // Kept only because the rotate buttons still fire this on PointerUp; rotation is a one-shot
        // step per tap now, so there is nothing left to stop.
        public void DisableRotation() {
        }

        private void RotateStep( int direction ) {
            if( currentlyAttachedItem == null || !CanRotate ) {
                return;
            }
            rotationStep = ( ( rotationStep + direction ) % 8 + 8 ) % 8;
            if( rotationTween != null ) {
                StopCoroutine( rotationTween );
            }
            rotationTween = StartCoroutine( TweenRotationTo( rotationStep * ROTATION_STEP_DEGREES ) );
        }

        private IEnumerator TweenRotationTo( float targetYDegrees ) {
            float startY = currentlyAttachedItem.eulerAngles.y;
            float delta = Mathf.DeltaAngle( startY, targetYDegrees );
            float elapsed = 0f;
            while( elapsed < ROTATION_TWEEN_SECONDS ) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01( elapsed / ROTATION_TWEEN_SECONDS );
                currentlyAttachedItem.rotation = Quaternion.Euler( 0f, startY + delta * t, 0f );
                yield return null;
            }
            currentlyAttachedItem.rotation = Quaternion.Euler( 0f, targetYDegrees, 0f );
            rotationTween = null;
        }

        private void PlaceBuilding() {
            if( !inventoryService.RemoveItems( currentRecipe.requirements ) ) {
                return;
            }

            if( IsFloorPiece ) {
                // Drop the piece from the ghost's preview height (CurrentGroundTopY, still-bumped) to where
                // the ground will actually end up (GroundTopY, flattened) BEFORE AddSceneObject captures this
                // transform into the saved SceneObjectModel - otherwise the save would keep the stale bumped
                // height even though SetFlattened below is about to make the terrain mesh flat under it.
                var floorPos = currentlyAttachedItem.position;
                floorPos.y = mapManager.CurrentMap.GroundTopY( pendingCell.x, pendingCell.y );
                currentlyAttachedItem.position = floorPos;
            }

            // AddSceneObject first: its SceneObjectModel hash is the stable id BuildGrid/EdgeGrid/FloorGrid uses
            // for this piece, the same id BlockContent.RegisterBuildingPiece will derive after a save/reload
            // (Phase 4/9).
            var sceneObject = mapManager.CurrentMap.AddSceneObject( currentlyAttachedItem );
            if( sceneObject != null ) {
                if( IsEdgePiece ) {
                    var box = new BuildEdgeBox( pendingEdgeX, pendingEdgeZ, pendingY, pendingEdgeSide, currentItem.sizeInCells.y, currentItem.category.ToEdgeKind() );
                    mapManager.CurrentMap.EdgeGrid.Place( sceneObject.GetHashCode(), box );
                } else if( IsFloorPiece ) {
                    var map = mapManager.CurrentMap;
                    map.FloorGrid.Place( sceneObject.GetHashCode(), pendingCell.x, pendingCell.y );
                    map.Terrain.SetFlattened( pendingCell.x, pendingCell.y, true );
                    map.ChunkManager.InvalidateVisualsAround( pendingCell.x, pendingCell.y );
                } else {
                    var size = currentItem.sizeInCells;
                    var box = new BuildCellBox( pendingCell.x, pendingY, pendingCell.y, size.x, size.y, size.z );
                    mapManager.CurrentMap.BuildGrid.Place( sceneObject.GetHashCode(), box );
                }
            }

            if( sceneObject != null && currentRecipe.clearsGrass ) {
                ClearGrassUnderPiece( sceneObject.GetHashCode() );
            }
            SpawnPlaceDust();
            cameraService.CameraShakeOnBuild();
            CloseBuildingPlacer();
        }

        private void ClearGrassUnderPiece( int pieceId ) {
            var map = mapManager.CurrentMap;
            if( IsEdgePiece ) {
                map.ClearGrassOnEdge( pieceId, pendingEdgeX, pendingEdgeZ, pendingEdgeSide, currentRecipe.grassClearRadius );
            } else {
                var size = currentItem.sizeInCells;
                map.ClearGrassUnderCells( pieceId, pendingCell.x, pendingCell.y, size.x, size.z, currentRecipe.grassClearRadius );
            }
        }

        // Dust is spread over the piece's ground footprint, in the piece's own axes (an edge piece is a thin
        // strip along its wall, anything else uses its cell size).
        private void SpawnPlaceDust() {
            float cell = MapGenerator.TILE_X_OFFSET;
            var footprint = IsEdgePiece
                ? new Vector2( cell, cell * 0.25f )
                : new Vector2( currentItem.sizeInCells.x * cell, currentItem.sizeInCells.z * cell );
            PlaceDust.Spawn( currentlyAttachedItem.position, currentlyAttachedItem.rotation, footprint );
        }

        private void ExitBuildingPlacer() {
            Lean.Pool.LeanPool.Despawn( currentlyAttachedItem.gameObject );
            CloseBuildingPlacer();
        }

        private void CloseBuildingPlacer() {
            StartCoroutine( DelayGoToDefaultState() );
            buildButton.SetActive( false );

            UnsubscribeTouch();

            rotateLeftButton.gameObject.SetActive( false );
            rotateRightButton.gameObject.SetActive( false );
            gridVisualizer.Hide();

            currentlyAttachedItem = null;
            currentRecipe = null;
            currentItem = null;
        }
    }
}
