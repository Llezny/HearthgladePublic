using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Block.Base;
using Hearthglade.Gameplay.Map.Visual;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>
    /// Everything the map needs to know about a block type without instantiating its prefab: terrain flags
    /// and the ground collider. Read once per prefab from the prefab asset.
    /// </summary>
    public sealed class BlockKind
    {
        public string PrefabName;
        public GameObject Prefab;
        public BlockSO Asset;

        public bool IsGround;
        public bool IsWalkable;
        public bool IsElevated;
        public byte Biome;

        public bool HasCollider;
        public Vector3 ColliderCenter;
        public Vector3 ColliderSize;

        public TerrainCell ToCell(bool isWall, byte patch = 0)
        {
            return new TerrainCell
            {
                Present = true,
                Ground = IsGround,
                Walkable = IsWalkable,
                Wall = isWall,
                Elevated = IsElevated,
                Biome = IsGround && patch != 0 ? (byte)(patch - 1) : Biome,
            };
        }
    }

    /// <summary>Finds and caches the <see cref="BlockKind"/> of block prefabs (by prefab or by name).</summary>
    public sealed class BlockCatalog
    {
        private readonly References references;
        private readonly Dictionary<string, BlockKind> byName = new();
        private readonly Dictionary<GameObject, BlockKind> byPrefab = new();

        public BlockCatalog(References references)
        {
            this.references = references;
        }

        public BlockKind Get(GameObject prefab)
        {
            if (prefab == null) return null;
            if (byPrefab.TryGetValue(prefab, out var kind)) return kind;
            kind = Create(prefab);
            byPrefab[prefab] = kind;
            byName[prefab.name] = kind;
            return kind;
        }

        /// <summary>By prefab name, as saved in BlockModel.prefabName. Null when no such prefab is registered.</summary>
        public BlockKind Get(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            if (byName.TryGetValue(prefabName, out var kind)) return kind;
            return references.TryGetGameObject(prefabName, out var prefab) ? Get(prefab) : null;
        }

        private static BlockKind Create(GameObject prefab)
        {
            var block = prefab.GetComponent<Block>();
            var asset = block != null ? block.blockSO : null;
            if (asset == null)
            {
                UnityEngine.Debug.LogError($"Block prefab {prefab.name} has no Block component with a BlockSO", prefab);
            }

            var kind = new BlockKind
            {
                PrefabName = prefab.name,
                Prefab = prefab,
                Asset = asset,
                IsGround = asset != null && asset.IsGround,
                IsWalkable = asset != null && asset.IsWalkable,
                IsElevated = asset != null && asset.IsElevated,
                Biome = (byte)BiomeResolver.Resolve(asset),
            };

            var collider = prefab.GetComponent<BoxCollider>();
            if (collider != null)
            {
                kind.HasCollider = true;
                kind.ColliderCenter = collider.center;
                kind.ColliderSize = collider.size;
            }

            return kind;
        }
    }
}
