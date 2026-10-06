using UnityEngine;

namespace Hearthglade.Gameplay.Characters
{
    public enum CharacterSlot
    {
        Head,
        // The body: shirt or dress, arms, hands and the trousers, so a torso piece decides the legs too.
        Torso,
        Shoes,
    }

    /// <summary>
    /// One swappable piece of a character: a skinned mesh made for the shared skeleton (Tools/Agent Tools/Blender/character_kit.py). The mesh
    /// lists its bones by name, so the piece fits any character that has the skeleton, whatever the order of its own bone array.
    /// </summary>
    [CreateAssetMenu(menuName = "Hearthglade/Characters/Part", fileName = "CharacterPart")]
    public class CharacterPartSO : ScriptableObject
    {
        [SerializeField] CharacterSlot slot;
        [SerializeField] Mesh mesh;
        [SerializeField] string[] boneNames;
        [SerializeField] string rootBoneName;

        public CharacterSlot Slot => slot;
        public Mesh Mesh => mesh;
        public string[] BoneNames => boneNames;
        public string RootBoneName => rootBoneName;
    }
}
