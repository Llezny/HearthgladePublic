using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Events;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Visual;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Common;
using Lean.Touch;
using UnityEngine;
using UnityEngine.Assertions;
using VContainer;

namespace Hearthglade.Gameplay.Player.Controller
{
    public class PlayerController : MonoBehaviour {
        
        public bool isDuringInteraction;
        private bool isKeyboardLocked = false;
        private bool isMouseLocked = false;
        public bool IsWalking { get; private set; }
        public bool IsAutoMoving { get; private set; }
        // Inside a room closed on every side (the same check as the "you're in home" label); a roof keeps off some of the cold and the heat.
        public bool IsIndoors { get; private set; }
        private bool joystickMustReturnToZero;
        private Vector2 lastJoystickInput;

        private const int PlayerRotationSpeed = 700;
        private const float MovementSpeed = 1.5f;
        private const float JoystickCancelThreshold = 0.001f;
        private const float DecelerationDuration = 0.3f;
        private Tween decelerationTween;
        private Transform currentlyPressedClickable = null;
        GameState buildingState = null;
        private float lastAppliedElevationRise = 0f;

        public Action onStartWalk;
        public Action onStopWalk;
        public Action<float> MovementSpeedChanged;
        public Action OnAutoMoveCancelledByInput;
        private Vector3 moveVector;

        [Header( "References " )]
        [SerializeField] AudioSO attackAudio = null;
        private GameManager gameManager;
        private TooltipManager tooltipManager = null;
        private ContextBubbleService contextBubbleService = null;
        private Camera mainCamera;
        private PauseMenu pauseMenu;
        private LoadedMapEvent loadedMapEvent;
        private Map.Map currentMap;
        private CanvasService canvasService;

        // "You're in home" POC (docs/BUILDING_SYSTEM_PLAN.md section 7) - lives here rather than a dedicated
        // component because PlayerController is already the one always-active, VContainer-injected object that
        // both ticks continuously and directly owns the player's own position/current map, with no scene
        // wiring needed for a new component. Worth a dedicated home if this grows beyond a POC bool.
        private const float RoomCheckIntervalSeconds = 0.5f;
        private float roomCheckTimer;
        private (int x, int z) lastRoomCheckCell = ( int.MinValue, int.MinValue );
        private RoomEnclosureIndicator roomEnclosureIndicator;

        // Darkening POC (docs/BUILDING_SYSTEM_PLAN.md phase 7) - same tick as the indicator above. Since a
        // flood-fill can only find a room by standing inside it, the last room found is cached so it can still
        // be darkened as a "roof" once the player steps back outside; a house that was never entered this
        // session shows no darkening yet, a known POC limitation with only one house tested so far.
        private RoomDarknessOverlay roomDarknessOverlay;
        private RoomWallCutaway roomWallCutaway;
        private List<(int x, int z)> lastKnownRoomCells;

        [Inject]
        public void Construct( PauseMenu pauseMenu, TooltipManager tooltipManager, LoadedMapEvent loadedMapEvent, GameManager gameManager, CanvasService canvasService, ContextBubbleService contextBubbleService ) {
            this.contextBubbleService = contextBubbleService;
            this.pauseMenu = pauseMenu;
            this.tooltipManager = tooltipManager;
            this.loadedMapEvent = loadedMapEvent;
            this.loadedMapEvent.RegisterListener( OnMapChanged );
            this.gameManager = gameManager;
            this.canvasService = canvasService;
        }

        /// <summary>Set while the player is inside a house: replaces the map's terrain as the judge of where they can walk.</summary>
        public IWalkArea WalkArea { get; private set; }

        public void EnterInterior( IWalkArea walkArea ) {
            WalkArea = walkArea;
            IsIndoors = true;
            roomEnclosureIndicator?.SetEnclosed( true );
            roomDarknessOverlay?.Hide();
            roomWallCutaway?.FadeIn();
        }

        public void ExitInterior( ) {
            WalkArea = null;
            IsIndoors = false;
            // Forces the room check on the next frame, whatever cell the player comes out in.
            lastRoomCheckCell = ( int.MinValue, int.MinValue );
            roomCheckTimer = 0f;
        }

        /// <summary>Stops all walking at once (before a teleport).</summary>
        public void StopMoving( ) {
            EndAutoMove( );
            KillDecelerationTween( );
            moveVector = Vector3.zero;
            if ( IsWalking ) {
                StopWalking( );
            }
            MovementSpeedChanged?.Invoke( 0f );
        }

