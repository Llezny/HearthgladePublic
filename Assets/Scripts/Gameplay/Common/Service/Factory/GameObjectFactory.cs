using System.Collections.Generic;
using PrefabLightmapBaker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service.Factory {
    public class GameObjectFactory {

        private readonly IObjectResolver container;
        private readonly PrefabLightmaps prefabLightmaps;

        // prefab -> index in prefabLightmaps.Data. Looked up for every spawned object, which used to be a
        // linear List.IndexOf per block (thousands of times while a map is generated).
        private Dictionary<GameObject, int> lightmapIndexByPrefab;
        
        public GameObjectFactory( IObjectResolver container, PrefabLightmaps prefabLightmaps ) {
            this.container = container;
            this.prefabLightmaps = prefabLightmaps;
        }
        
        private void ApplyLightmap( GameObject prefab, GameObject obj ) {
            if( lightmapIndexByPrefab == null ) {
                lightmapIndexByPrefab = new Dictionary<GameObject, int>();
                var prefabs = prefabLightmaps.Data.prefabs;
                for( int i = 0; i < prefabs.Count; i++ ) {
                    if( prefabs[ i ] != null ) {
                        lightmapIndexByPrefab.TryAdd( prefabs[ i ], i );
                    }
                }
            }
            if( !lightmapIndexByPrefab.TryGetValue( prefab, out var idx ) ) {
                return;
            }
            var rendererInfo = prefabLightmaps.Data.m_RendererInfo[ idx ];
            var renderer = obj.GetComponent<Renderer>();
            if( renderer == null ) {
                return;
            }
            renderer.lightmapIndex = rendererInfo.lightmapIndex;
            renderer.lightmapScaleOffset = rendererInfo.lightmapOffsetScale;
        }

        public GameObject Get( GameObject prefab ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab );
            container.InjectGameObject( obj );
            ApplyLightmap( prefab, obj );
            return obj;
        }

        public GameObject Get( GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent = null ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab, position, rotation, scale, parent );
            container.InjectGameObject( obj );
            ApplyLightmap( prefab, obj );
            return obj;
        }
        
        public GameObject Get( GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null ) {
            var obj = Lean.Pool.LeanPool.Spawn( prefab, position, rotation, parent );
            container.InjectGameObject( obj );
            ApplyLightmap( prefab, obj );

            return obj;
        }
    }
}