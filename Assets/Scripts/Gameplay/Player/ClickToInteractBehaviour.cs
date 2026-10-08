using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Common;
using Lean.Touch;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Player {
    public class ClickToInteractBehaviour : MonoBehaviour {
        // Screen pixels; a quick finger that moved further than this is a flick, not a tap.
        private const float TapMaxTravel = 30f;

        private PlayerController playerController;
        private InteractBehaviour interactBehaviour;
        private GameManager gameManager;
        private MurmurService murmurService;
        private GameState gameState;

        private Camera mainCamera;
        private IInteractable currentTarget;
        private Transform currentTargetTransform;
        private int clickableLayerMask;

        [Inject]
        public void Construct( PlayerController playerController, InteractBehaviour interactBehaviour, GameManager gameManager, MurmurService murmurService ) {
            this.playerController = playerController;
            this.interactBehaviour = interactBehaviour;
            this.gameManager = gameManager;
            this.murmurService = murmurService;
        }

        private void Start( ) {
            mainCamera = Camera.main;
            clickableLayerMask = LayerMask.GetMask( "Clickable" );
            if ( gameManager != null ) {
                gameState = gameManager.GetState( "Game" );
                var buildingState = gameManager.GetState( "Building" );
                buildingState?.AddAtEnter( CancelAutoWalk );
            }
        }

        private void OnEnable( ) {
            LeanTouch.OnFingerDown += HandleFingerDown;
            LeanTouch.OnFingerTap += HandleFingerTap;
            if ( playerController != null ) {
                playerController.OnAutoMoveCancelledByInput += HandleAutoMoveCancelledByInput;
            }
        }

        private void OnDisable( ) {
            LeanTouch.OnFingerDown -= HandleFingerDown;
            LeanTouch.OnFingerTap -= HandleFingerTap;
            if ( playerController != null ) {
                playerController.OnAutoMoveCancelledByInput -= HandleAutoMoveCancelledByInput;
            }
        }

        private void FixedUpdate( ) {
            if ( currentTarget == null ) {
                return;
            }
            if ( !currentTargetTransform ) {
                CancelAutoWalk( );
                return;
            }

            var toTarget = FlatVectorToTarget( );
            if ( IsInInteractionRange( toTarget ) ) {
                TriggerInteractionAndStop( );
                return;
            }

            playerController.SetAutoMoveVector( toTarget.normalized );
        }

        private Vector3 FlatVectorToTarget( ) {
            var toTarget = currentTargetTransform.position - transform.position;
            toTarget.y = 0f;
            return toTarget;
        }

        private bool IsInInteractionRange( Vector3 flatToTarget ) {
            var interactionDistance = currentTarget.InteractionDistance;
            return flatToTarget.sqrMagnitude <= interactionDistance * interactionDistance;
        }

        private void HandleFingerDown( LeanFinger finger ) {
            if ( finger.IsOverGui || MenuManager.isGamePaused ) {
                return;
            }
            if ( gameManager != null && gameState != null && gameManager.CurrentState != gameState ) {
                return;
            }

            CancelAutoWalk( );
        }

        // Interaction fires on release: the floating joystick covers the whole screen, so a press that
        // starts on a scene object may just as well be the start of a joystick drag.
        private void HandleFingerTap( LeanFinger finger ) {
            if ( finger.StartedOverGui || MenuManager.isGamePaused ) {
                return;
            }
            if ( gameManager != null && gameState != null && gameManager.CurrentState != gameState ) {
                return;
            }
            if ( ( finger.ScreenPosition - finger.StartScreenPosition ).sqrMagnitude > TapMaxTravel * TapMaxTravel ) {
                return;
            }

            var ray = mainCamera.ScreenPointToRay( finger.ScreenPosition );
            if ( !Physics.Raycast( ray, out var hit, 100, clickableLayerMask ) ) {
                return;
            }

            var interactable = hit.transform.GetComponent<IInteractable>( );
            if ( interactable == null ) {
                return;
            }
            if ( !interactable.CanInteract( ) ) {
                if ( !string.IsNullOrEmpty( interactable.RefusalMessage ) ) {
                    murmurService.Show( interactable.RefusalMessage );
                }
                return;
            }

            BeginAutoWalk( interactable );
        }

        private void BeginAutoWalk( IInteractable target ) {
            currentTarget = target;
            currentTargetTransform = target.GetTransform( );
            // Already close enough: interact right away instead of starting a run that would end on the next tick.
            if ( IsInInteractionRange( FlatVectorToTarget( ) ) ) {
                TriggerInteractionAndStop( );
                return;
            }
            playerController.BeginAutoMove( );
        }

        private void TriggerInteractionAndStop( ) {
            var target = currentTarget;
            currentTarget = null;
            currentTargetTransform = null;
            playerController.EndAutoMove( );
            interactBehaviour.Interact( target );
        }

        private void CancelAutoWalk( ) {
            if ( currentTarget == null ) {
                return;
            }
            currentTarget = null;
            currentTargetTransform = null;
            playerController.EndAutoMove( );
        }

        private void HandleAutoMoveCancelledByInput( ) {
            if ( currentTarget == null ) {
                return;
            }
            currentTarget = null;
            currentTargetTransform = null;
        }
    }
}
