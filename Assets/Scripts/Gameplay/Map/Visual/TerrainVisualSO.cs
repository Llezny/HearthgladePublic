using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Map.Visual
{
    [CreateAssetMenu(fileName = "TerrainVisualSO", menuName = "ScriptableObjects/Terrain Visual")]
    public class TerrainVisualSO : ScriptableObject
    {
        [Serializable]
        public struct BiomeGrassEntry
        {
            public BiomeId biome;
            [Range(0, 32)] public int tuftsPerTile;

            [Tooltip("Multiplies the ground texture's average color to get this biome's grass color. Independent of the ground's own _BiomeTintN, so retinting the ground does not change the grass. Alpha 0 (unset) = white.")]
            public Color tintMultiplier;
        }

        [Tooltip("Material using the TerrainSplat shader. Shared across all chunks; per-chunk splat is set via MaterialPropertyBlock.")]
        public Material terrainMaterial;

        [Tooltip("Material for the cliff/skirt mesh around island edges and lake holes. Uses CliffRock shader.")]
        public Material cliffMaterial;

        [Tooltip("How many blocks around each splat texel are averaged into its biome weight. 0 = old sharp one-block blend, higher = biomes fade into each other over a longer distance. Costs O(radius^2) extra per-texel work at chunk build time.")]
        [Range(0, 4)] public int biomeBlendRadius = 2;

        [Tooltip("Vertical offset for the visual mesh on top of the block layer.")]
        public float topYOffset = 0.4f;

        [Tooltip("How far elevated blocks (IsElevated=true) sit above the base topYOffset. Creates grass-lip on grass↔sand/dirt seams.")]
        public float elevatedYRise = 0.2f;

        [Tooltip("How deep cliffs/skirt walls extend below the top mesh.")]
        public float cliffDepth = 2.0f;

        [Tooltip("Grass overhang: how far the top mesh extends beyond cliff edges (creates the diorama turf-lip look). 0 = disabled.")]
        public float overhang = 0.08f;

        [Tooltip("Vertical drop of the overhang outer edge below top surface (slight bevel).")]
        public float overhangDrop = 0.03f;

        [Tooltip("Multiplier applied to overhang & overhangDrop on internal grass↔sand/dirt seams (i.e. neighbor exists but is lower). Map edges and lake borders keep full overhang.")]
        [Range(0f, 1f)] public float innerOverhangScale = 0.5f;

        [Tooltip("Enabled = visual mesh is generated and rendered per chunk.")]
        public bool enableVisual = true;

        [Tooltip("Enabled = generate skirt/cliff mesh around island edges and lakes.")]
        public bool enableCliffs = true;

        [Header("Grass Tufts")]
        [Tooltip("Enabled = scatter camera-facing grass tufts on supported biomes (one merged mesh per chunk).")]
        public bool enableGrass = true;

        [Tooltip("Material using the GrassTufts shader (alpha-cutout card; the clump silhouette comes from the material's clump texture, tinted per biome via vertex color). Shared across all chunks.")]
        public Material grassMaterial;

        [Tooltip("Tuft height in world units (top vertex Y offset from ground).")]
        public float grassHeight = 0.12f;

        [Tooltip("Tuft width in world units (base spread).")]
        public float grassWidth = 0.08f;

        [Tooltip("World-space direction the main camera looks (its Transform.forward). Grass cards stand vertically and yaw to face this direction's horizontal component - the gameplay camera never orbits, so this only needs to match Game.unity's MainCamera rotation once. Only X/Z matter.")]
        public Vector3 grassCameraForward = new Vector3(0.4607f, -0.4454f, 0.7678f);

        [Tooltip("Number of clump variants laid out side by side in the grass material's texture (equal-width columns). Each tuft picks one at random and may be mirrored.")]
        [Range(1, 8)] public int grassVariants = 3;

        [Tooltip("Grass is meshed in square blocks of about this many tiles per side, so the camera culls it per block instead of per whole chunk. Smaller = less off-screen grass drawn but more draw calls and meshes.")]
        [Range(2, 13)] public int grassCullCellSize = 4;

        [Tooltip("Blade root brightness as a fraction of the ground color. 1 = the root is exactly the ground color (no gradient).")]
        [Range(0.4f, 1.2f)] public float grassBaseDarken = 1f;

        [Tooltip("Blade tip brightness as a multiple of the ground color. 1 = the tip is exactly the ground color.")]
        [Range(0.5f, 1.5f)] public float grassTipBrighten = 1f;

        [Tooltip("Shrinks the area inside each tile where tufts may spawn (1 = full tile, 0.7 = keep away from edges to avoid overhang z-fight).")]
        [Range(0.4f, 1f)] public float grassTileMargin = 0.7f;

        [Tooltip("Perlin seed for the per-tile density mask. Change to reshuffle patches without touching map seed.")]
        public int grassNoiseSeed = 1337;

        [Tooltip("Spatial frequency of the density Perlin (multiplied by tile grid coords). Lower = larger smooth patches, higher = smaller speckles.")]
        public float grassNoiseScale = 0.18f;

        [Tooltip("Tiles with normalized noise < threshold have ZERO tufts (bare patches). 0 = no bare patches, 0.5 = roughly half the area bare.")]
        [Range(0f, 0.95f)] public float grassNoiseThreshold = 0.25f;

        [Tooltip("Curve applied to the noise above threshold before scaling to tuftsPerTile. 1 = linear, >1 = sharper edges of patches, <1 = softer.")]
        [Range(0.25f, 4f)] public float grassNoiseExponent = 1.8f;

        // Tint (shadowed blade base) and TipLighten (sunlit tip) are NOT authored here - GrassGroundColor
        // derives them automatically every time chunk params are built, straight from this biome's actual
        // ground: average color of its TerrainSplat _BiomeTexN PNG x its _BiomeTintN on terrainMaterial.
        // tipLighten = ground color, tint = ground color x (0.55, 0.78, 0.65) for a richer/darker shadowed
        // base. This is deliberate: hand-copied color constants drifted out of sync with the ground once
        // already (Forest's tint ended up nearly identical to Grass's after an unrelated tweak, 2026-09-25)
        // - retune the ground texture/tint on terrainMaterial and grass follows automatically, nothing to
        // keep in sync here. Ground textures need Read/Write enabled for this to work (GrassGroundColor
        // falls back to a flat color and logs a warning otherwise). See GrassGroundColor.cs.
        [Tooltip("Per-biome configuration: which biomes get grass and how dense. Color is derived automatically from the ground (see GrassGroundColor.cs), not set here.")]
        public List<BiomeGrassEntry> grassBiomes = new List<BiomeGrassEntry>
        {
            new BiomeGrassEntry { biome = BiomeId.Grass,  tuftsPerTile = 3 },
            new BiomeGrassEntry { biome = BiomeId.Forest, tuftsPerTile = 3 },
            new BiomeGrassEntry { biome = BiomeId.Swamp,  tuftsPerTile = 2 },
            new BiomeGrassEntry { biome = BiomeId.Taiga,  tuftsPerTile = 3 },
            new BiomeGrassEntry { biome = BiomeId.Tundra, tuftsPerTile = 1 },
        };

        private static TerrainVisualSO cached;

        public static TerrainVisualSO Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<TerrainVisualSO>("ScriptableObjects/Terrain/TerrainVisualSO");
            if (cached == null)
            {
                UnityEngine.Debug.LogWarning("[TerrainVisualSO] Not found at Resources/ScriptableObjects/Terrain/TerrainVisualSO. Chunk visual layer disabled.");
            }
            return cached;
        }
    }
}
