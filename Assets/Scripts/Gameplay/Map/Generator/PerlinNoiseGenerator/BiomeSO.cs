using System.Collections.Generic;
using Hearthglade.Core.Farming;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Environment.Block.Base;
using Hearthglade.Gameplay.Map.Visual;
using UnityEngine;

namespace Hearthglade {

    /// <summary>A cosmetic patch inside a biome: it looks like another biome but keeps the block, resources and walkability.</summary>
    [System.Serializable]
    public struct BiomePatchData {
        [Tooltip("The look of the patch, e.g. Dirt for a bare spot.")]
        public BiomeId Look;

        [Tooltip("Share of the whole map covered by the noise peaks this patch grows on (only the part inside the biome shows).")]
        [Range(0f, 1f)] public float Coverage;

        [Tooltip("Map cells per lattice unit of the patch noise: the size of a patch.")]
        public float Scale;

        [Tooltip("Tells patches apart when two of them share the same settings.")]
        public int Salt;
    }

    /// <summary>
    /// One biome as data: its look on the map, the block that makes it (collider, resources, elevation) and the part
    /// of the climate it grows in. A map config lists biomes in priority order, the first one that matches wins.
    /// Height, temperature and humidity are spread evenly over [-1, 1), so a range of width w covers w/2 of the map.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBiome", menuName = "ScriptableObjects/Map/Biome")]
    public class BiomeSO : ScriptableObject {

        [Tooltip("Colour in the map preview window.")]
        public Color Color = Color.white;

        [Tooltip("Block prefab of the biome. Its BlockSO decides the ground look (Biome), resources, walkability and elevation. A block without ground is water.")]
        public GameObject Block;

        [SerializeField] Range heightRequirement = new Range { From = -1f, To = 1f };
        [SerializeField] Range temperatureRequirement = new Range { From = -1f, To = 1f };
        [SerializeField] Range humidityRequirement = new Range { From = -1f, To = 1f };

        [Tooltip("Bare spots, outcrops and the like: only the look of the ground changes.")]
        public List<BiomePatchData> Patches = new List<BiomePatchData>();

        [Tooltip("What grows here. Near a border the resources of both biomes thin out and mix. Every entry must be gatherable and have a collider.")]
        public List<SpawnableResourceData> Resources = new List<SpawnableResourceData>();

        [Tooltip("What lives here. Animals appear off screen anywhere in the biome; a biome without ground (water) spawns its entities on the water.")]
        public List<EntitySpawnData> Entities = new List<EntitySpawnData>();

        public string BiomeName => name;

        public Climate ClimateCentre => new Climate(
            ( temperatureRequirement.From + temperatureRequirement.To ) / 2f, ( humidityRequirement.From + humidityRequirement.To ) / 2f );

        public BiomeRule ToRule( ) {
            return new BiomeRule {
                HeightFrom = heightRequirement.From, HeightTo = heightRequirement.To,
                TemperatureFrom = temperatureRequirement.From, TemperatureTo = temperatureRequirement.To,
                HumidityFrom = humidityRequirement.From, HumidityTo = humidityRequirement.To
            };
        }

        public void SetRanges( Range height, Range temperature, Range humidity ) {
            heightRequirement = height;
            temperatureRequirement = temperature;
            humidityRequirement = humidity;
        }

        public bool BiomeMatchesRequirements( double height, double temperature, double humidity ) {
            return heightRequirement.IsInRange( height ) && temperatureRequirement.IsInRange( temperature ) && humidityRequirement.IsInRange( humidity );
        }
    }
}
