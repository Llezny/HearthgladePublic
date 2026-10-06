using System;
using Animancer;
using DG.Tweening;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Animation {
    public class PlayerAnimationController : MonoBehaviour {
        [ SerializeField ] private AnimancerComponent animancerComponent;
        [ SerializeField ] private Float1ControllerTransition transitionController;
        [ SerializeField ] private PlayerController playerController;
        [ SerializeField ] private InteractBehaviour interactBehaviour;

        private Tween targetObjectTween;
 
        public float Speed {
            get => transitionController.State.Parameter;
            set => transitionController.State.Parameter = value;
        }

        private void OnEnable() {
            PlayMovementAnim( );
            playerController.MovementSpeedChanged += ChangeSpeed;
            playerController.onStartWalk += PlayMovementAnim;
            interactBehaviour.InteractionCompleted += _ => PlayMovementAnim( );
            interactBehaviour.InteractionStarted += SetPlayerAnimation;
        }
        
        private void OnDisable( ) {
            playerController.MovementSpeedChanged -= ChangeSpeed;
            interactBehaviour.InteractionStarted -= SetPlayerAnimation;
            interactBehaviour.InteractionCompleted -= _ => PlayMovementAnim( );
            playerController.onStartWalk -= PlayMovementAnim;
        }

        private void PlayHitAnimAndShakeTargetObject( IInteractable target ) {
            // AudioManager.Instance.Play( resourceSO.OnGather );
            target.InteractableDisabled += _ => { targetObjectTween?.Kill( ); };
            targetObjectTween = target.GetTransform( ).DOShakeRotation( 0.5f, strength: 5, randomness: 40, vibrato: 8 );
        }

        public void SetPlayerAnimation( IInteractable interactable ) {
            if ( !interactable?.InteractionAnim ) {
                return;
            }
            var animState = animancerComponent.Play(interactable?.InteractionAnim, 0.15f, FadeMode.NormalizedSpeed );
            animState.Events.NormalizedEndTime = 0.5f;
            animState.Events.Add(  0.5f, () => PlayHitAnimAndShakeTargetObject( interactable ));
        }
        
        private void ChangeSpeed( float movementSpeed ) {
            Speed = movementSpeed;
        }

        private void PlayMovementAnim( ) {
            animancerComponent.Play(transitionController, 0.25f);
        }
    }
}