        /// <summary>
        /// Moves the player to a position now. The player is an interpolated Rigidbody: without moving the body too, its old pose wins on
        /// the next physics step.
        /// </summary>
        public void TeleportTo( Vector3 position, Quaternion rotation ) {
            transform.SetPositionAndRotation( position, rotation );
            if ( TryGetComponent<Rigidbody>( out var body ) ) {
                body.position = position;
                body.rotation = rotation;
            }
            Physics.SyncTransforms();
        }

        private void OnMapChanged( Map.Map newMap ) {
            this.currentMap = newMap;
            this.lastAppliedElevationRise = 0f;
            this.lastKnownRoomCells = null;
            this.lastRoomCheckCell = ( int.MinValue, int.MinValue );
            this.roomDarknessOverlay?.Reset();
            this.roomWallCutaway?.Reset();
        }

        private void Start( ) {
            Assert.IsNotNull( gameManager, "Game Manager is null" );
            buildingState = gameManager.GetState( "Building" );
            buildingState.AddAtEnter( LockControl );
            buildingState.AddAtExit( UnlockControl );
            LeanTouch.OnFingerDown += HandleFingerDown;
            LeanTouch.OnFingerUp += HandleFingerUp;

            mainCamera = Camera.main;
            roomEnclosureIndicator = RoomEnclosureIndicator.Create( canvasService.HudCanvas.transform );
            roomDarknessOverlay = new RoomDarknessOverlay();
            roomWallCutaway = new RoomWallCutaway();
        }

        void Update( ) {
            // Every frame, unlike the room check below - the walls and the room's darkening both ease over a
            // fraction of a second, far finer than the check that decides what they should be showing.
            roomWallCutaway?.Tick( Time.deltaTime );
            roomDarknessOverlay?.Tick( Time.deltaTime );

            roomCheckTimer -= Time.deltaTime;
            // Inside a house the terrain says nothing about the room (the house has its own walk area).
            if ( currentMap == null || WalkArea != null ) {
                return;
            }

            // Which room the player is in can only change when they step into a different cell, so react the
            // moment that happens instead of waiting out the tick - otherwise walking through a doorway sat
            // there doing nothing for up to half a second before the walls began to fade. The timer stays on
            // as a safety net for the changes that happen while standing still: a wall going up, a door
            // swinging open.
            var cellIdx = MapHelper.WorldPositionToBlockIndex( transform.position );
            var cell = ( x: cellIdx.x, z: cellIdx.y );
            if ( roomCheckTimer > 0f && cell == lastRoomCheckCell ) {
                return;
            }
            roomCheckTimer = RoomCheckIntervalSeconds;
            lastRoomCheckCell = cell;
            UpdateRoomEnclosureIndicator( cell );
        }

        // Capped cardinal flood-fill from the player's cell over terrain that is walkable and not blocked by
        // a wall/door edge (BuildEdgeGrid) at the player's own Y layer - see RoomEnclosureDetector for the
        // "bounded fill = enclosed" logic itself.
        private void UpdateRoomEnclosureIndicator( (int x, int z) cell ) {
            // Only used for the wall/door edge check below - RoomDarknessOverlay is height-independent (post process).
            int y = Mathf.RoundToInt( transform.position.y / MapGenerator.TILE_X_OFFSET );
            var terrain = currentMap.Terrain;
            var edgeGrid = currentMap.EdgeGrid;

            // The flood fill reads the terrain/edge grid/layer through fields and a cached delegate instead of
            // local functions: those captured locals, so every check allocated a closure and delegates, on top
            // of the fill's own collections (see RoomFloodFill).
            probeTerrain = terrain;
            probeEdgeGrid = edgeGrid;
            probeY = y;
            canMoveDelegate ??= CanMove;

            List<(int x, int z)> roomCells = null;
            if ( terrain.InBounds( cell.x, cell.z ) && terrain.Get( cell.x, cell.z ).Walkable
                 && roomFloodFill.TryFill( cell.x, cell.z, canMoveDelegate ) ) {
                // The same room as last time keeps the list it already has: no copy, and RoomDarknessOverlay
                // recognises the list and skips rebuilding its mask.
                roomCells = SameAsLastKnownRoom( roomFloodFill.Cells )
                    ? lastKnownRoomCells
                    : new List<(int x, int z)>( roomFloodFill.Cells );
            }
            IsIndoors = roomCells != null;
            roomEnclosureIndicator.SetEnclosed( IsIndoors );

            if ( roomCells != null ) {
                if ( !ReferenceEquals( roomCells, lastKnownRoomCells ) ) {
                    lastKnownRoomCells = roomCells;
                    lastKnownRoomSet.Clear();
                    foreach ( var roomCell in roomCells ) {
                        lastKnownRoomSet.Add( roomCell );
                    }
                }
                roomDarknessOverlay.ShowOutsideDark( roomCells );
                // Only while actually inside: seen from outside, a room keeps all four of its walls.
                roomWallCutaway.Apply( currentMap, roomCells, mainCamera.transform.forward );
                return;
            }

            roomWallCutaway.FadeIn();
            if ( lastKnownRoomCells != null ) {
                roomDarknessOverlay.ShowRoof( lastKnownRoomCells );
            }
            else {
                roomDarknessOverlay.Hide();
            }
        }

