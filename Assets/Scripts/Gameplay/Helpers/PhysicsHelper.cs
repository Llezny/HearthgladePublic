using UnityEngine;

namespace Hearthglade.Gameplay.Helpers
{
    public class PhysicsHelper {
        public static bool HasAnyCollision( GameObject gameObject ) {
            return Physics.OverlapSphere( 
                gameObject.transform.position,
                gameObject.GetComponent<Collider>().bounds.size.x / 2,
                LayerMask.GetMask("Clickable") ).Length > 1;
        }
    }
}
