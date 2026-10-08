using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.UI.HUD;
using UnityEngine;

namespace Hearthglade.Gameplay.Player {
    public class InteractBehaviour : MonoBehaviour {

        public IInteractable InteractableInProgress { get; private set; }
        public Action<IInteractable> InteractionStarted { get; set; }
        public Action<IInteractable> InteractionStepCompleted { get; set; }
        public Action<IInteractable> InteractionCompleted { get; set; }
        
        public Action<IInteractable> ObjectSelected { get; set; }
        public Action ObjectUnselected { get; set; }
        
        private readonly List<Transform> objectsInRange = new();
        private IInteractable currentlySelectedInteractable;
        private Transform currentlySelectedGameObject;

        void OnTriggerEnter(Collider collision)
        {
            // Ground colliders share this trigger's range but are not interactable: selecting one would hand null to the HUD.
            if( !collision.TryGetComponent<IInteractable>( out _ ) ) {
                return;
            }
            objectsInRange.Add(collision.transform);
        }

        private void OnTriggerExit( Collider other ) {
            objectsInRange.Remove(other.transform);
            if ( currentlySelectedGameObject == other.transform ) {
                UnselectCurrentObject();
            }
        }
        
        private void FixedUpdate( ) {
            if( !TryFindTarget( out var closestTarget ) ) {
                return;
            }
            if(closestTarget == currentlySelectedGameObject){
                return;
            }
            SwitchSelectedObject(closestTarget);
        }

        private bool TryFindTarget( out Transform closestTarget ) {
            closestTarget = null;

            if(objectsInRange.Count == 0) {
                return false;
            }

            var minDist = double.MaxValue;
            var result = false;
    
            for ( int i = objectsInRange.Count - 1; i >= 0 ; i-- ) {
                
                // It happens when target object got destroyed
                if( objectsInRange[i] == null ) {
                    objectsInRange.RemoveAt( i );
                    UnselectCurrentObject();
                    continue;
                }
                Vector3 dir = objectsInRange[i].transform.position - transform.position;

                if(dir.sqrMagnitude <= minDist) {
                    minDist = dir.sqrMagnitude;
                    closestTarget = objectsInRange[i];
                    result = true;
                }
            }

            return result;
        }

        private void SwitchSelectedObject( Transform newOutlineTarget ) {
            UnselectCurrentObject( );
            SelectNewObject( newOutlineTarget );
        } 
        
        private void UnselectCurrentObject( ) {
            currentlySelectedGameObject = null;
            ObjectUnselected?.Invoke();
        }
        
        private void SelectNewObject( Transform target ) {
            currentlySelectedGameObject = target;
            currentlySelectedInteractable = target.GetComponent<IInteractable>();
            ObjectSelected?.Invoke(currentlySelectedInteractable);
        }

        public void Interact( IInteractable interactable ) {
            interactable = interactable.GetInteractable();
            InteractableInProgress = interactable;
            if (interactable.InteractionTiming == InteractionTiming.Instant) {
                InteractionCompleted.Invoke( interactable );
            }
            else if ( interactable.InteractionTiming == InteractionTiming.LoadingBar ) {
                LoadingBarsManager.instance.CreateLoadingBar( 
                    () => CompleteInteraction( interactable ),
                    Mathf.Max( interactable.InteractionDuration, 0.1f ),
                    interactable.InteractCancelCallback
                );
            }
            StartInteraction( interactable );
        }
        private void StartInteraction( IInteractable interactable ) {
            // The interactable goes first: it is what picks the tool, which the animation and the hand then show.
            interactable.InteractionStart();
            InteractionStarted.Invoke( interactable );
        }

        private void CompleteInteraction( IInteractable interactable ) {
            InteractionCompleted?.Invoke( interactable );
            interactable.InteractionCompleted();
        }
    }
}