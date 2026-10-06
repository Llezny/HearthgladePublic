using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.ItemPickup {
    public class ItemPickup : HUD.HUD {

        [ SerializeField ] Button pickupButton;
        private InteractBehaviour interactBehaviour;
        private PlayerController playerController;
        protected override Vector2 positionOnClose { get => new( 1200, -880 ); }
        protected override Vector2 positionOnOpen { get => new( 400, -880 ); }
        protected override bool hideOnPause => true;
        protected override bool hideOnAnyMenuShow => true;

        [ Inject ]
        public void Construct( InteractBehaviour interactBehaviour, PlayerController playerController ) {
            this.interactBehaviour = interactBehaviour;
            this.playerController = playerController; 
        }

        protected override void OnEnable() {
            base.OnEnable();
            interactBehaviour.ObjectUnselected += DeactivateButton;
            interactBehaviour.ObjectSelected += ActivateButton;
        }
        protected override void OnDisable() {
            base.OnDisable();
            interactBehaviour.ObjectUnselected -= DeactivateButton;
            interactBehaviour.ObjectSelected -= ActivateButton;    
        }

        private void ActivateButton( IInteractable interactble ) {
            pickupButton.interactable = true;
            pickupButton.onClick.RemoveAllListeners();
            pickupButton.onClick.AddListener( () => InteractIfPossible( interactble ) );
        }

        private void DeactivateButton() {
            pickupButton.interactable = false;
        }

        private void InteractIfPossible( IInteractable interactable ) {
            if( interactable.CanInteract() && !playerController.IsWalking ) {
                interactBehaviour.Interact( interactable );
            }
        }
    }
}
