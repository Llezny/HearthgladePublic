using System;
using Animancer;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>How a flying insect moves. Speeds are world units per second, distances world units (a map cell is 0.3675).</summary>
    [Serializable]
    public class FlyerMovement {

        public float speed = 0.25f;

        [Tooltip("How far from the place it appeared the insect wanders.")]
        public float wanderRadius = 1.2f;

        [Tooltip("Flight height above the spot it appeared at.")]
        public float minHeight = 0.1f;
        public float maxHeight = 0.4f;

        [Tooltip("Up and down flutter on top of the flight path.")]
        public float bobAmplitude = 0.025f;
        public float bobFrequency = 4f;

        [Tooltip("Degrees per second.")]
        public float turnSpeed = 240f;
    }

    /// <summary>
    /// A butterfly: flutters about near the place it appeared at while its wings beat (one looping Animancer clip).
    /// Nothing to gather, click or fight, it only makes the meadow look alive.
    /// </summary>
    public class AmbientFlyer : Entity {

        [ SerializeField ] private AnimancerComponent animancer = null;
        [ SerializeField ] private AnimationClip flyClip = null;
        [ SerializeField ] private FlyerMovement movement = new();

        private Vector3 home;
        private Vector3 path;
        private Vector3 target;
        private float bobPhase;

        public override bool CanInteract( ) {
            return false;
        }

        public override void Construct( Map.Map targetMap, Transform player ) {
            base.Construct( targetMap, player );
            home = transform.position;
            path = home;
            bobPhase = Random.value * Mathf.PI * 2f;
            PickTarget( );
        }

        protected override void OnEnable( ) {
            base.OnEnable( );
            if( flyClip != null ) {
                animancer.Play( flyClip );
            }
        }

        private void Update( ) {
            var toTarget = target - path;
            float distance = toTarget.magnitude;
            if( distance < 0.05f ) {
                PickTarget( );
                return;
            }

            var direction = toTarget / distance;
            path += direction * Mathf.Min( movement.speed * Time.deltaTime, distance );
            float bob = Mathf.Sin( Time.time * movement.bobFrequency + bobPhase ) * movement.bobAmplitude;
            transform.position = path + Vector3.up * bob;

            var flat = new Vector3( direction.x, 0f, direction.z );
            if( flat.sqrMagnitude > 0.0001f ) {
                transform.rotation = Quaternion.RotateTowards( transform.rotation, Quaternion.LookRotation( flat ), movement.turnSpeed * Time.deltaTime );
            }
        }

        private void PickTarget( ) {
            var offset = Random.insideUnitCircle * movement.wanderRadius;
            target = home + new Vector3( offset.x, Random.Range( movement.minHeight, movement.maxHeight ), offset.y );
        }
    }
}
