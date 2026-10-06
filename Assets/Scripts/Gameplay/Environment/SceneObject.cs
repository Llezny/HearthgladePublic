using System;
using Hearthglade.Gameplay.Map;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment {
    public abstract class SceneObject : MonoBehaviour, IInteractable {
        private SceneObjectModel sceneObjectModel;

        public SceneObjectModel SceneObjectModel {
            get => sceneObjectModel;
            set {
                sceneObjectModel = value;
                OnSceneObjectModelAssigned();
            }
        }

        // Runs after the object was spawned and injected (OnEnable and Construct came first), when its place in the map is known.
        protected virtual void OnSceneObjectModelAssigned() { }

        public Action<IInteractable> InteractableEnabled { get; set; }
        public Action<IInteractable> InteractableDisabled { get; set; }

        public Transform GetTransform( ) {
            return transform;
        }
        
        public virtual IInteractable GetInteractable()  {
            return this;
        }

        protected virtual void OnEnable( ) {
            InteractableEnabled?.Invoke(this);
        }
        
        protected virtual void OnDisable( ) {
            InteractableDisabled?.Invoke(this);
        }

        public virtual bool CanInteract( ) {
            return true;
        }
        
        public virtual void InteractionStart() {}
        public virtual void InteractionCompleted( ) {}
        public virtual void Tick() {  }

    }
}