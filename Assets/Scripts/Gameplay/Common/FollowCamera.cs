using UnityEngine;

namespace Hearthglade.Gameplay.Common {
    public class FollowCamera : MonoBehaviour {
        public Transform target;
        public Vector3 offset = new Vector3(-6, 6, -10);

        // Portrait: the visible width is what matters for gameplay, so derive the orthographic
        // size from it instead of using a fixed vertical size that would leave a sliver of world.
        [SerializeField] private float horizontalHalfWidth = 1.4f;

        private Camera cam;

        private void Awake() {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate() {
            transform.position = target.position + offset;
            if ( cam != null && cam.orthographic ) {
                cam.orthographicSize = horizontalHalfWidth / cam.aspect;
            }
        }
    }
}
