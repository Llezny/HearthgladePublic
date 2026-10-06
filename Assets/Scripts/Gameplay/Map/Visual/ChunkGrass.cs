using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthglade.Gameplay.Map.Visual
{
    public class ChunkGrass : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh runtimeMesh;

        // Also used for the cliff walls, which (unlike grass tufts) do receive shadows.
        public void Apply(Mesh grassMesh, Material sharedMaterial, bool receiveShadows = false)
        {
            EnsureComponents();
            meshRenderer.receiveShadows = receiveShadows;

            if (runtimeMesh != null) Destroy(runtimeMesh);
            runtimeMesh = grassMesh;

            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = sharedMaterial;
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
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                meshRenderer.staticShadowCaster = false;
                meshRenderer.allowOcclusionWhenDynamic = false;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
        }
    }
}