        private readonly RoomFloodFill roomFloodFill = new();
        private readonly HashSet<(int x, int z)> lastKnownRoomSet = new();

        // Same cells as the last room found, whatever order the fill happened to visit them in.
        private bool SameAsLastKnownRoom( List<(int x, int z)> found ) {
            if ( lastKnownRoomCells == null || found.Count != lastKnownRoomCells.Count ) {
                return false;
            }
            foreach ( var foundCell in found ) {
                if ( !lastKnownRoomSet.Contains( foundCell ) ) {
                    return false;
                }
            }
            return true;
        }
        private TerrainGrid probeTerrain;
        private BuildEdgeGrid probeEdgeGrid;
        private int probeY;
        private Func<int, int, int, int, bool> canMoveDelegate;

        // Stepping from (x, z) to its cardinal neighbour: the neighbour is walkable terrain and no wall/door
        // edge sits between them at the player's Y layer.
        private bool CanMove( int x, int z, int nx, int nz ) {
            if ( !probeTerrain.InBounds( nx, nz ) || !probeTerrain.Get( nx, nz ).Walkable ) {
                return false;
            }
            var ( ex, ez, side ) = BuildEdgeGrid.Canonicalize( x, z, nx - x, nz - z );
            return !probeEdgeGrid.IsWallOccupied( ex, ez, probeY, side );
        }

        private void OnDestroy( ) {
            LeanTouch.OnFingerDown -= HandleFingerDown;
            LeanTouch.OnFingerUp -= HandleFingerUp;
            loadedMapEvent?.UnregisterListener( OnMapChanged );
        }

        void FixedUpdate( ) {
            if ( !isKeyboardLocked ) {
                KeyboardInput( );
            }

            if ( moveVector != Vector3.zero ) {
                RotatePlayer( );
                Walk( );
            }

            ApplyTerrainElevation( );
        }

        private const float ElevationLerpSpeed = 0.5f;

        private void ApplyTerrainElevation( ) {
            if ( currentMap == null || WalkArea != null ) {
                return;
            }
            var terrainVisual = TerrainVisualSO.Load();
            // Not "elevatedYRise <= 0 -> bail entirely" any more: a floor tile's own thickness (below) is a
            // second, independent reason to adjust standing height, unrelated to whether the grass/sand bump
            // feature is even enabled.
            float elevatedRise = terrainVisual != null ? terrainVisual.elevatedYRise : 0f;
            var index = MapHelper.WorldPositionToBlockIndex( transform.position );
            float desiredRise = lastAppliedElevationRise;
            var cell = currentMap.Terrain.Get( index.x, index.y );
            if ( cell.Solid ) {
                // TopY(0, rise) is exactly the rise amount: 0 when not elevated, or when a floor tile
                // flattened this cell (docs/BUILDING_SYSTEM_PLAN.md section 9) - reusing it instead of
                // checking cell.Elevated alone keeps this in sync with whatever the terrain mesh itself
                // renders, so the player doesn't keep standing at the old bump height once it flattens.
                desiredRise = cell.TopY( 0f, elevatedRise ).Value;
                // A placed floor tile has real thickness above the (flattened) ground it sits on - stand on
                // top of it instead of sinking to bare-ground height.
                if ( currentMap.FloorGrid.Has( index.x, index.y ) ) {
                    desiredRise += Map.Map.FloorTileHeight;
                }
            }
            float newRise = Mathf.MoveTowards( lastAppliedElevationRise, desiredRise, ElevationLerpSpeed * Time.fixedDeltaTime );
            if ( Mathf.Approximately( newRise, lastAppliedElevationRise ) ) {
                return;
            }
            var pos = transform.position;
            pos.y += newRise - lastAppliedElevationRise;
            transform.position = pos;
            lastAppliedElevationRise = newRise;
        }

