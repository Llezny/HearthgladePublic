using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Creates the data of the expedition island (docs/EXPLORATION_LOOP_PLAN.md, phase 1): one map whose biomes cover the whole climate
    // space, so that the direction and depth of a trip (a climate offset, see ExpeditionClimate) decide which biomes come out.
    // Existing assets are never overwritten, so the data can be tuned by hand afterwards.
    public static class ExpeditionMapBuilder
    {
        private const string ForestMapPath = "Assets/Resources/ScriptableObjects/Maps/Forest.asset";
        private const string BiomeFolder = "Assets/ScriptableObjects/Map/Biomes";
        private const string ConfigFolder = "Assets/Resources/ScriptableObjects/PerlinNoise/Config";
        private const string MapFolder = "Assets/Resources/ScriptableObjects/Maps";
        private const string CostFolder = "Assets/Resources/ScriptableObjects/Expeditions";

        // Tundra only where it is really cold; the taiga covers the rest of the cold part of the climate space.
        private const float TundraTemperatureTo = -0.7f;

        [ MenuItem( "Tools/Expedition/Create expedition map assets" ) ]
        public static void Create()
        {
            var forest = AssetDatabase.LoadAssetAtPath<MapSO>( ForestMapPath );
            if( forest == null || forest.perlinNoiseConfig == null )
            {
                UnityEngine.Debug.LogError( $"[ExpeditionMapBuilder] {ForestMapPath} is missing or has no config" );
                return;
            }

            var tundra = LoadOrCreate( $"{BiomeFolder}/ExpeditionTundra.asset", () => CreateColdTundra() );
            var config = LoadOrCreate<PerlinNoiseMapConfig>( $"{ConfigFolder}/ExpeditionMapConfig.asset", () => CreateConfig( forest.perlinNoiseConfig, tundra ) );
            LoadOrCreate( $"{MapFolder}/Expedition.asset", () => CreateMap( forest, config ) );
            LoadOrCreate( $"{CostFolder}/ExpeditionCostCurve.asset", CreateCostCurve );
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var problems = config.GetProblems();
            UnityEngine.Debug.Log( $"[ExpeditionMapBuilder] Expedition assets are ready ({config.Biomes.Count} biomes, {problems.Count} problem(s){( problems.Count > 0 ? ": " + string.Join( "; ", problems ) : "" )})" );
        }

        // Prints the share of land per biome for every direction and the first depths, from the real generator pipeline.
        [ MenuItem( "Tools/Expedition/Print biome shares per direction" ) ]
        public static void PrintShares()
        {
            var map = AssetDatabase.FindAssets( "t:MapSO" )
                .Select( guid => AssetDatabase.LoadAssetAtPath<MapSO>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .FirstOrDefault( m => m != null && m.mapName == "Expedition" );
            if( map == null )
            {
                UnityEngine.Debug.LogError( "[ExpeditionMapBuilder] No map named Expedition, run Tools > Expedition > Create expedition map assets first" );
                return;
            }
            var config = map.perlinNoiseConfig;
            int edge = ChunkManager.ChunkSize * map.SizeInChunks;
            var heightSettings = PerlinNoiseGenerator.ToSettings( config.HeightNoisePreset );
            var temperatureSettings = PerlinNoiseGenerator.ToSettings( config.TemperatureMapPreset );
            var humiditySettings = PerlinNoiseGenerator.ToSettings( config.HumidityMapPreset );
            var rules = PerlinNoiseGenerator.ToBiomeRules( config );
            var liquid = PerlinNoiseGenerator.ToLiquidFlags( config );
            var patches = PerlinNoiseGenerator.ToPatches( config );
            var names = config.Biomes.Select( b => b.BiomeName ).ToList();
            int[] seeds = { 12345, 777, 4242 };

            var text = new StringBuilder( "[ExpeditionMapBuilder] land share per biome (3 seeds)\n" );
            foreach( Direction direction in System.Enum.GetValues( typeof( Direction ) ) )
            {
                for( int depth = 1; depth <= 5; depth++ )
                {
                    var trip = new ExpeditionTarget( direction, depth );
                    var shape = config.ToShape();
                    ExpeditionClimate.Offsets( trip, out shape.TemperatureOffset, out shape.HumidityOffset );
                    var share = new double[ names.Count ];
                    int land = 0;
                    foreach( int gameSeed in seeds )
                    {
                        int seed = MapGenerator.GetMapSeed( gameSeed, map.mapName, 1 );
                        var terrain = TerrainGenerator.Generate( edge, seed, heightSettings, temperatureSettings, humiditySettings, rules, shape, liquid, patches );
                        foreach( int biome in terrain.Biome )
                        {
                            if( biome >= 0 && !liquid[ biome ] )
                            {
                                share[ biome ]++;
                                land++;
                            }
                        }
                    }
                    var parts = new List<string>();
                    for( int b = 0; b < names.Count; b++ )
                    {
                        if( !liquid[ b ] && share[ b ] > 0 )
                        {
                            parts.Add( $"{names[ b ]} {share[ b ] / land:P0}" );
                        }
                    }
                    text.AppendLine( $"{trip,-12} {string.Join( ", ", parts )}" );
                }
            }
            UnityEngine.Debug.Log( text.ToString() );
        }

        private static T LoadOrCreate<T>( string path, System.Func<T> create ) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>( path );
            if( existing != null )
            {
                return existing;
            }
            EnsureFolder( System.IO.Path.GetDirectoryName( path ).Replace( '\\', '/' ) );
            var created = create();
            AssetDatabase.CreateAsset( created, path );
            return created;
        }

        private static BiomeSO CreateColdTundra()
        {
            var source = AssetDatabase.LoadAssetAtPath<BiomeSO>( $"{BiomeFolder}/Tundra.asset" );
            var biome = Object.Instantiate( source );
            biome.name = "ExpeditionTundra";
            var serialized = new SerializedObject( biome );
            serialized.FindProperty( "temperatureRequirement" ).FindPropertyRelative( "To" ).floatValue = TundraTemperatureTo;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return biome;
        }

        private static PerlinNoiseMapConfig CreateConfig( PerlinNoiseMapConfig source, BiomeSO coldTundra )
        {
            BiomeSO Load( string name ) => AssetDatabase.LoadAssetAtPath<BiomeSO>( $"{BiomeFolder}/{name}.asset" );
            var config = Object.Instantiate( source );
            config.name = "ExpeditionMapConfig";
            // Priority order: the first match wins. Together the ranges leave no gap (the config validates itself).
            config.Biomes = new List<BiomeSO> { Load( "Water" ), Load( "Beach" ), Load( "Rocks" ), coldTundra, Load( "Taiga" ), Load( "Swamp" ), Load( "Forest" ), Load( "Meadow" ) };
            return config;
        }

        private static MapSO CreateMap( MapSO source, PerlinNoiseMapConfig config )
        {
            var map = Object.Instantiate( source );
            map.name = "Expedition";
            map.mapName = "Expedition";
            map.perlinNoiseConfig = config;
            return map;
        }

        // Food is the main cost (see FoodBudget); a little wood is the rest. A first guess, to be tuned by playing.
        private static ExpeditionCostCurve CreateCostCurve()
        {
            var curve = ScriptableObject.CreateInstance<ExpeditionCostCurve>();
            curve.baseFood = 20f;
            curve.foodPerExtraDepth = 8f;
            curve.baseCost = new List<Requirement> { new Requirement( "Wood", 2 ) };
            curve.perExtraDepth = new List<Requirement> { new Requirement( "Wood", 1 ) };
            return curve;
        }

        private static void EnsureFolder( string path )
        {
            if( AssetDatabase.IsValidFolder( path ) )
            {
                return;
            }
            var parent = System.IO.Path.GetDirectoryName( path ).Replace( '\\', '/' );
            EnsureFolder( parent );
            AssetDatabase.CreateFolder( parent, System.IO.Path.GetFileName( path ) );
        }
    }
}
