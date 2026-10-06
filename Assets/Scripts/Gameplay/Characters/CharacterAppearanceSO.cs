using System;
using UnityEngine;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// What a character wears: one part per slot, and the colours of the colour roles. A role that is not listed keeps the colour its
    /// part was made with. Traders and other NPCs are data, not new code.
    /// </summary>
    [CreateAssetMenu(menuName = "Hearthglade/Characters/Appearance", fileName = "CharacterAppearance")]
    public class CharacterAppearanceSO : ScriptableObject
    {
        [Serializable]
        public struct RoleColor
        {
            public CharacterColorRole role;
            [Tooltip("Cell of the colour sheet, row * 8 + column from the top left (see CharacterPalette).")]
            public int paletteCell;
        }

        [SerializeField] CharacterPartSO head;
        [SerializeField] CharacterPartSO torso;
        [SerializeField] CharacterPartSO shoes;
        [SerializeField] RoleColor[] colors = Array.Empty<RoleColor>();

        public CharacterPartSO Head => head;
        public CharacterPartSO Torso => torso;
        public CharacterPartSO Shoes => shoes;
        public RoleColor[] Colors => colors;

        public CharacterPartSO Get(CharacterSlot slot)
        {
            return slot switch
            {
                CharacterSlot.Head => head,
                CharacterSlot.Torso => torso,
                _ => shoes,
            };
        }
    }
}