        private void RotatePlayer( ) {
            var toRotation = Quaternion.LookRotation( moveVector, Vector3.up );
            transform.rotation = Quaternion.RotateTowards( transform.rotation, toRotation,
                Time.fixedDeltaTime * PlayerRotationSpeed );
        }
        
        private bool CanStep( Vector3 from, Vector3 to ) {
            return WalkArea != null ? WalkArea.CanMoveTo( from, to ) : currentMap.CanMoveTo( from, to );
        }

        // Whether a step is allowed is decided entirely by terrain data (Map.CanMoveTo, checked below), not
        // by physics colliders: the terrain grid is loaded synchronously for the whole map up front, while
        // chunk colliders/GameObjects stream in asynchronously and are unreliable to gate movement on (a
        // collider-trigger-based ground check used to get permanently stuck ungrounded after a save load).
        private void Walk( ) {
            var step = MovementSpeed * Time.fixedDeltaTime * moveVector;
            var currentPos = transform.position;

            if ( currentMap == null && WalkArea == null ) {
                transform.Translate( step, Space.World );
            }
            else if ( CanStep( currentPos, currentPos + step ) ) {
                transform.Translate( step, Space.World );
            }
            else {
                var stepX = new Vector3( step.x, 0f, 0f );
                var stepZ = new Vector3( 0f, 0f, step.z );

                if ( stepX.sqrMagnitude > 0f && CanStep( currentPos, currentPos + stepX ) ) {
                    transform.Translate( stepX, Space.World );
                }
                else if ( stepZ.sqrMagnitude > 0f && CanStep( currentPos, currentPos + stepZ ) ) {
                    transform.Translate( stepZ, Space.World );
                }
            }

            if ( decelerationTween == null ) {
                LoadingBarsManager.instance.DeleteAllWaitableBars( ); // Todo make loadingBarsManager use onStartWalk events
            }
        }

        // The joystick appearing means the press was a drag, not a long press on a scene object.
        public void HideTooltip( ) {
            tooltipManager.HideToolTip( );
        }

        public void SetMoveVector( Vector2 joystickMoveVector ) {
            lastJoystickInput = joystickMoveVector;
            if ( IsAutoMoving ) {
                var aboveThreshold = joystickMoveVector.sqrMagnitude > JoystickCancelThreshold;
                if ( joystickMustReturnToZero ) {
                    if ( !aboveThreshold ) {
                        joystickMustReturnToZero = false;
                    }
                }
                else if ( aboveThreshold ) {
                    IsAutoMoving = false;
                    OnAutoMoveCancelledByInput?.Invoke();
                }
                if ( IsAutoMoving ) {
                    return;
                }
            }

            KillDecelerationTween( );

            var desiredMoveDirection = JoystickVectorToWorldMoveVector( joystickMoveVector );
            if ( moveVector == Vector3.zero && desiredMoveDirection != Vector3.zero ) {
                StartWalking( );
            }
            else if ( desiredMoveDirection == Vector3.zero && ( moveVector != Vector3.zero || IsWalking ) ) {
                // Also when moveVector is already zero but a killed deceleration tween left IsWalking set.
                StopWalking( );
                MovementSpeedChanged?.Invoke( 0f );
            }

            if ( moveVector != desiredMoveDirection ) {
                moveVector = desiredMoveDirection;
                MovementSpeedChanged?.Invoke( moveVector.magnitude );
            }
        }

        public void BeginAutoMove( ) {
            if ( IsAutoMoving ) {
                return;
            }
            KillDecelerationTween( );
            IsAutoMoving = true;
            joystickMustReturnToZero = lastJoystickInput.sqrMagnitude > JoystickCancelThreshold;
            moveVector = Vector3.zero;
            if ( !IsWalking ) {
                StartWalking( );
            }
            MovementSpeedChanged?.Invoke( 1f );
        }

        public void EndAutoMove( ) {
            if ( !IsAutoMoving ) {
                return;
            }
            IsAutoMoving = false;
            KillDecelerationTween( );
            // Nothing to decelerate from: a tween here only leaves a window in which killing it strands the run animation.
            if ( moveVector == Vector3.zero ) {
                FinalizeStop( );
                return;
            }
            decelerationTween = DOTween.To(
                () => moveVector,
                v => moveVector = v,
                Vector3.zero,
                DecelerationDuration
            ).SetEase( Ease.OutQuad )
             .OnUpdate( () => MovementSpeedChanged?.Invoke( moveVector.magnitude ) )
             .OnComplete( FinalizeStop );
        }

        public void SetAutoMoveVector( Vector3 worldDir ) {
            if ( !IsAutoMoving ) {
                return;
            }
            KillDecelerationTween( );
            moveVector = worldDir;
        }

