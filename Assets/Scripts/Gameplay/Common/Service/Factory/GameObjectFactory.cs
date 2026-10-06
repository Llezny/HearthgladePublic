using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service.Factory {
    public class GameObjectFactory {

        private readonly IObjectResolver container;

        public GameObjectFactory( IObjectResolver container ) {
            this.container = container;
        }

        public GameObject Get( GameObject prefab ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab );
            container.InjectGameObject( obj );
            return obj;
        }

        public GameObject Get( GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent = null ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab, position, rotation, scale, parent );
            container.InjectGameObject( obj );
            return obj;
        }
        
        public GameObject Get( GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab, position, rotation, parent );
            container.InjectGameObject( obj );

            return obj;
        }
    }
}
