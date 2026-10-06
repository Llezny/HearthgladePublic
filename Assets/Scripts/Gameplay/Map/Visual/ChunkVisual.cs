using UnityEngine;

namespace Hearthglade.Gameplay.Map.Visual
{
    public class ChunkVisual : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh runtimeMesh;
        private Texture2D runtimeSplatA;
        private Texture2D runtimeSplatB;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int SplatMapId  = Shader.PropertyToID("_SplatMap");
        private static readonly int SplatMapBId = Shader.PropertyToID("_SplatMapB");

        public void Apply(Mesh topMesh, Texture2D splatTextureA, Texture2D splatTextureB, Material sharedMaterial)
        {
            EnsureComponents();

            if (runtimeMesh != null) Destroy(runtimeMesh);
            if (runtimeSplatA != null) Destroy(runtimeSplatA);
            if (runtimeSplatB != null) Destroy(runtimeSplatB);

            runtimeMesh = topMesh;
            runtimeSplatA = splatTextureA;
            runtimeSplatB = splatTextureB;

            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = sharedMaterial;

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(SplatMapId,  runtimeSplatA);
            propertyBlock.SetTexture(SplatMapBId, runtimeSplatB);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
            if (runtimeSplatA != null) Destroy(runtimeSplatA);
            if (runtimeSplatB != null) Destroy(runtimeSplatB);
        }
    }
}
