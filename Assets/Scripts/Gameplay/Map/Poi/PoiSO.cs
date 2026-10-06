using System;
using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Map.Visual;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>
    /// A point of interest that the world generator places on the map, for example an abandoned camp: where it may stand,
    /// which prefabs it is made of and what its chests hold. A cell is 0.3675 units wide.
    /// </summary>
    [CreateAssetMenu(fileName = "Poi", menuName = "ScriptableObjects/Map/Poi")]
    public class PoiSO : ScriptableObject
    {
        [Serializable]
        public class Piece
        {
            public GameObject prefab;
            [Tooltip("Cells from the centre of the site (x, z), before the site is turned.")]
            public Vector2 offset;
            [Tooltip("Degrees added to the rotation of the site and of the prefab.")]
            [Range(0f, 360f)] public float rotation;
        }

        [Serializable]
        public class LootData
        {
            public ItemSO item;
            [Min(1)] public int min = 1;
            [Min(1)] public int max = 1;
            [Min(0f)] public float weight = 1f;
            [Tooltip("Drops only from this tier up (the farther from the start, the higher the tier).")]
            [Min(0)] public int minTier = 0;
        }

        [Serializable]
        public class PathStripe
        {
            [Tooltip("Cells from the centre of the site (x, z), before the site is turned.")]
            public Vector2 from;
            [Tooltip("Same as From for a round plaza.")]
            public Vector2 to;
            [Tooltip("Cells within this distance of the stripe are painted.")]
            [Min(0.5f)] public float halfWidth = 1f;
            [Tooltip("The ground looks like this biome.")]
            public BiomeId look = BiomeId.Dirt;
        }

        public List<Piece> pieces = new List<Piece>();

        [Header("Paths (painted on the ground, no objects)")]
        public List<PathStripe> paths = new List<PathStripe>();

        [Header("Fixed place (a hand-made site such as a harbour)")]
        [Tooltip("One site at a fixed place and rotation instead of a random pick. Count, biomes, distances, spacing and water below are ignored; the centre must be ground connected to the start.")]
        public bool anchored;
        [Tooltip("Anchored: cells from the player's start to the centre (x east, z north).")]
        public Vector2Int anchorOffset;
        [Tooltip("Anchored: degrees around the vertical axis.")]
        [Range(0f, 360f)] public float anchorRotation;

        [Header("Where it stands")]
        [Tooltip("Biomes the centre may lie in. Empty = any ground.")]
        public List<BiomeSO> biomes = new List<BiomeSO>();
        [Min(0)] public int minCount = 1;
        [Min(0)] public int maxCount = 1;
        [Tooltip("Cells around the centre that must be walkable ground connected to the start. Nothing else grows there.")]
        [Min(1f)] public float clearRadius = 3f;
        [Min(0f)] public float minStartDistance = 10f;
        [Tooltip("0 = no limit.")]
        [Min(0f)] public float maxStartDistance = 0f;
        [Tooltip("Least distance, in cells, between the centres of this site and any other site.")]
        [Min(0f)] public float minSpacing = 15f;
        public WaterCondition water = WaterCondition.Any;

        [Header("Loot of the chests among the pieces")]
        [Tooltip("Cells from the start per loot tier: the farther, the better the loot.")]
        [Min(1f)] public float tierStep = 30f;
        [Min(0)] public int maxTier = 2;
        [Min(0)] public int lootRollsMin = 3;
        [Min(0)] public int lootRollsMax = 5;
        public List<LootData> loot = new List<LootData>();

        public PoiRule ToRule(IReadOnlyList<BiomeSO> mapBiomes)
        {
            var indices = new List<int>();
            foreach (var biome in biomes)
            {
                int index = biome != null ? IndexOf(mapBiomes, biome) : -1;
                if (index >= 0)
                {
                    indices.Add(index);
                }
            }
            // Biomes were listed but none of them is on this map: no cell may match (an empty array would mean "anywhere").
            if (biomes.Count > 0 && indices.Count == 0)
            {
                indices.Add(-1000);
            }
            return new PoiRule
            {
                MinCount = minCount,
                MaxCount = Mathf.Max(minCount, maxCount),
                ClearRadius = clearRadius,
                Biomes = indices.ToArray(),
                MinStartDistance = minStartDistance,
                MaxStartDistance = maxStartDistance,
                MinSpacing = minSpacing,
                Water = water,
                Anchored = anchored,
                AnchorX = anchorOffset.x,
                AnchorZ = anchorOffset.y,
                AnchorRotation = anchorRotation,
                Paths = ToPathSegments(),
            };
        }

        private PoiPathSegment[] ToPathSegments()
        {
            var segments = new PoiPathSegment[paths.Count];
            for (int i = 0; i < segments.Length; i++)
            {
                var stripe = paths[i];
                segments[i] = new PoiPathSegment
                {
                    FromX = stripe.from.x, FromZ = stripe.from.y, ToX = stripe.to.x, ToZ = stripe.to.y,
                    HalfWidth = stripe.halfWidth, Look = (byte)stripe.look,
                };
            }
            return segments;
        }

        public List<LootEntry> ToLootEntries()
        {
            var entries = new List<LootEntry>();
            foreach (var data in loot)
            {
                if (data.item != null)
                {
                    entries.Add(new LootEntry { Item = data.item.name, Min = data.min, Max = data.max, Weight = data.weight, MinTier = data.minTier });
                }
            }
            return entries;
        }

        private static int IndexOf(IReadOnlyList<BiomeSO> list, BiomeSO biome)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == biome)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
