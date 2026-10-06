using UnityEngine;

namespace Hearthglade.Gameplay.Common.Extension {
    public static class TransformExtensions {
        public static void DestroyChildren(this Transform transform) {
            foreach( Transform child in transform ) {
                Object.Destroy( child.gameObject );
            }
        }
    }
}