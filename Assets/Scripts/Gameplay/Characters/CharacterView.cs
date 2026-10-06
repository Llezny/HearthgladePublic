using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// The clothes of a character on the shared skeleton: one skinned mesh renderer per slot, whose mesh and bones are swapped for
    /// those of the chosen part, painted in the colours of the appearance. Works in the editor too (the prefab of a trader is baked
    /// with it) and at runtime (a future appearance editor of the player calls the same methods).
    /// </summary>
    public class CharacterView : MonoBehaviour
    {
        [SerializeField] Transform skeleton;
        [SerializeField] SkinnedMeshRenderer headRenderer;
        [SerializeField] SkinnedMeshRenderer torsoRenderer;
        [SerializeField] SkinnedMeshRenderer shoesRenderer;
        [SerializeField] CharacterAppearanceSO appearance;

        // The recoloured copies of meshes made at runtime; the view destroys what it made.
        private readonly Mesh[] ownMeshes = new Mesh[3];

        public CharacterAppearanceSO Appearance => appearance;

        public void Apply(CharacterAppearanceSO newAppearance)
        {
            appearance = newAppearance;
            var bones = FindBones();
            foreach (CharacterSlot slot in System.Enum.GetValues(typeof(CharacterSlot)))
            {
                Wear(slot, newAppearance.Get(slot), bones);
            }
        }

        /// <summary>Changes one part, in the colours of the current appearance.</summary>
        public void Wear(CharacterPartSO part)
        {
            Wear(part.Slot, part, FindBones());
        }

        private void Wear(CharacterSlot slot, CharacterPartSO part, Dictionary<string, Transform> bones)
        {
            var target = RendererOf(slot);
            if (target == null || part == null)
            {
                return;
            }
            var transforms = new Transform[part.BoneNames.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                if (!bones.TryGetValue(part.BoneNames[i], out transforms[i]))
                {
                    UnityEngine.Debug.LogError($"[CharacterView] {name}: the skeleton has no bone {part.BoneNames[i]} needed by {part.name}", this);
                    return;
                }
            }
            var colors = appearance != null ? appearance.Colors : null;
            var mesh = CharacterRecolor.Apply(part.Mesh, colors);
            Release((int)slot);
            if (mesh != part.Mesh)
            {
                ownMeshes[(int)slot] = mesh;
            }
            target.sharedMesh = mesh;
            target.bones = transforms;
            bones.TryGetValue(part.RootBoneName, out var root);
            target.rootBone = root;
            // The bounds of the mesh that was there before would clip a bigger piece (a skirt) while it moves.
            target.updateWhenOffscreen = true;
        }

        private void Release(int slot)
        {
            var mesh = ownMeshes[slot];
            ownMeshes[slot] = null;
            if (mesh == null)
            {
                return;
            }
#if UNITY_EDITOR
            // A mesh the editor saved as an asset (when a prefab is baked) is no longer ours to destroy.
            if (UnityEditor.EditorUtility.IsPersistent(mesh))
            {
                return;
            }
#endif
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }

        private void OnDestroy()
        {
            for (int slot = 0; slot < ownMeshes.Length; slot++)
            {
                Release(slot);
            }
        }

        private SkinnedMeshRenderer RendererOf(CharacterSlot slot)
        {
            return slot switch
            {
                CharacterSlot.Head => headRenderer,
                CharacterSlot.Torso => torsoRenderer,
                _ => shoesRenderer,
            };
        }

        private Dictionary<string, Transform> FindBones()
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in skeleton.GetComponentsInChildren<Transform>(true))
            {
                bones.TryAdd(bone.name, bone);
            }
            return bones;
        }
    }
}
