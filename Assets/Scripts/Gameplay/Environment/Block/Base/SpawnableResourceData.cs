using Hearthglade.Core.World;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Block.Base
{
    /// <summary>
    /// One resource a biome grows. Every spawned resource must be gatherable and have a collider, so the prefab
    /// needs a Resource component. Cells are 0.3675 units wide.
    /// </summary>
    [System.Serializable]
    public class SpawnableResourceData {
        public GameObject resourcePrefab;

        [Range(1, 30)]
        public int howManyResourcesSpawn = 1;

        [Tooltip("Chance per cell and per spawn attempt; with deposits: chance that a square of the map holds a deposit.")]
        [Range(0f, 1f)]
        public float spawnProbability = 1f;

        [Range(0f, 0.35f)]
        public float spawnRange = 0.35f;

        [Range(0f, 360f)]
        public float minRotation = 0f;

        [Range(0f, 360f)]
        public float maxRotation = 360f;

        [Header("Density field (groves and clearings)")]
        [Tooltip("The chance is scaled by a ramp of the density field: 0 below From, 1 above To. From above To = clearings. Equal = ignore.")]
        [Range(0f, 1f)] public float densityFrom = 0f;
        [Range(0f, 1f)] public float densityTo = 0f;

        [Header("Conditions")]
        public WaterCondition water = WaterCondition.Any;
        [Tooltip("Cells from the player's start below which the resource does not grow.")]
        public float minStartDistance = 0f;
        [Tooltip("Cells over which the chance rises from 0 to full after Min Start Distance (0 = a hard step).")]
        public float startDistanceRamp = 0f;
        [Tooltip("Cells from the player's start above which the resource does not grow (0 = no limit).")]
        [Min(0f)] public float maxStartDistance = 0f;
        [Tooltip("Cells before Max Start Distance over which the chance falls from full to 0 (0 = a hard step).")]
        [Min(0f)] public float maxStartDistanceFade = 0f;

        [Header("Climate niche")]
        [Tooltip("Grow only where temperature and humidity (world scale -1 cold/dry .. 1 hot/wet) suit the plant: full chance inside the ranges, falling to 0 over 0.3 outside.")]
        public bool useClimate = false;
        [Range(-1f, 1f)] public float temperatureFrom = -1f;
        [Range(-1f, 1f)] public float temperatureTo = 1f;
        [Range(-1f, 1f)] public float humidityFrom = -1f;
        [Range(-1f, 1f)] public float humidityTo = 1f;

        [Header("Patches (thickets, groves)")]
        [Tooltip("Cells per lattice unit of this entry's own noise: the size of one patch. 0 = no patches (the entry is spread evenly).")]
        [Min(0f)] public float patchScale = 0f;
        [Tooltip("Share of the map the patches cover. The chance inside a patch is the Spawn Probability, so raise it when the coverage is small.")]
        [Range(0f, 1f)] public float patchCoverage = 0.5f;

        [Header("Spacing")]
        [Tooltip("Objects of this entry stay at least this many cells apart (0 = they may overlap). A cell is 0.3675 units wide.")]
        [Min(0f)] public float minSpacing = 0f;

        [Header("Deposits (ore veins, mushroom rings)")]
        [Tooltip("Side, in cells, of the map squares that may hold a deposit. 0 = plain spawns instead of deposits.")]
        [Min(0)] public int clusterSpacing = 0;
        [Min(1)] public int clusterMin = 3;
        [Min(1)] public int clusterMax = 6;
        [Tooltip("Cells around the centre of the deposit the objects are scattered in.")]
        [Min(0f)] public float clusterRadius = 1.5f;

        public ResourceRule ToRule()
        {
            return new ResourceRule
            {
                Count = howManyResourcesSpawn,
                Probability = spawnProbability,
                SpawnRange = spawnRange,
                MinRotation = minRotation,
                MaxRotation = maxRotation,
                DensityFrom = densityFrom,
                DensityTo = densityTo,
                Water = water,
                MinStartDistance = minStartDistance,
                StartDistanceRamp = startDistanceRamp,
                MaxStartDistance = maxStartDistance,
                MaxStartDistanceFade = maxStartDistanceFade,
                ClusterSpacing = clusterSpacing,
                ClusterMin = clusterMin,
                ClusterMax = clusterMax,
                ClusterRadius = clusterRadius,
                MinSpacing = minSpacing,
                UseClimate = useClimate,
                TemperatureFrom = temperatureFrom,
                TemperatureTo = temperatureTo,
                HumidityFrom = humidityFrom,
                HumidityTo = humidityTo,
                PatchScale = patchScale,
                PatchCoverage = patchCoverage,
            };
        }
    }
}
