using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service {
    public class FollowCamera : MonoBehaviour {
        public Transform target;
        public Vector3 offset = new Vector3(-6, 6, -10);

        private float shakeAmplitude;
        private float shakeFrequency;
        private float shakeDuration;
        private float shakeStartTime;

        // Rattles the camera in its own screen plane, fading out linearly over the duration.
        public void Shake( float frequency, float amplitude, float duration ) {
            shakeFrequency = frequency;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeStartTime = Time.time;
        }

        private void LateUpdate() {
            transform.position = target.position + offset + ShakeOffset();
        }

        private Vector3 ShakeOffset() {
            float elapsed = Time.time - shakeStartTime;
            if( elapsed >= shakeDuration ) {
                return Vector3.zero;
            }
            float fade = 1f - elapsed / shakeDuration;
            float phase = elapsed * shakeFrequency * Mathf.PI * 2f;
            // Two unrelated sine rates so the motion is a jitter, not a straight back-and-forth line.
            return ( transform.right * Mathf.Sin( phase ) + transform.up * Mathf.Sin( phase * 1.3f + 1f ) ) * ( shakeAmplitude * fade );
        }
    }
}
