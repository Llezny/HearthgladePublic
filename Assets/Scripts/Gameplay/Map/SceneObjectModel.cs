using Hearthglade.Core.World;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    [System.Serializable]
    public class SceneObjectModel {

        [System.NonSerialized]
        public GameObject gameObject;

        // True while gameObject is a live instance for this model. Lets Block.ShowSceneObjects be
        // idempotent, so re-enabling a chunk (or re-entering a map) never spawns a second copy.
        [System.NonSerialized]
        public bool isSpawned;

        public string name;
        public Vector3 pos, rot, sca;
        public object additionalData;

        public SceneObjectModel(){}
        public SceneObjectModel( Transform obj ) {
            this.gameObject = obj.gameObject;
            this.isSpawned = true;
            this.name = obj.name;
            this.pos = obj.position;
            this.rot = obj.rotation.eulerAngles;
            this.sca = obj.localScale;
            if( obj.GetComponent(typeof(ISceneObjectWithData)) is ISceneObjectWithData component ) {
                this.additionalData = component?.CaptureState();
            }
        }

        public SceneObjectModel( Vector3 pos, Vector3 rot, Vector3 sca, string prefabName, object additionalData = null ) {
            this.name = prefabName;
            this.pos = pos;
            this.rot = rot;
            this.sca = sca;
            this.additionalData = additionalData;
        }

        // The key of the object in its block. Stable between launches (unlike HashCode.Combine), see SceneObjectKey.
        public override int GetHashCode() {
            return SceneObjectKey.Compute( name, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, sca.x, sca.y, sca.z );
        }

    }
}