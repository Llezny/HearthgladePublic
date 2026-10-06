using Lean.Pool;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>
    /// A handful of fireflies drifting over one spot at night, drawn by one particle system (so it costs a draw call,
    /// not an object per insect). Nothing to gather or click. They fade out at dawn instead of vanishing.
    /// </summary>
    public class FireflySwarm : Entity {

        [ SerializeField ] private ParticleSystem particles = null;
        [ SerializeField ] private float fadeSeconds = 2f;

        public override bool CanInteract( ) {
            return false;
        }

        protected override void OnEnable( ) {
            base.OnEnable( );
            particles.Clear( );
            particles.Play( true );
        }

        public override void Retire( ) {
            if( !isActiveAndEnabled ) {
                base.Retire( );
                return;
            }
            particles.Stop( true, ParticleSystemStopBehavior.StopEmitting );
            LeanPool.Despawn( gameObject, fadeSeconds );
        }
    }
}
