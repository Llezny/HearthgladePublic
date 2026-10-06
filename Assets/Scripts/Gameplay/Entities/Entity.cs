using System;
using Hearthglade.Core.Stats;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities
{
    /// <summary>
    /// Something alive on the map that the <see cref="EntityManager"/> spawns from a pool. The manager calls
    /// <see cref="Construct"/> right after taking one from the pool, so everything a life needs is set up there.
    /// </summary>
    public class Entity : MonoBehaviour, IInteractable {

        public Action<IInteractable> InteractableEnabled { get; set; }
        public Action<IInteractable> InteractableDisabled { get; set; }

        /// <summary>Raised when the entity dies or is removed by the game, right before it goes back to the pool.</summary>
        public event Action<Entity> Died;

        protected PlayerNeed Health;
        protected Map.Map targetMap;

        public virtual void Construct( Map.Map targetMap, Transform player ) {
            this.targetMap = targetMap;
            ResetHealth();
        }

        public virtual void GetAttacked( PlayerController attackSource ) {
            Health.CurrentValue -= 15; //TODO change 15 to some calculated damage
        }

        public Transform GetTransform( ) {
            return transform;
        }

        public virtual bool CanInteract( ) {
            return true;
        }

        public IInteractable GetInteractable()  {
            return this;
        }

        /// <summary>The entity is no longer wanted (its time of day is over): it leaves, by default at once.</summary>
        public virtual void Retire( ) {
            Lean.Pool.LeanPool.Despawn( this.gameObject );
        }

        protected virtual void Die( ) {
            Died?.Invoke( this );
            Lean.Pool.LeanPool.Despawn( this.gameObject );
        }

        protected virtual void OnEnable( ) {
            InteractableEnabled?.Invoke(this);
            if( Health != null ) {
                Health.OnCurrentValueEqualZero += Die;
            }
        }

        protected virtual void OnDisable( ) {
            InteractableDisabled?.Invoke(this);
            if( Health != null ) {
                Health.OnCurrentValueEqualZero -= Die;
            }
        }

        // A pooled entity starts every life healthy.
        private void ResetHealth() {
            if( Health != null ) {
                Health.OnCurrentValueEqualZero -= Die;
            }
            Health = new PlayerNeed( 100, 100, 100 );
            if( isActiveAndEnabled ) {
                Health.OnCurrentValueEqualZero += Die;
            }
        }
    }
}
