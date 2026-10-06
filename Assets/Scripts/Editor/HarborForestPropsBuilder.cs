using System.Collections.Generic;
using System.Linq;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Trade;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Dark timber props for the forest port Mosshollow (Tools/Agent Tools/Blender/harbor_forest_props.py): shingled timber houses, wooden stalls, a rail
    // fence, wood piles, a bracket lantern, and the shared props (crates, barrels, boardwalk, ...) in the dark palette of the forest. The
    // layout of Mosshollow is switched to them and dressed with fences and wood piles. Rimehaven and Dunegate keep the original props.
    // Safe to run again: prefabs are refitted, pieces already switched stay, the dressing is only added once.
    public static class HarborForestPropsBuilder
    {
        private const string ModelFolder = "Assets/Arts/Models/Environment/HarborForest";
        private const string SharedModelFolder = "Assets/Arts/Models/Environment/Harbor";
        private const string PrefabFolder = "Assets/_Prefabs/Environment/HarborForest";
        private const string PaletteFolder = "Assets/Arts/Sprites/ColorPalette";
        private const string MaterialPath = "Assets/Arts/Materials/Environment/HarborForest.mat";
        private const string BaseMaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";
        private const string SceneryFolder = "Assets/_Prefabs/Environment/Trees/Scenery";
        private const string HarborPoiPath = "Assets/ScriptableObjects/Map/Poi/Harbor_Mosshollow.asset";

        // Models drawn for the forest. Colliders as in HarborAssetBuilder: box in the prop's own metres (centre, size), then the scale.
        private static readonly HarborAssetBuilder.Spec[] Specs =
        {
            HarborAssetBuilder.Solid( "ForestStallFruit", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.64f, 0.14f, 0.16f ), 1.7f ),
            HarborAssetBuilder.Solid( "ForestStallHerbs", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.64f, 0.14f, 0.16f ), 1.7f ),
            HarborAssetBuilder.Solid( "ForestStallTimber", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.64f, 0.14f, 0.16f ), 1.7f ),
            HarborAssetBuilder.Solid( "ForestHouseA", new Vector3( 0f, 0.2f, 0f ), new Vector3( 0.56f, 0.4f, 0.66f ), 1.8f ),
            HarborAssetBuilder.Solid( "ForestHouseB", new Vector3( 0f, 0.22f, 0f ), new Vector3( 0.54f, 0.44f, 0.58f ), 1.8f ),
            HarborAssetBuilder.Solid( "ForestHouseC", new Vector3( 0f, 0.2f, -0.095f ), new Vector3( 0.8f, 0.4f, 0.7f ), 1.8f ),
            HarborAssetBuilder.Solid( "ForestHouseLarge", new Vector3( 0.18f, 0.25f, -0.09f ), new Vector3( 1.38f, 0.5f, 0.76f ), 1.5f ),
            HarborAssetBuilder.Solid( "ForestFence", new Vector3( 0f, 0.08f, 0f ), new Vector3( 0.37f, 0.16f, 0.05f ), 1.6f ),
            HarborAssetBuilder.Solid( "ForestWoodPile", new Vector3( 0f, 0.07f, 0f ), new Vector3( 0.36f, 0.15f, 0.3f ), 1.6f ),
            HarborAssetBuilder.Solid( "ForestLantern", new Vector3( 0f, 0.17f, 0f ), new Vector3( 0.05f, 0.34f, 0.05f ), 1.6f ),
        };

        // The shared harbour props that only change their colours: "Harbor<name>" model and collider, prefab "Forest<name>".
        private static readonly string[] Recoloured = { "Quay", "Bollard", "Crate", "Barrel", "Sacks", "Signpost", "Cart", "DryingRack", "Boat" };

        // Layout pieces of the original harbour that are replaced by a forest prop.
        private static readonly Dictionary<string, string> Replacements = new Dictionary<string, string>
        {
            { "HarborStallFruit", "ForestStallFruit" }, { "HarborStallHerbs", "ForestStallHerbs" }, { "HarborStallTimber", "ForestStallTimber" },
            { "HarborHouseA", "ForestHouseA" }, { "HarborHouseB", "ForestHouseB" }, { "HarborHouseC", "ForestHouseC" }, { "HarborHouseLarge", "ForestHouseLarge" },
            { "HarborLantern", "ForestLantern" },
        };

        // Cells around the centre of a house in which the scenery trees are cleared (the planted trees keep 2.4 cells from the centres, the
        // wide houses are bigger than that).
        private static readonly Dictionary<string, float> HouseRadius = new Dictionary<string, float>
        {
            { "ForestHouseA", 2.3f }, { "ForestHouseB", 2.3f }, { "ForestHouseC", 2.8f }, { "ForestHouseLarge", 3.6f },
        };

        [ MenuItem( "Tools/Agent Tools/Ports/Build Mosshollow forest props" ) ]
        public static void BuildAll()
        {
            if( !AssetDatabase.IsValidFolder( PrefabFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/_Prefabs/Environment", "HarborForest" );
            }
            AssetDatabase.Refresh();
            HarborAssetBuilder.ImportModels( ModelFolder );
            var material = EnsureMaterial();

            var prefabs = new Dictionary<string, GameObject>();
            foreach( var spec in Specs )
            {
                prefabs[ spec.Name ] = BuildPrefab( spec, $"{ModelFolder}/{spec.Name}.fbx", material );
            }
            foreach( var name in Recoloured )
            {
                var shared = HarborAssetBuilder.Specs.First( s => s.Name == $"Harbor{name}" );
                var spec = shared;
                spec.Name = $"Forest{name}";
                prefabs[ spec.Name ] = BuildPrefab( spec, $"{SharedModelFolder}/{shared.Name}.fbx", material );
            }
            ApplyLayout( prefabs );
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( $"[HarborForestPropsBuilder] {prefabs.Count} forest prefabs are ready" );
        }

        // The palette of the forest keeps the cell layout of the main palette, so a prop is recoloured by its material alone; the emission map
        // lights the windows and the lamps.
        private static Material EnsureMaterial()
        {
            var palette = LoadPalette( "Colorsheet Forest.png" );
            var emission = LoadPalette( "Colorsheet Forest Emission.png" );
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            if( material == null )
            {
                material = new Material( AssetDatabase.LoadAssetAtPath<Material>( BaseMaterialPath ) ) { name = "HarborForest" };
                AssetDatabase.CreateAsset( material, MaterialPath );
            }
            material.SetTexture( "_BaseMap", palette );
            material.SetTexture( "_EmissionMap", emission );
            material.SetColor( "_EmissionColor", Color.white * 1.6f );
            material.EnableKeyword( "_EMISSION" );
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty( material );
            return material;
        }

        // Point filtering and no mips, so that a cell of the palette stays one flat colour.
        private static Texture2D LoadPalette( string file )
        {
            string path = $"{PaletteFolder}/{file}";
            AssetDatabase.ImportAsset( path );
            var importer = ( TextureImporter ) AssetImporter.GetAtPath( path );
            if( importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 32 )
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 32;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>( path );
        }

        private static GameObject BuildPrefab( HarborAssetBuilder.Spec spec, string modelPath, Material material )
        {
            string path = $"{PrefabFolder}/{spec.Name}.prefab";
            if( AssetDatabase.LoadAssetAtPath<GameObject>( path ) != null )
            {
                var contents = PrefabUtility.LoadPrefabContents( path );
                Paint( contents, material );
                HarborAssetBuilder.Fit( contents, spec );
                var refitted = PrefabUtility.SaveAsPrefabAsset( contents, path );
                PrefabUtility.UnloadPrefabContents( contents );
                return refitted;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>( modelPath );
            var root = new GameObject( spec.Name );
            root.AddComponent<PortProp>();
            var visual = ( GameObject ) PrefabUtility.InstantiatePrefab( model, root.transform );
            visual.name = "Model";
            Paint( root, material );
            HarborAssetBuilder.Fit( root, spec );
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, path );
            Object.DestroyImmediate( root );
            return prefab;
        }

        private static void Paint( GameObject root, Material material )
        {
            foreach( var renderer in root.GetComponentsInChildren<Renderer>() )
            {
                renderer.sharedMaterial = material;
            }
        }

        // ---- layout ----

        private static void ApplyLayout( Dictionary<string, GameObject> prefabs )
        {
            var poi = AssetDatabase.LoadAssetAtPath<PoiSO>( HarborPoiPath );
            if( poi == null )
            {
                UnityEngine.Debug.LogError( $"[HarborForestPropsBuilder] {HarborPoiPath} is missing" );
                return;
            }
            var scenery = AssetDatabase.FindAssets( "t:Prefab", new[] { SceneryFolder } )
                .Select( guid => AssetDatabase.LoadAssetAtPath<GameObject>( AssetDatabase.GUIDToAssetPath( guid ) ) ).ToHashSet();

            // Every shared prop has its forest twin, the originals only stay in the other ports. Flower beds make no sense in a dark forest;
            // the wood piles below stand in for them.
            var replacements = Recoloured.ToDictionary( name => $"Harbor{name}", name => $"Forest{name}" );
            foreach( var pair in Replacements )
            {
                replacements[ pair.Key ] = pair.Value;
            }
            int switched = 0;
            foreach( var piece in poi.pieces.Where( p => p.prefab != null && replacements.ContainsKey( p.prefab.name ) ) )
            {
                piece.prefab = prefabs[ replacements[ piece.prefab.name ] ];
                switched++;
            }
            int beds = poi.pieces.RemoveAll( p => p.prefab != null && p.prefab.name == "HarborFlowerBed" );

            int cleared = 0;
            foreach( var house in poi.pieces.Where( p => p.prefab != null && HouseRadius.ContainsKey( p.prefab.name ) ).ToList() )
            {
                cleared += poi.pieces.RemoveAll( p => scenery.Contains( p.prefab ) && Vector2.Distance( p.offset, house.offset ) < HouseRadius[ house.prefab.name ] );
            }

            int dressed = 0;
            if( !poi.pieces.Any( p => p.prefab != null && ( p.prefab.name == "ForestFence" || p.prefab.name == "ForestWoodPile" ) ) )
            {
                foreach( var ( prefab, spot, yaw ) in Dressing( poi, prefabs, scenery ) )
                {
                    cleared += poi.pieces.RemoveAll( p => scenery.Contains( p.prefab ) && Vector2.Distance( p.offset, spot ) < 1.3f );
                    poi.pieces.Add( new PoiSO.Piece { prefab = prefab, offset = spot, rotation = yaw } );
                    dressed++;
                }
            }
            EditorUtility.SetDirty( poi );
            UnityEngine.Debug.Log( $"[HarborForestPropsBuilder] {switched} pieces switched to forest props, {beds} flower bed(s) removed, {dressed} fence/wood pile pieces added, {cleared} scenery trees cleared" );
        }

        // A short rail fence either side of the lane to every house, a wood pile beside the houses.
        private static IEnumerable<(GameObject, Vector2, float)> Dressing( PoiSO poi, Dictionary<string, GameObject> prefabs, HashSet<GameObject> scenery )
        {
            // A forest fence module is one 0.3675 m cell long, drawn at scale 1.6.
            const float module = 1.6f;
            var fence = prefabs[ "ForestFence" ];
            var pile = prefabs[ "ForestWoodPile" ];
            var houses = poi.pieces.Where( p => p.prefab != null && HouseRadius.ContainsKey( p.prefab.name ) && Mathf.Abs( p.offset.y ) > 5f ).ToList();
            var solid = poi.pieces.Where( p => p.prefab != null && !HouseRadius.ContainsKey( p.prefab.name ) && !scenery.Contains( p.prefab ) ).ToList();

            bool Free( Vector2 spot, float distance )
            {
                return solid.All( p => Vector2.Distance( p.offset, spot ) > distance );
            }

            var placed = new List<(GameObject, Vector2, float)>();
            foreach( var house in houses )
            {
                float side = Mathf.Sign( house.offset.y );
                float y = side * 5.7f;
                foreach( float direction in new[] { -1f, 1f } )
                {
                    for( int i = 0; i < 2; i++ )
                    {
                        var spot = new Vector2( house.offset.x + direction * ( 0.9f + module * ( i + 0.5f ) ), y );
                        // Not across the cross lane and the plaza, not into the east end where the forest closes in.
                        if( Mathf.Abs( spot.x - 6f ) < 2.0f || spot.x > 14.5f || !Free( spot, 1.1f ) )
                        {
                            continue;
                        }
                        placed.Add( ( fence, spot, 0f ) );
                    }
                }
                // A wood pile against the side of the house, its ends towards the street.
                var pileSpot = new Vector2( house.offset.x + ( house.offset.x < 6f ? -1f : 1f ) * 2.3f, house.offset.y + side * 0.3f );
                if( Free( pileSpot, 1.2f ) && houses.IndexOf( house ) % 2 == 0 )
                {
                    placed.Add( ( pile, pileSpot, side > 0f ? 0f : 180f ) );
                }
            }
            return placed;
        }
    }
}
