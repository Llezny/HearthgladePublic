using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>
    /// A harmless animal that wanders around and runs away from the player. Subclasses only say how their
    /// animations are played; the prefab sets how it moves.
    /// </summary>
    public abstract class PassiveAnimal : Entity {

        [ SerializeField ] private AnimalMovement movement = new();
        [ SerializeField ] private AnimalForaging foraging = new();

        private PassiveEntityController controller;
        private IAnimalAnimator animalAnimator;
        private bool bound;

        protected abstract IAnimalAnimator CreateAnimator( );

        public override void Construct( Map.Map targetMap, Transform player ) {
            base.Construct( targetMap, player );
            Unbind( );
            controller = new PassiveEntityController( transform, targetMap, player, movement, foraging, GetType().Name );
            // A pooled animal keeps its animator between lives.
            animalAnimator ??= CreateAnimator( );
            if( isActiveAndEnabled ) {
                Bind( );
            }
        }

        public override void GetAttacked( PlayerController attackSource ) {
            controller?.GetAttacked( attackSource );
            base.GetAttacked( attackSource );
        }

        private void Update( ) {
            controller?.Update( Time.deltaTime );
        }

        protected override void OnEnable( ) {
            base.OnEnable( );
            if( controller != null ) {
                Bind( );
            }
        }

        protected override void OnDisable( ) {
            Unbind( );
            base.OnDisable( );
        }

        private void Bind( ) {
            if( bound ) {
                return;
            }
            bound = true;
            controller.StateChanged += animalAnimator.Play;
            animalAnimator.Play( controller.State );
        }

        private void Unbind( ) {
            if( !bound ) {
                return;
            }
            bound = false;
            controller.StateChanged -= animalAnimator.Play;
        }
    }
}
