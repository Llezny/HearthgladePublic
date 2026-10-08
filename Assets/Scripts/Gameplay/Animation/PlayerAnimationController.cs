using System;
using Animancer;
using DG.Tweening;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Animation {
    public class PlayerAnimationController : MonoBehaviour {
        [ SerializeField ] private AnimancerComponent animancerComponent;
        [ SerializeField ] private Float1ControllerTransition transitionController;
        [ SerializeField ] private PlayerController playerController;
        [ SerializeField ] private InteractBehaviour interactBehaviour;

        // The blend back to walking and standing; longer right after a crouched loop.
        private const float MOVEMENT_FADE = 0.25f;

        private InteractionAnimationPicker picker;
        private float movementFade = MOVEMENT_FADE;
        private Tween targetObjectTween;

        [ Inject ]
        public void Construct( InteractionAnimationPicker picker ) {
            this.picker = picker;
        }

        public float Speed {
            get => transitionController.State.Parameter;
            set => transitionController.State.Parameter = value;
        }

        private void OnEnable() {
            PlayMovementAnim( );
            playerController.MovementSpeedChanged += ChangeSpeed;
            playerController.onStartWalk += PlayMovementAnim;
            interactBehaviour.InteractionCompleted += OnInteractionCompleted;
            interactBehaviour.InteractionStarted += SetPlayerAnimation;
        }

        private void OnDisable( ) {
            playerController.MovementSpeedChanged -= ChangeSpeed;
            interactBehaviour.InteractionStarted -= SetPlayerAnimation;
            interactBehaviour.InteractionCompleted -= OnInteractionCompleted;
            playerController.onStartWalk -= PlayMovementAnim;
        }

        private void OnInteractionCompleted( IInteractable _ ) {
            PlayMovementAnim( );
        }

        private void PlayHitAnimAndShakeTargetObject( IInteractable target ) {
            // sfx.Play( resourceSO.OnGather );
            target.InteractableDisabled += _ => { targetObjectTween?.Kill( ); };
            targetObjectTween = target.GetTransform( ).DOShakeRotation( 0.5f, strength: 5, randomness: 40, vibrato: 8 );
        }

        // The work clip InteractionAnimationPicker chooses; nothing plays when there is none.
        public void SetPlayerAnimation( IInteractable interactable ) {
            if( picker == null || !picker.TryPick( interactable, out var picked ) ) {
                return;
            }
            // Getting up again takes at least as long as getting down took.
            movementFade = Mathf.Max( MOVEMENT_FADE, picked.FadeIn );
            var animState = animancerComponent.Play( picked.Clip, picked.FadeIn, FadeMode.FixedDuration );
            animState.Events.NormalizedEndTime = picked.HitTime;
            animState.Events.Add( picked.HitTime, () => PlayHitAnimAndShakeTargetObject( interactable ));
        }

        private void ChangeSpeed( float movementSpeed ) {
            Speed = movementSpeed;
        }

        private void PlayMovementAnim( ) {
            animancerComponent.Play( transitionController, movementFade );
            movementFade = MOVEMENT_FADE;
        }
    }
}
