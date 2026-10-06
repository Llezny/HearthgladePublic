using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using UnityEngine;

namespace Hearthglade {

    [System.Serializable]
    public struct Range {
        [Range(-1f, 1f)]
        public float To;

        [Range(-1f, 1f)]
        public float From;
        public bool IsInRange( double value ) {
            return value >= From && value < To;
        }
    }


    [CreateAssetMenu(fileName = "PerlinNoiseMapConfig", menuName = "ScriptableObjects/Map/PerlinNoise/MapConfig")]
    public class PerlinNoiseMapConfig : ScriptableObject {

        public int Size = 0;
        public PerlinNoisePreset HeightNoisePreset;
        public PerlinNoisePreset TemperatureMapPreset;
        public PerlinNoisePreset HumidityMapPreset;

        [Tooltip("Height, temperature and humidity are spread evenly over [-1, 1), so a range of width w covers w/2 of the map. The first matching biome wins.")]
        public List<BiomeSO> Biomes;

        [Header("World shape (Island Radius 0 = plain noise, no island)")]
        [Tooltip("Where the coast lies on average, as a share of the half map width (1 = the middle of an edge). 0 turns the whole shape off.")]
        public float IslandRadius = 0f;
        [Tooltip("Half width of the fade to sea around the radius, as a share of the half map width.")]
        public float IslandFalloff = 0.2f;
        [Tooltip("Height of the lowest inland ground (-1..1). The land noise is spread over [InteriorFloor, 1); 0 = no lakes inland.")]
        [Range(-1f, 1f)] public float InteriorFloor = 0f;
        [Tooltip("Map cells per lattice unit of the noise that bends the coast and the biome borders.")]
        public float WarpScale = 25f;
        [Tooltip("How far, in cells, the bend can move a sample. 0 = no bend.")]
        public float WarpStrength = 0f;
        [Tooltip("How far a fine noise moves temperature and humidity before biomes are picked (climate units, -1..1): ragged instead of smooth borders. 0 = off.")]
        [Range(0f, 0.3f)] public float ClimateJitter = 0f;
        [Tooltip("Map cells per lattice unit of that noise: small = fine ragged borders.")]
        public float ClimateJitterScale = 5f;
        [Tooltip("Only the largest piece of land stays, any other becomes sea.")]
        public bool KeepMainIslandOnly = false;
        [Tooltip("Lakes smaller than this many cells are filled in (they turn into beach). 0 = keep all.")]
        public int MinLakeSize = 0;

        [Header("Resources")]
        [Tooltip("Map cells per lattice unit of the density field that resource rules read: the size of a grove or a clearing.")]
        [Min(1f)] public float ResourceDensityScale = 14f;

        [Tooltip("Most resource objects one chunk may hold (the rest is cut, deposits last). Protects the frame rate in dense groves. 0 = no limit.")]
        [Min(0)] public int ResourceChunkBudget = 0;

        [Header("Points of interest")]
        [Tooltip("Sites the generator places (camps, ruins, ...), each with its own rules and loot.")]
        public List<PoiSO> Pois = new List<PoiSO>();

        public bool HasIslandShape => IslandRadius > 0f;

        public WorldShapeSettings ToShape( ) {
            return new WorldShapeSettings {
                IslandRadius = IslandRadius, IslandFalloff = IslandFalloff, InteriorFloor = InteriorFloor,
                WarpScale = WarpScale, WarpStrength = WarpStrength,
                ClimateJitter = ClimateJitter, ClimateJitterScale = ClimateJitterScale,
                KeepMainIslandOnly = KeepMainIslandOnly, MinLakeSize = MinLakeSize
            };
        }

        /// <summary>
        /// What is wrong with this config, one message per problem (empty when it is fine): missing presets or
        /// biome blocks, and climate ranges no biome covers (cells there get no block).
        /// </summary>
        public List<string> GetProblems( ) {
            var problems = new List<string>();
            if( HeightNoisePreset == null ) problems.Add( "HeightNoisePreset is not set" );
            if( TemperatureMapPreset == null ) problems.Add( "TemperatureMapPreset is not set" );
            if( HumidityMapPreset == null ) problems.Add( "HumidityMapPreset is not set" );
            if( Biomes == null || Biomes.Count == 0 ) {
                problems.Add( "there are no biomes" );
                return problems;
            }

            for( int i = 0; i < Biomes.Count; i++ ) {
                if( Biomes[ i ] == null ) {
                    problems.Add( $"biome {i} is empty" );
                    return problems;
                }
                if( Biomes[ i ].Block == null ) {
                    problems.Add( $"biome {i} ({Biomes[ i ].BiomeName}) has no block" );
                }
            }

            var report = BiomeRuleValidator.Validate( PerlinNoiseGenerator.ToBiomeRules( this ) );
            if( report.HasGaps ) {
                problems.Add( $"{report.GapVolume:P1} of the climate space matches no biome, e.g. {report.Gaps[ 0 ]}" );
            }
            return problems;
        }

        private void OnValidate( ) {
            foreach( var problem in GetProblems() ) {
                UnityEngine.Debug.LogWarning( $"{name}: {problem}", this );
            }
        }

        public bool TryFindMatchingBiome( double height, double temperature, double humidity, out BiomeSO outBiome ) {
            foreach( var biome in Biomes ) {
                if( biome.BiomeMatchesRequirements( height, temperature, humidity ) ) {
                    outBiome = biome;
                    return true;
                }
            }
            outBiome = null;
            return false;
        }
    }
}
