using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// Paints a part in the colours of an appearance: a copy of the mesh whose first UV set points every face of a recoloured role at
    /// the chosen cell of the colour sheet. One mesh and one material per renderer, so batching is not touched.
    /// </summary>
    public static class CharacterRecolor
    {
        private const int RoleSlots = 16;

        /// <summary>The part's mesh itself when nothing of it is recoloured, otherwise a copy that the caller owns (and destroys).</summary>
        public static Mesh Apply(Mesh source, CharacterAppearanceSO.RoleColor[] colors)
        {
            if (source == null || colors == null || colors.Length == 0)
            {
                return source;
            }
            if (!source.isReadable)
            {
                UnityEngine.Debug.LogWarning($"[CharacterRecolor] {source.name} is not readable, so it keeps its own colours", source);
                return source;
            }
            var cells = new int[RoleSlots];
            System.Array.Fill(cells, -1);
            foreach (var color in colors)
            {
                cells[(int)color.role] = color.paletteCell;
            }

            var uv = new List<Vector2>();
            var roles = new List<Vector2>();
            source.GetUVs(0, uv);
            source.GetUVs(1, roles);
            if (roles.Count != uv.Count)
            {
                UnityEngine.Debug.LogWarning($"[CharacterRecolor] {source.name} has no colour roles (re-export it with character_kit.py)", source);
                return source;
            }
            bool changed = false;
            for (int i = 0; i < uv.Count; i++)
            {
                int role = Mathf.Clamp(Mathf.FloorToInt(roles[i].x * RoleSlots), 0, RoleSlots - 1);
                if (cells[role] >= 0)
                {
                    uv[i] = CharacterPalette.CellUv(cells[role]);
                    changed = true;
                }
            }
            if (!changed)
            {
                return source;
            }
            var copy = Object.Instantiate(source);
            copy.name = source.name + " (recoloured)";
            copy.SetUVs(0, uv);
            return copy;
        }
    }
}
