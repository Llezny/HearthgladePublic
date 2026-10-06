using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Plans the resources of the home map for a few seeds (the same pure pipeline MapGenerator uses, no scene objects)
    // and prints how many of each resource grows, to tune the spawn probabilities in the biome assets.
    public static class WildPlantCounter
    {
        private static readonly int[] Seeds = { 12345, 777, 2024 };

        [ MenuItem( "Tools/Farming/Count wild plants on the home map" ) ]
        public static void Count()
        {
            var map = AssetDatabase.FindAssets( "t:MapSO" )
                .Select( guid => AssetDatabase.LoadAssetAtPath<MapSO>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .FirstOrDefault( m => m != null && m.IsHome && !m.IsEndless );
            if( map == null )
            {
                UnityEngine.Debug.LogError( "[WildPlantCounter] No finite home map found" );
                return;
            }
            var config = map.perlinNoiseConfig;
            int edge = ChunkManager.ChunkSize * map.SizeInChunks;

            var heightSettings = PerlinNoiseGenerator.ToSettings( config.HeightNoisePreset );
            var temperatureSettings = PerlinNoiseGenerator.ToSettings( config.TemperatureMapPreset );
            var humiditySettings = PerlinNoiseGenerator.ToSettings( config.HumidityMapPreset );
            var rules = PerlinNoiseGenerator.ToBiomeRules( config );
            var shape = config.ToShape();
            var liquid = PerlinNoiseGenerator.ToLiquidFlags( config );
            var patches = PerlinNoiseGenerator.ToPatches( config );
            var tables = new ResourceTable[ config.Biomes.Count ];
            var ruleTables = new IReadOnlyList<ResourceRule>[ tables.Length ];
            for( int i = 0; i < tables.Length; i++ )
            {
                tables[ i ] = ResourceTable.From( config.Biomes[ i ].Resources );
                ruleTables[ i ] = tables[ i ].Rules;
            }
            var pois = config.Pois.Where( poi => poi != null ).ToList();
            var poiRules = pois.Select( poi => poi.ToRule( config.Biomes ) ).ToList();

            var report = new StringBuilder( $"[WildPlantCounter] {map.mapName}, {edge}x{edge} cells, resources per seed\n" );
            var totals = new SortedDictionary<string, List<int>>();
            foreach( int seed in Seeds )
            {
                var terrain = TerrainGenerator.Generate( edge, seed, heightSettings, temperatureSettings, humiditySettings, rules, shape, liquid, patches );
                var field = ResourceField.Build( terrain, seed, liquid, config.ResourceDensityScale );
                PoiPlacer.Place( field, poiRules, seed );
                var plan = new ResourcePlanner( field, ruleTables, seed, MapGenerator.TILE_X_OFFSET, ChunkManager.ChunkSize, config.ResourceChunkBudget ).PlanMap();

                var cellsPerBiome = new int[ config.Biomes.Count ];
                foreach( int biome in terrain.Biome )
                {
                    if( biome != GeneratedTerrain.NoBiome )
                    {
                        cellsPerBiome[ biome ]++;
                    }
                }
                var counts = new SortedDictionary<string, int>();
                foreach( var cell in plan.Values )
                {
                    foreach( var planned in cell )
                    {
                        string name = tables[ planned.Table ].Templates[ planned.RuleIndex ].Name;
                        counts[ name ] = counts.TryGetValue( name, out int n ) ? n + 1 : 1;
                    }
                }

                report.Append( $"seed {seed}: " );
                report.Append( string.Join( ", ", config.Biomes.Select( ( b, i ) => $"{b.name} {cellsPerBiome[ i ]}" ) ) );
                report.Append( '\n' );
                foreach( var pair in counts )
                {
                    if( !totals.TryGetValue( pair.Key, out var list ) )
                    {
                        totals[ pair.Key ] = list = new List<int>();
                    }
                    list.Add( pair.Value );
                }
            }

            foreach( var pair in totals )
            {
                report.Append( $"{pair.Key,-26} {string.Join( " / ", pair.Value )}\n" );
            }
            UnityEngine.Debug.Log( report.ToString() );
        }
    }
}
