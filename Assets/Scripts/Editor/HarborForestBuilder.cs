using System.Collections.Generic;
using Hearthglade.Gameplay.Environment.Block.Base;
using Hearthglade.Gameplay.Map;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Makes Mosshollow a forest port: scenery variants of the trees (Prefab Variants with Resource.isProp ticked, so they cannot be gathered)
    // and a dense stand of them in the land biomes of the island. The harbour's clear radius keeps the biome trees out of the village,
    // so the trees between the houses are scenery pieces of the harbour POI (planted here, tweak them in the POI editor afterwards).
    // Safe to run again: the variants are kept, the resources of the biomes are rewritten from the table below and the planted trees are replanted.
    public static class HarborForestBuilder
    {
        private const string SceneryFolder = "Assets/_Prefabs/Environment/Trees/Scenery";
        private const string BiomeFolder = "Assets/ScriptableObjects/Map/Biomes/Harbor";
        private const string HarborPoiPath = "Assets/ScriptableObjects/Map/Poi/Harbor_Mosshollow.asset";
        private const string ConfigPath = "Assets/Resources/ScriptableObjects/PerlinNoise/Config/MosshollowMapConfig.asset";
        private const string DirtBlockPath = "Assets/_Prefabs/Blocks/DirtBlock.prefab";

        // Source prefab, by path; the variant is named after it with "Scenery" added.
        private static readonly string[] Sources =
        {
            "Assets/_Prefabs/Environment/Trees/Pine.prefab",
            "Assets/_Prefabs/Environment/Trees/Spruce.prefab",
            "Assets/_Prefabs/Environment/Trees/Birch.prefab",
            "Assets/_Prefabs/Environment/Trees/Oak.prefab",
            "Assets/_Prefabs/Environment/Bush/Bush.prefab",
        };

        // What one biome grows: the variant, the chance per cell, the spacing in cells and the patch noise that clumps the entry (0 = even).
        private struct Stand
        {
            public string Prefab;
            public float Probability;
            public float Spacing;
            public float PatchScale;
            public float PatchCoverage;
        }

        // Conifers carry the look (the reference is a dark pine forest with a village in it); the broadleaves only sprinkle it.
        // The island has one land biome, so this is the whole forest.
        private static readonly Stand[] Forest =
        {
            Tree( "Pine", 0.22f, 0.9f ),
            Tree( "Spruce", 0.20f, 1.0f ),
            Tree( "Birch", 0.05f, 1.4f ),
            Tree( "Oak", 0.04f, 1.8f ),
            Undergrowth( "Bush", 0.08f, 0.9f ),
        };

        private static Stand Tree( string prefab, float probability, float spacing )
        {
            return new Stand { Prefab = prefab, Probability = probability, Spacing = spacing, PatchScale = 0f, PatchCoverage = 0.5f };
        }

        private static Stand Undergrowth( string prefab, float probability, float spacing )
        {
            return new Stand { Prefab = prefab, Probability = probability, Spacing = spacing, PatchScale = 7f, PatchCoverage = 0.5f };
        }

        [ MenuItem( "Tools/Ports/Grow the Mosshollow forest" ) ]
        public static void GrowForest()
        {
            var variants = CreateVariants();
            var forest = AssetDatabase.LoadAssetAtPath<BiomeSO>( $"{BiomeFolder}/HarborForest.asset" );
            var water = AssetDatabase.LoadAssetAtPath<BiomeSO>( $"{BiomeFolder}/HarborWater.asset" );
            var config = AssetDatabase.LoadAssetAtPath<PerlinNoiseMapConfig>( ConfigPath );
            if( forest == null || water == null || config == null )
            {
                UnityEngine.Debug.LogError( "[HarborForestBuilder] The Mosshollow assets are missing, run Tools > Ports > Create Mosshollow map assets first" );
                return;
            }
            forest.Resources.Clear();
            foreach( var stand in Forest )
            {
                forest.Resources.Add( ToResource( variants[ stand.Prefab ], stand ) );
            }
            EditorUtility.SetDirty( forest );
            ShapeIsland( config, water, forest );
            int planted = PlantVillageTrees( variants );
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            var problems = config.GetProblems();
            UnityEngine.Debug.Log( $"[HarborForestBuilder] {variants.Count} scenery variants, the island is water, a dirt shore and the forest, {planted} trees planted in the harbour, {problems.Count} config problem(s){( problems.Count > 0 ? ": " + string.Join( "; ", problems ) : "" )}" );
        }

        // The island is a dirt shore around a forest and nothing else: the forest takes every climate, and the shore is a copy of the harbour
        // beach with the block of dirt. The beach itself stays as it is, because Rimehaven and Dunegate share it.
        private static void ShapeIsland( PerlinNoiseMapConfig config, BiomeSO water, BiomeSO forest )
        {
            var serialized = new SerializedObject( forest );
            foreach( var range in new[] { "temperatureRequirement", "humidityRequirement" } )
            {
                serialized.FindProperty( range ).FindPropertyRelative( "From" ).floatValue = -1f;
                serialized.FindProperty( range ).FindPropertyRelative( "To" ).floatValue = 1f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string shorePath = $"{BiomeFolder}/HarborShore.asset";
            var shore = AssetDatabase.LoadAssetAtPath<BiomeSO>( shorePath );
            if( shore == null )
            {
                shore = Object.Instantiate( AssetDatabase.LoadAssetAtPath<BiomeSO>( $"{BiomeFolder}/HarborBeach.asset" ) );
                shore.name = "HarborShore";
                shore.Color = new Color( 0.45f, 0.32f, 0.2f, 1f );
                AssetDatabase.CreateAsset( shore, shorePath );
            }
            shore.Block = AssetDatabase.LoadAssetAtPath<GameObject>( DirtBlockPath );
            EditorUtility.SetDirty( shore );

            config.Biomes = new List<BiomeSO> { water, shore, forest };
            EditorUtility.SetDirty( config );
        }

        // Trees of the village: scenery pieces in the free space between the houses, thicker the farther from the street, so the forest closes in
        // around the harbour. Keeps clear of the paths and of every piece that is not one of these trees.
        private static int PlantVillageTrees( Dictionary<string, GameObject> variants )
        {
            var poi = AssetDatabase.LoadAssetAtPath<PoiSO>( HarborPoiPath );
            if( poi == null )
            {
                UnityEngine.Debug.LogError( $"[HarborForestBuilder] {HarborPoiPath} is missing" );
                return 0;
            }
            var scenery = new HashSet<GameObject>( variants.Values );
            poi.pieces.RemoveAll( piece => scenery.Contains( piece.prefab ) );

            var kinds = new[] { "Pine", "Pine", "Spruce", "Spruce", "Birch", "Oak", "Bush", "Bush" };
            var random = new System.Random( 20261006 );
            var planted = new List<Vector2>();
            for( int attempt = 0; attempt < 6000; attempt++ )
            {
                var spot = new Vector2( Mathf.Round( Between( random, -13f, 16f ) * 2f ) / 2f, Mathf.Round( Between( random, -13f, 13f ) * 2f ) / 2f );
                // The chance to keep a spot rises from the street (the village stays readable) to the edge of the site.
                float depth = Mathf.Clamp01( ( Mathf.Abs( spot.y ) - 2.5f ) / 5f );
                // The east end, away from the ship, is kept thin.
                float east = 1f - 0.7f * Mathf.Clamp01( ( spot.x - 6f ) / 8f );
                if( random.NextDouble() > ( 0.3f + 0.7f * depth ) * east || !IsFree( poi, spot, planted ) )
                {
                    continue;
                }
                planted.Add( spot );
                string kind = kinds[ random.Next( kinds.Length ) ];
                poi.pieces.Add( new PoiSO.Piece { prefab = variants[ kind ], offset = spot, rotation = ( float ) random.NextDouble() * 360f } );
            }
            EditorUtility.SetDirty( poi );
            return planted.Count;
        }

        private static float Between( System.Random random, float from, float to )
        {
            return from + ( float ) random.NextDouble() * ( to - from );
        }

        private static bool IsFree( PoiSO poi, Vector2 spot, List<Vector2> trees )
        {
            foreach( var path in poi.paths )
            {
                if( DistanceToSegment( spot, path.from, path.to ) < path.halfWidth + 1.5f )
                {
                    return false;
                }
            }
            foreach( var piece in poi.pieces )
            {
                // The houses and stalls are a few cells wide, the props (lanterns, crates) are small.
                if( Vector2.Distance( spot, piece.offset ) < 2.4f )
                {
                    return false;
                }
            }
            foreach( var tree in trees )
            {
                if( Vector2.Distance( spot, tree ) < 1.4f )
                {
                    return false;
                }
            }
            return true;
        }

        private static float DistanceToSegment( Vector2 point, Vector2 from, Vector2 to )
        {
            var edge = to - from;
            float length = edge.sqrMagnitude;
            float t = length > 0f ? Mathf.Clamp01( Vector2.Dot( point - from, edge ) / length ) : 0f;
            return Vector2.Distance( point, from + edge * t );
        }

        private static SpawnableResourceData ToResource( GameObject prefab, Stand stand )
        {
            return new SpawnableResourceData
            {
                resourcePrefab = prefab,
                howManyResourcesSpawn = 1,
                spawnProbability = stand.Probability,
                spawnRange = 0.15f,
                // Groves with few clearings: the chance ramps up early in the density field.
                densityFrom = 0.05f,
                densityTo = 0.3f,
                // The forest thins out towards the far (east) end of the island, away from the ship.
                maxStartDistance = 38f,
                maxStartDistanceFade = 14f,
                minSpacing = stand.Spacing,
                patchScale = stand.PatchScale,
                patchCoverage = stand.PatchCoverage,
            };
        }

        // Scenery variant per source prefab, keyed by the source's name.
        private static Dictionary<string, GameObject> CreateVariants()
        {
            if( !AssetDatabase.IsValidFolder( SceneryFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/_Prefabs/Environment/Trees", "Scenery" );
            }
            var variants = new Dictionary<string, GameObject>();
            foreach( var sourcePath in Sources )
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>( sourcePath );
                string name = source.name;
                string path = $"{SceneryFolder}/{name}Scenery.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>( path );
                if( existing == null )
                {
                    var instance = ( GameObject ) PrefabUtility.InstantiatePrefab( source );
                    instance.name = $"{name}Scenery";
                    var serialized = new SerializedObject( instance.GetComponent<Hearthglade.Gameplay.Resource.Resource>() );
                    serialized.FindProperty( "isProp" ).boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    existing = PrefabUtility.SaveAsPrefabAsset( instance, path );
                    Object.DestroyImmediate( instance );
                }
                variants[ name ] = existing;
            }
            return variants;
        }
    }
}
