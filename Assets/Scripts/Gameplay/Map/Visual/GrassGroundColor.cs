using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Map.Visual
{
    /// <summary>
    /// Derives a grass biome's color straight from the ground it is rendered on, so retinting a biome's ground
    /// retints its grass with it instead of silently drifting out of sync (this happened to Forest, 2026-09-25).
    ///
    /// groundColor = average color of the biome's _BiomeTexN PNG x its _BiomeTintN on the terrain material x
    /// the biome's own optional grass multiplier (TerrainVisualSO.BiomeGrassEntry, white = none). Both tint
    /// and tipLighten are exactly groundColor: the GrassTufts shader and GrassBuilder only move away from it
    /// by the sliders the user sets, so with everything neutral grass and ground read as the same color.
    /// </summary>
    public static class GrassGroundColor
    {
        private struct Source
        {
            public string TexProperty;
            public string TintProperty;
        }

        // Which _BiomeTexN texture TerrainSplat.shader actually samples for each biome. Grass and
        // Forest share one texture (tex0) and differ only by tint, same for Dirt/Swamp (tex1) - see the
        // "tint-blend family" comment in TerrainSplat.shader. Stone/Tundra look like they'd share too (same
        // source PNG) but the shader gives Tundra its own _BiomeTex7 slot/UV scale, so it's listed
        // separately here even though it happens to read the same file today.
        private static readonly Dictionary<BiomeId, Source> Sources = new Dictionary<BiomeId, Source> {
            { BiomeId.Grass,  new Source { TexProperty = "_BiomeTex0", TintProperty = "_BiomeTint0" } },
            { BiomeId.Dirt,   new Source { TexProperty = "_BiomeTex1", TintProperty = "_BiomeTint1" } },
            { BiomeId.Sand,   new Source { TexProperty = "_BiomeTex2", TintProperty = "_BiomeTint2" } },
            { BiomeId.Stone,  new Source { TexProperty = "_BiomeTex3", TintProperty = "_BiomeTint3" } },
            { BiomeId.Taiga,  new Source { TexProperty = "_BiomeTex4", TintProperty = "_BiomeTint4" } },
            { BiomeId.Forest, new Source { TexProperty = "_BiomeTex0", TintProperty = "_BiomeTint5" } },
            { BiomeId.Swamp,  new Source { TexProperty = "_BiomeTex1", TintProperty = "_BiomeTint6" } },
            { BiomeId.Tundra, new Source { TexProperty = "_BiomeTex7", TintProperty = "_BiomeTint7" } },
        };

        // Keyed by the actual Texture instance so Grass/Forest (sharing one PNG) only pay for one average.
        // Ground textures never change at runtime, so this never needs invalidating.
        private static readonly Dictionary<Texture, Color> averageCache = new Dictionary<Texture, Color>();

        /// <summary>False when this biome isn't mapped, the material is missing the property, or the
        /// texture isn't Read/Write enabled - callers should fall back to a hardcoded color in that case.</summary>
        public static bool TryDerive(Material terrainMaterial, BiomeId biome, Color tintMultiplier, out Color tint, out Color tipLighten)
        {
            tint = default;
            tipLighten = default;

            if (terrainMaterial == null || !Sources.TryGetValue(biome, out var source))
            {
                return false;
            }
            if (!terrainMaterial.HasProperty(source.TexProperty) || !terrainMaterial.HasProperty(source.TintProperty))
            {
                return false;
            }
            var tex = terrainMaterial.GetTexture(source.TexProperty) as Texture2D;
            if (tex == null)
            {
                return false;
            }

            if (!averageCache.TryGetValue(tex, out var texAverage))
            {
                texAverage = AverageColor(tex);
                averageCache[tex] = texAverage;
            }

            // TerrainSplat multiplies the texture (sampled as linear) by the tint property (converted to linear
            // by Unity), so the product has to be formed in linear space and converted back to sRGB for the
            // vertex color - the GrassTufts shader converts it to linear again. Doing the math in raw sRGB
            // instead made the grass far brighter than the ground it grows on.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var groundTint = terrainMaterial.GetColor(source.TintProperty);
            if (linear)
            {
                groundTint = groundTint.linear;
            }
            var ground = new Color(
                texAverage.r * groundTint.r * tintMultiplier.r,
                texAverage.g * groundTint.g * tintMultiplier.g,
                texAverage.b * groundTint.b * tintMultiplier.b,
                1f);
            if (linear)
            {
                ground = ground.gamma;
            }
            ground = new Color(Mathf.Clamp01(ground.r), Mathf.Clamp01(ground.g), Mathf.Clamp01(ground.b), 1f);

            tint = ground;
            tipLighten = ground;
            return true;
        }

        // Mean of the texture as the terrain shader sees it (linear when the project is, since the texture is
        // an sRGB one). Done on mip 0 once per texture (cached), not the smallest mip: averaging in gamma space
        // first and converting after would come out darker than the true linear mean.
        private static Color AverageColor(Texture2D tex)
        {
            if (!tex.isReadable)
            {
                UnityEngine.Debug.LogWarning($"[GrassGroundColor] '{tex.name}' isn't Read/Write enabled - can't sample its average color for grass tinting. Falling back to a flat color for its biome.");
                return Color.white;
            }

            var pixels = tex.GetPixels32();
            if (pixels.Length == 0)
            {
                return Color.white;
            }

            var lut = new float[256];
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < 256; i++)
            {
                lut[i] = linear ? Mathf.GammaToLinearSpace(i / 255f) : i / 255f;
            }

            double r = 0, g = 0, b = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                r += lut[pixels[i].r];
                g += lut[pixels[i].g];
                b += lut[pixels[i].b];
            }
            return new Color((float)(r / pixels.Length), (float)(g / pixels.Length), (float)(b / pixels.Length), 1f);
        }
    }
}
