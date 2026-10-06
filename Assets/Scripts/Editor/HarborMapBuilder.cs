using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Visual;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Creates the data of a port island (docs/EXPLORATION_LOOP_PLAN.md, 8.1b): a small persistent map made from the home map's
    // config, but with biome copies that grow nothing and hold no animals, and one anchored "Harbor" site that is laid out by hand.
    // Existing assets are never overwritten, so the layout can be tuned by hand afterwards.
    public static class HarborMapBuilder
    {
        private const string HomeMapPath = "Assets/Resources/ScriptableObjects/Maps/Home.asset";
        private const string BiomeFolder = "Assets/ScriptableObjects/Map/Biomes/Harbor";
        private const string PoiFolder = "Assets/ScriptableObjects/Map/Poi";
        private const string ConfigFolder = "Assets/Resources/ScriptableObjects/PerlinNoise/Config";
        private const string MapFolder = "Assets/Resources/ScriptableObjects/Maps";
        private const int SizeInChunks = 4;

        [ MenuItem( "Tools/Agent Tools/Ports/Create Mosshollow map assets" ) ]
        public static void CreateMosshollow()
        {
            var home = AssetDatabase.LoadAssetAtPath<MapSO>( HomeMapPath );
            if( home == null || home.perlinNoiseConfig == null )
            {
                UnityEngine.Debug.LogError( $"[HarborMapBuilder] {HomeMapPath} is missing or has no config" );
                return;
            }

            var harbor = LoadOrCreate( $"{PoiFolder}/Harbor_Mosshollow.asset", CreateHarborPoi );
            var config = LoadOrCreate<PerlinNoiseMapConfig>( $"{ConfigFolder}/MosshollowMapConfig.asset", () => CreateConfig( home.perlinNoiseConfig, harbor ) );
            LoadOrCreate( $"{MapFolder}/Mosshollow.asset", () => CreateMap( home, config ) );
            foreach( var biome in config.Biomes )
            {
                if( biome.Patches.Count > 0 )
                {
                    biome.Patches.Clear();
                    EditorUtility.SetDirty( biome );
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log( "[HarborMapBuilder] Mosshollow assets are ready (map, config, biomes, harbor site)" );
        }

        // Both further ports reuse the noise and the island shape of Mosshollow (the layout of a harbour is made for that size) and
        // differ in their biomes: a bare tundra for Rimehaven, bare sand for Dunegate. The island itself differs by its seed, which comes
        // from the map name.
        [ MenuItem( "Tools/Agent Tools/Ports/Create Rimehaven and Dunegate map assets" ) ]
        public static void CreateFurtherPorts()
        {
            CreateIsland( "Rimehaven", ( ) => new System.Collections.Generic.List<BiomeSO> { Bare( "Water" ), Bare( "Beach" ), Bare( "Tundra" ) } );
            CreateIsland( "Dunegate", ( ) => new System.Collections.Generic.List<BiomeSO> { Bare( "Water" ), Bare( "Beach" ), Sand() } );
        }

        private static void CreateIsland( string name, System.Func<System.Collections.Generic.List<BiomeSO>> biomes )
        {
            var home = AssetDatabase.LoadAssetAtPath<MapSO>( HomeMapPath );
            var template = AssetDatabase.LoadAssetAtPath<PerlinNoiseMapConfig>( $"{ConfigFolder}/MosshollowMapConfig.asset" );
            if( home == null || template == null )
            {
                UnityEngine.Debug.LogError( "[HarborMapBuilder] The home map or the Mosshollow config is missing, run Tools > Agent Tools > Ports > Create Mosshollow map assets first" );
                return;
            }
            EnsureFolder( BiomeFolder );
            var harbor = LoadOrCreate( $"{PoiFolder}/Harbor_{name}.asset", CreateHarborPoi );
            var config = LoadOrCreate<PerlinNoiseMapConfig>( $"{ConfigFolder}/{name}MapConfig.asset", ( ) =>
            {
                var created = Object.Instantiate( template );
                created.name = $"{name}MapConfig";
                created.Pois = new System.Collections.Generic.List<PoiSO> { harbor };
                created.Biomes = biomes();
                return created;
            } );
            LoadOrCreate( $"{MapFolder}/{name}.asset", ( ) =>
            {
                var map = CreateMap( home, config );
                map.name = name;
                map.mapName = name;
                return map;
            } );
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var problems = config.GetProblems();
            UnityEngine.Debug.Log( $"[HarborMapBuilder] {name} assets are ready ({config.Biomes.Count} biomes, {problems.Count} problem(s){( problems.Count > 0 ? ": " + string.Join( "; ", problems ) : "" )})" );
        }

        private static BiomeSO Bare( string sourceName )
        {
            return LoadOrCreate( $"{BiomeFolder}/Harbor{sourceName}.asset",
                ( ) => CreateBareBiome( AssetDatabase.LoadAssetAtPath<BiomeSO>( $"Assets/ScriptableObjects/Map/Biomes/{sourceName}.asset" ) ) );
        }

        // Ground of the beach all over the land (a bare copy of the meadow with the block of the beach).
        private static BiomeSO Sand()
        {
            return LoadOrCreate( $"{BiomeFolder}/HarborSand.asset", ( ) =>
            {
                var beach = AssetDatabase.LoadAssetAtPath<BiomeSO>( "Assets/ScriptableObjects/Map/Biomes/Beach.asset" );
                var sand = CreateBareBiome( AssetDatabase.LoadAssetAtPath<BiomeSO>( "Assets/ScriptableObjects/Map/Biomes/Meadow.asset" ) );
                sand.name = "HarborSand";
                sand.Block = beach.Block;
                var serialized = new SerializedObject( sand );
                serialized.FindProperty( "temperatureRequirement" ).FindPropertyRelative( "From" ).floatValue = -1f;
                serialized.FindProperty( "temperatureRequirement" ).FindPropertyRelative( "To" ).floatValue = 1f;
                serialized.FindProperty( "humidityRequirement" ).FindPropertyRelative( "From" ).floatValue = -1f;
                serialized.FindProperty( "humidityRequirement" ).FindPropertyRelative( "To" ).floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return sand;
            } );
        }

        private static T LoadOrCreate<T>( string path, System.Func<T> create ) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>( path );
            if( existing != null )
            {
                return existing;
            }
            var created = create();
            AssetDatabase.CreateAsset( created, path );
            return created;
        }

        private static PoiSO CreateHarborPoi()
        {
            var poi = ScriptableObject.CreateInstance<PoiSO>();
            poi.anchored = true;
            poi.anchorOffset = new Vector2Int( 14, 0 ); // east of the player's start, which is on the west coast
            poi.anchorRotation = 0f;
            poi.clearRadius = 14f;
            poi.minCount = poi.maxCount = 1;
            // A first layout to judge the flow of the island: a street from the pier to the market plaza and a lane across it.
            poi.paths.Add( new PoiSO.PathStripe { from = new Vector2( -12, 0 ), to = new Vector2( 6, 0 ), halfWidth = 1.2f, look = BiomeId.Dirt } );
            poi.paths.Add( new PoiSO.PathStripe { from = new Vector2( 6, 0 ), to = new Vector2( 6, 0 ), halfWidth = 4f, look = BiomeId.Stone } );
            poi.paths.Add( new PoiSO.PathStripe { from = new Vector2( 6, -9 ), to = new Vector2( 6, 9 ), halfWidth = 1f, look = BiomeId.Dirt } );
            return poi;
        }

        private static PerlinNoiseMapConfig CreateConfig( PerlinNoiseMapConfig source, PoiSO harbor )
        {
            EnsureFolder( BiomeFolder );
            var config = Object.Instantiate( source );
            config.name = "MosshollowMapConfig";
            config.Size = SizeInChunks * ChunkManager.ChunkSize;
            config.ResourceChunkBudget = 0;
            config.Pois = new System.Collections.Generic.List<PoiSO> { harbor };
            config.Biomes = new System.Collections.Generic.List<BiomeSO>();
            foreach( var biome in source.Biomes )
            {
                config.Biomes.Add( LoadOrCreate( $"{BiomeFolder}/Harbor{biome.name}.asset", () => CreateBareBiome( biome ) ) );
            }
            return config;
        }

        // Same block and climate range as the original, so the ground looks the same; nothing grows and nothing lives there.
        private static BiomeSO CreateBareBiome( BiomeSO source )
        {
            var biome = Object.Instantiate( source );
            biome.name = $"Harbor{source.name}";
            biome.Resources.Clear();
            biome.Entities.Clear();
            biome.Patches.Clear(); // random bare spots would blur the paths of the harbour
            return biome;
        }

        private static MapSO CreateMap( MapSO home, PerlinNoiseMapConfig config )
        {
            var map = Object.Instantiate( home );
            map.name = "Mosshollow";
            map.mapName = "Mosshollow";
            map.IsHome = false;
            map.IsPersistent = true;
            map.SizeInChunks = SizeInChunks;
            map.perlinNoiseConfig = config;
            return map;
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