        private void FinalizeStop( ) {
            decelerationTween = null;
            moveVector = Vector3.zero;
            if ( IsWalking ) {
                StopWalking( );
            }
            MovementSpeedChanged?.Invoke( 0f );
        }

        private void KillDecelerationTween( ) {
            if ( decelerationTween == null ) {
                return;
            }
            decelerationTween.Kill( );
            decelerationTween = null;
        }

        private Vector3 JoystickVectorToWorldMoveVector( Vector2 moveVec ) {
            //camera forward and right vectors:
            var forward = mainCamera.transform.forward;
            var right = mainCamera.transform.right;

            //project forward and right vectors on the horizontal plane (y = 0)
            forward.y = 0f;
            right.y = 0f;
            forward = forward.normalized;
            right = right.normalized;

            //this is the direction in the world space we want to move:
            return forward * moveVec.y + right * moveVec.x;
        }

        private void StartWalking( ) {
            if ( IsWalking ) {
                return;
            }
            IsWalking = true;
            onStartWalk?.Invoke( );
        }

        private void StopWalking( ) {
            if ( !IsWalking ) {
                return;
            }
            IsWalking = false;
            onStopWalk?.Invoke( );
        }

        #region input

        private void KeyboardInput( ) {
            if ( Input.GetKeyUp( KeyCode.Escape ) ) {
                if ( MenuManager.instance.openedMenu.Count > 0 ) {
                    MenuManager.instance.CloseAll( true );
                }
                else {
                    pauseMenu.OpenMenu( );
                }
            }
        }

        private void HandleFingerUp( LeanFinger finger ) {
            tooltipManager.HideToolTip( );
            CancelContextHold( );
            // if ( finger.IsOverGui || isMouseLocked || MenuManager.isGamePaused ) {
            //     return;
            // }
        }

        private void HandleFingerDown( LeanFinger finger ) {
            if ( finger.IsOverGui || isMouseLocked || MenuManager.isGamePaused ) {
                return;
            }

            var ray = mainCamera.ScreenPointToRay( Input.mousePosition );
            if ( !Physics.Raycast( ray, out var hit, 100, LayerMask.GetMask( "Clickable" ) ) ) {
                return;
            }

            currentlyPressedClickable = hit.transform;
            var clickable = currentlyPressedClickable.GetComponent<IInteractable>( );
            // An object with actions opens their menu when held (a tap still interacts); any other object shows its tooltip.
            if ( clickable is IContextActions withActions && withActions.ContextActions.Count > 0 ) {
                CancelContextHold( );
                contextHold = StartCoroutine( OpenContextBubbleAfterHold( finger, clickable, withActions ) );
            }
            else {
                tooltipManager.SetupTooltipAndStartCounter( clickable.TooltipTitle, clickable.TooltipDescription );
            }
        }

        private const float ContextHoldSeconds = 0.5f;
        // Screen pixels; a finger that travels further is steering the joystick, not asking for the menu.
        private const float ContextHoldMaxTravel = 30f;
        private Coroutine contextHold;

        private void CancelContextHold( ) {
            if ( contextHold != null ) {
                StopCoroutine( contextHold );
                contextHold = null;
            }
        }

        private IEnumerator OpenContextBubbleAfterHold( LeanFinger finger, IInteractable target, IContextActions actions ) {
            var start = finger.ScreenPosition;
            for ( float held = 0f; held < ContextHoldSeconds; held += Time.unscaledDeltaTime ) {
                if ( ( finger.ScreenPosition - start ).magnitude > ContextHoldMaxTravel ) {
                    contextHold = null;
                    yield break;
                }
                yield return null;
            }
            contextHold = null;
            contextBubbleService.Show( target.TooltipTitle, actions.ContextActions, target.GetTransform( ) );
        }

        public void LockControl( ) {
            this.isKeyboardLocked = true;
            this.isMouseLocked = true;
        }

        public void UnlockControl( ) {
            this.isKeyboardLocked = false;
            this.isMouseLocked = false;
        }

        /* TODO - PlayerController should not be responsible for this shit, in my dreams I should not either
        public void TryAttack( Entity target ) {
            //   animationController.PlayAnimation( "Slash", 0.3f, () => OnAttackAnimEnd( target ) );
        }
        
        private void OnAttackAnimEnd( Entity target ) {
            sfx.Play( attackAudio );
            //  animationController.PlayAnimation( "Idle" );
            target.GetAttacked( this );
        }
        */
    }

    #endregion input
}
