using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service {
    public class CameraService : MonoBehaviour, ICameraService {

        // A barely-there thud: ~0.4% of the visible width, over about a tenth of a second.
        public void CameraShakeOnBuild() {
            CameraShake( 14f, 0.01f, 0.12f );
        }

        /// <param name="frequency">Oscillations per second.</param>
        /// <param name="amplitude">World units at the start; fades to zero over the duration.</param>
        public void CameraShake( float frequency, float amplitude, float shakeDuration ) {
            var mainCamera = Camera.main;
            if( mainCamera != null && mainCamera.TryGetComponent<FollowCamera>( out var follow ) ) {
                follow.Shake( frequency, amplitude, shakeDuration );
            }
        }
    }
}