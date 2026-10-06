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
    // Runs the pure terrain pipeline for a port map (the same calls MapGenerator makes, no scene objects) and prints the island as text:
    // ~ water, . ground, S start of the player, H centre of the harbour site, = painted path, o ground reserved by the site.
    public static class HarborPreview
    {
        private static readonly int[] Seeds = { 12345, 777 };

        public static void PrintMosshollow()
        {
            Print( "Mosshollow" );
        }

        [ MenuItem( "Tools/Agent Tools/Ports/Print Mosshollow layout" ) ]
        public static void PrintMenu()
        {
            Print( "Mosshollow" );
        }

        private static void Print( string mapName )
        {
            var map = AssetDatabase.FindAssets( "t:MapSO" )
                .Select( guid => AssetDatabase.LoadAssetAtPath<MapSO>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .FirstOrDefault( m => m != null && m.mapName == mapName );
            if( map == null )
            {
                UnityEngine.Debug.LogError( $"[HarborPreview] No map named {mapName}" );
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
            var pois = config.Pois.Where( poi => poi != null ).ToList();
            var poiRules = pois.Select( poi => poi.ToRule( config.Biomes ) ).ToList();

            foreach( int gameSeed in Seeds )
            {
                int seed = MapGenerator.GetMapSeed( gameSeed, mapName, 0 );
                var terrain = TerrainGenerator.Generate( edge, seed, heightSettings, temperatureSettings, humiditySettings, rules, shape, liquid, patches );
                var field = ResourceField.Build( terrain, seed, liquid, config.ResourceDensityScale );
                var sites = PoiPlacer.Place( field, poiRules, seed );
                int painted = PoiLayout.PaintPaths( terrain, sites, poiRules, field.IsGround );

                var text = new StringBuilder();
                text.AppendLine( $"[HarborPreview] {mapName}, game seed {gameSeed}: {edge}x{edge} cells, start ({field.StartX},{field.StartZ}), {sites.Count} site(s), {painted} painted cells" );
                int land = 0;
                for( int z = edge - 1; z >= 0; z-- )
                {
                    for( int x = 0; x < edge; x++ )
                    {
                        char c = field.IsGround( x, z ) ? '.' : '~';
                        land += c == '.' ? 1 : 0;
                        if( c == '.' && field.IsReserved( x, z ) )
                        {
                            c = 'o';
                        }
                        if( c != '~' && terrain.Patch[ terrain.Index( x, z ) ] != 0 )
                        {
                            c = '=';
                        }
                        if( x == field.StartX && z == field.StartZ )
                        {
                            c = 'S';
                        }
                        if( sites.Any( s => s.CellX == x && s.CellY == z ) )
                        {
                            c = 'H';
                        }
                        text.Append( c );
                    }
                    text.AppendLine();
                }
                text.AppendLine( $"land cells: {land} of {edge * edge}" );
                UnityEngine.Debug.Log( text.ToString() );
            }
        }
    }
}
