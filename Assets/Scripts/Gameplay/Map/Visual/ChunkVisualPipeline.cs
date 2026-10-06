using Hearthglade.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthglade.Gameplay.Map.Visual
{
    /// <summary>Mesh data as Unity arrays, prepared on a worker thread so the main thread only uploads it.</summary>
    public sealed class MeshArrays
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] Uvs;
        public Color32[] Colors; // null when the mesh has no vertex colors
        public int[] Triangles;
        public bool HasBounds;
        public Bounds Bounds;

        public static MeshArrays From(MeshBuffers buffers, bool withUvs = true)
        {
            if (buffers == null) return null;
            int count = buffers.VertexCount;
            var arrays = new MeshArrays
            {
                Vertices = new Vector3[count],
                Normals = new Vector3[count],
                Uvs = withUvs ? new Vector2[count] : null,
                Triangles = buffers.Triangles.ToArray(),
            };
            for (int i = 0; i < count; i++)
            {
                var v = buffers.Vertices[i];
                var n = buffers.Normals[i];
                arrays.Vertices[i] = new Vector3(v.X, v.Y, v.Z);
                arrays.Normals[i] = new Vector3(n.X, n.Y, n.Z);
                if (withUvs)
                {
                    var uv = buffers.Uvs[i];
                    arrays.Uvs[i] = new Vector2(uv.X, uv.Y);
                }
            }
            if (buffers.Colors.Count == count && count > 0)
            {
                arrays.Colors = new Color32[count];
                for (int i = 0; i < count; i++)
                {
                    var c = buffers.Colors[i];
                    arrays.Colors[i] = new Color32(c.R, c.G, c.B, c.A);
                }
            }
            return arrays;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(Vertices);
            if (Uvs != null) mesh.SetUVs(0, Uvs);
            mesh.SetNormals(Normals);
            if (Colors != null) mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            if (HasBounds) mesh.bounds = Bounds;
            else mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Result of a chunk visual computation, ready to be applied on the main thread.</summary>
    public sealed class ChunkVisualArrays
    {
        public int ChunkX, ChunkY;
        public bool HasTerrain;
        public int SplatSize;
        public byte[] SplatA, SplatB;
        public MeshArrays Top, Cliffs;
        public MeshArrays[] Grass;
    }

    public static class ChunkVisualPipeline
    {
        public static TerrainVisualParams CreateParams(TerrainVisualSO config)
        {
            // Vertex colors cap at 1, so the ground's brightness multiplier travels to the grass shader as a
            // global instead of being baked into the grass tint.
            float groundBrightness = config.terrainMaterial != null && config.terrainMaterial.HasProperty("_Brightness")
                ? config.terrainMaterial.GetFloat("_Brightness")
                : 1f;
            Shader.SetGlobalFloat("_GrassGroundBrightness", groundBrightness);

            var p = new TerrainVisualParams
            {
                ChunkSize = ChunkManager.ChunkSize,
                TileSize = MapGenerator.TILE_X_OFFSET,
                BiomeBlendRadius = config.biomeBlendRadius,
                BaseTopY = config.topYOffset,
                ElevatedRise = config.elevatedYRise,
                CliffDepth = config.cliffDepth,
                Overhang = config.overhang,
                OverhangDrop = config.overhangDrop,
                InnerOverhangScale = config.innerOverhangScale,
                EnableCliffs = config.enableCliffs && config.cliffMaterial != null,
                EnableGrass = config.enableGrass && config.grassMaterial != null && config.grassBiomes != null && config.grassBiomes.Count > 0,
                GrassHeight = config.grassHeight,
                GrassWidth = config.grassWidth,
                GrassTileMargin = config.grassTileMargin,
                GrassNoiseSeed = config.grassNoiseSeed,
                GrassNoiseScale = config.grassNoiseScale,
                GrassNoiseThreshold = config.grassNoiseThreshold,
                GrassNoiseExponent = config.grassNoiseExponent,
                GrassCameraForward = new Float3(config.grassCameraForward.x, config.grassCameraForward.y, config.grassCameraForward.z),
                GrassVariants = config.grassVariants,
                GrassCullCellSize = config.grassCullCellSize,
                GrassBaseDarken = config.grassBaseDarken,
                GrassTipBrighten = config.grassTipBrighten,
            };

            int biomeCount = System.Enum.GetValues(typeof(BiomeId)).Length;
            p.GrassByBiome = new GrassStyle[biomeCount];
            if (config.grassBiomes != null)
            {
                foreach (var entry in config.grassBiomes)
                {
                    int biome = (int)entry.biome;
                    if (biome < 0 || biome >= biomeCount || entry.tuftsPerTile <= 0) continue;

                    // Tint/tipLighten come from the ground texture's average color (GrassGroundColor) times the
                    // entry's own multiplier - not the ground's _BiomeTintN, so retinting the ground leaves the
                    // grass alone. The fallback only fires for a biome GrassGroundColor doesn't recognise or a
                    // ground texture that isn't Read/Write enabled.
                    var multiplier = entry.tintMultiplier.a <= 0f ? Color.white : entry.tintMultiplier;
                    if (!GrassGroundColor.TryDerive(config.terrainMaterial, entry.biome, multiplier, out var tint, out var tipLighten))
                    {
                        tint = new Color(0.3f, 0.5f, 0.25f, 1f);
                        tipLighten = new Color(0.5f, 0.7f, 0.4f, 1f);
                    }

                    p.GrassByBiome[biome] = new GrassStyle
                    {
                        TuftsPerTile = entry.tuftsPerTile,
                        Tint = new Float3(tint.r, tint.g, tint.b),
                        TipLighten = new Float3(tipLighten.r, tipLighten.g, tipLighten.b),
                    };
                }
            }
            return p;
        }

        /// <summary>Pure computation plus array conversion: safe to run on a worker thread.</summary>
        public static ChunkVisualArrays Compute(TerrainWindow window, int chunkX, int chunkY, TerrainVisualParams p)
        {
            var data = ChunkVisualComputer.Compute(window, chunkX, chunkY, p);
            var result = new ChunkVisualArrays { ChunkX = chunkX, ChunkY = chunkY, HasTerrain = data.HasTerrain };
            if (!data.HasTerrain) return result;

            result.SplatSize = data.Splat.TextureSize;
            result.SplatA = data.Splat.SplatA;
            result.SplatB = data.Splat.SplatB;
            result.Top = MeshArrays.From(data.Top);
            result.Cliffs = MeshArrays.From(data.Cliffs);
            if (data.Grass != null)
            {
                result.Grass = new MeshArrays[data.Grass.Length];
                for (int i = 0; i < data.Grass.Length; i++)
                {
                    var cell = data.Grass[i];
                    var arrays = MeshArrays.From(cell.Mesh);
                    arrays.HasBounds = true;
                    arrays.Bounds = new Bounds(
                        new Vector3(cell.BoundsCenter.X, cell.BoundsCenter.Y, cell.BoundsCenter.Z),
                        new Vector3(cell.BoundsSize.X, cell.BoundsSize.Y, cell.BoundsSize.Z));
                    result.Grass[i] = arrays;
                }
            }
            return result;
        }

        /// <summary>Creates the visual GameObjects under the chunk. Main thread only.</summary>
        public static void Apply(Chunk chunk, ChunkVisualArrays arrays, TerrainVisualSO config)
        {
            Discard(chunk);
            if (!arrays.HasTerrain) return;

            var parent = chunk.chunkGameObject.transform;
            int size = arrays.SplatSize;
            var splatA = CreateSplat(arrays.SplatA, size, $"SplatA_{chunk.chunkId}");
            var splatB = CreateSplat(arrays.SplatB, size, $"SplatB_{chunk.chunkId}");

            chunk.topVisual = new GameObject("_VisualTop");
            chunk.topVisual.transform.SetParent(parent, false);
            chunk.topVisual.AddComponent<ChunkVisual>().Apply(
                arrays.Top.ToMesh($"ChunkVisual_{chunk.chunkId}"), splatA, splatB, config.terrainMaterial);

            if (arrays.Cliffs != null)
            {
                chunk.cliffVisual = new GameObject("_VisualCliffs");
                chunk.cliffVisual.transform.SetParent(parent, false);
                chunk.cliffVisual.AddComponent<ChunkGrass>().Apply(
                    arrays.Cliffs.ToMesh($"ChunkCliffs_{chunk.chunkId}"), config.cliffMaterial, receiveShadows: true);
            }

            if (arrays.Grass != null)
            {
                chunk.grassVisual = new GameObject("_VisualGrass");
                chunk.grassVisual.transform.SetParent(parent, false);
                // One child per block of tiles, each with its own bounds so the camera culls them separately.
                for (int i = 0; i < arrays.Grass.Length; i++)
                {
                    var cell = new GameObject($"_GrassCell{i}");
                    cell.transform.SetParent(chunk.grassVisual.transform, false);
                    cell.AddComponent<ChunkGrass>().Apply(
                        arrays.Grass[i].ToMesh($"ChunkGrass_{chunk.chunkId}_{i}"), config.grassMaterial, receiveShadows: false);
                }
            }
        }

        /// <summary>Destroys the chunk's visual objects (their meshes and textures go with them).</summary>
        public static void Discard(Chunk chunk)
        {
            DestroyVisual(ref chunk.topVisual);
            DestroyVisual(ref chunk.cliffVisual);
            DestroyVisual(ref chunk.grassVisual);
        }

        private static void DestroyVisual(ref GameObject visual)
        {
            if (visual != null) Object.Destroy(visual);
            visual = null;
        }

        // Linear (non-sRGB) data texture: the shader reads biome weights from it, not colors.
        private static Texture2D CreateSplat(byte[] rgba, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                name = name,
            };
            texture.SetPixelData(rgba, 0);
            texture.Apply(false, true);
            return texture;
        }
    }
}
