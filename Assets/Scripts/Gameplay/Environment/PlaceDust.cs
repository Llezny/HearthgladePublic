using Lean.Pool;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    /// <summary>
    /// A short puff of dust kicked up when the player places a building piece. One pooled particle burst
    /// spread over the piece's footprint; despawns itself once the last puff has faded.
    /// Prefab built by LuakszTools/Building/Build place dust, loaded from Resources so no scene wiring is needed.
    /// </summary>
    public class PlaceDust : MonoBehaviour {

        private const string ResourcePath = "VFX/PlaceDust";
        private const float DespawnSeconds = 1.5f;

        private static GameObject prefab;

        [ SerializeField ] private ParticleSystem particles = null;

        /// <param name="footprint">Ground size in world units, in the piece's own X/Z axes.</param>
        public static void Spawn( Vector3 position, Quaternion rotation, Vector2 footprint ) {
            if( prefab == null ) {
                prefab = Resources.Load<GameObject>( ResourcePath );
            }
            if( prefab == null ) {
                return;
            }
            var dust = LeanPool.Spawn( prefab, position, rotation ).GetComponent<PlaceDust>();
            dust.Play( footprint );
        }

        private void Play( Vector2 footprint ) {
            var shape = particles.shape;
            shape.scale = new Vector3( footprint.x, footprint.y, 1f );
            particles.Clear( true );
            particles.Play( true );
            LeanPool.Despawn( gameObject, DespawnSeconds );
        }
    }
}
