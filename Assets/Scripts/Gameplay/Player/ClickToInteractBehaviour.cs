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
            if ( playerController != null ) {
                playerController.OnAutoMoveCancelledByInput += HandleAutoMoveCancelledByInput;
            }
        }

        private void OnDisable( ) {
            LeanTouch.OnFingerDown -= HandleFingerDown;
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

            var toTarget = currentTargetTransform.position - transform.position;
            toTarget.y = 0f;

            var interactionDistance = currentTarget.InteractionDistance;
            if ( toTarget.sqrMagnitude <= interactionDistance * interactionDistance ) {
                TriggerInteractionAndStop( );
                return;
            }

            playerController.SetAutoMoveVector( toTarget.normalized );
        }

        private void HandleFingerDown( LeanFinger finger ) {
            if ( finger.IsOverGui || MenuManager.isGamePaused ) {
                return;
            }
            if ( gameManager != null && gameState != null && gameManager.CurrentState != gameState ) {
                return;
            }

            CancelAutoWalk( );

            var ray = mainCamera.ScreenPointToRay( Input.mousePosition );
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
