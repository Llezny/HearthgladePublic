using System.Collections.Generic;
using System.Linq;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Visual;
using Hearthglade.Gameplay.Trade;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Turns the harbour FBX models (Tools/Blender/harbor_props.py) into prefabs and gives the harbour of a port its first layout as a point of interest
    // (docs/EXPLORATION_LOOP_PLAN.md, W4). Prefabs that exist are kept; a layout that exists is kept too (edit it with Tools > POI).
    public static class HarborAssetBuilder
    {
        private const string ModelFolder = "Assets/Arts/Models/Environment/Harbor";
        private const string PrefabFolder = "Assets/_Prefabs/Environment/Harbor";
        private const string MaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";

        // The FBX import turns the models 180 degrees about Y (front +Z, x mirrored); this brings them back to front -Z as laid out below.
        private const float ModelFlip = 180f;

        // How a prop collides: the box is given in the prop's own metres (centre, size). Null = no collider (the player may walk over it).
        internal struct Spec
        {
            public string Name;
            public Vector3? ColliderCenter;
            public Vector3 ColliderSize;
            public float ModelYaw;
            public float Scale;
        }

        // Models are drawn for the player's size 0.3 m; Scale brings them up to it (the walls the player builds are 0.735 m high).
        internal static Spec Solid( string name, Vector3 center, Vector3 size, float scale ) => new Spec { Name = name, ColliderCenter = center, ColliderSize = size, Scale = scale };

        internal static Spec Walkable( string name, float scale = 1f ) => new Spec { Name = name, Scale = scale };

        internal static readonly Spec[] Specs =
        {
            Solid( "HarborStallFruit", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.62f, 0.14f, 0.15f ), 1.7f ),
            Solid( "HarborStallHerbs", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.62f, 0.14f, 0.15f ), 1.7f ),
            Solid( "HarborStallTimber", new Vector3( 0f, 0.07f, -0.07f ), new Vector3( 0.62f, 0.14f, 0.15f ), 1.7f ),
            Solid( "HarborHouseA", new Vector3( 0f, 0.2f, 0f ), new Vector3( 0.72f, 0.4f, 0.56f ), 1.8f ),
            Solid( "HarborHouseB", new Vector3( 0f, 0.2f, 0f ), new Vector3( 0.6f, 0.4f, 0.5f ), 1.8f ),
            Solid( "HarborHouseC", new Vector3( 0f, 0.2f, 0f ), new Vector3( 0.84f, 0.4f, 0.58f ), 1.8f ),
            Solid( "HarborHouseLarge", new Vector3( 0.17f, 0.25f, 0.02f ), new Vector3( 1.34f, 0.5f, 0.7f ), 1.6f ),
            Walkable( "HarborQuay" ),
            Solid( "HarborBollard", new Vector3( 0f, 0.045f, 0f ), new Vector3( 0.07f, 0.09f, 0.07f ), 1.5f ),
            Solid( "HarborCrate", new Vector3( 0f, 0.065f, 0f ), new Vector3( 0.13f, 0.13f, 0.13f ), 1.5f ),
            Solid( "HarborBarrel", new Vector3( 0f, 0.075f, 0f ), new Vector3( 0.13f, 0.15f, 0.13f ), 1.5f ),
            Solid( "HarborSacks", new Vector3( 0f, 0.07f, 0f ), new Vector3( 0.2f, 0.14f, 0.11f ), 1.5f ),
            Solid( "HarborLantern", new Vector3( 0f, 0.17f, 0f ), new Vector3( 0.05f, 0.34f, 0.05f ), 1.6f ),
            Solid( "HarborSignpost", new Vector3( 0f, 0.15f, 0f ), new Vector3( 0.04f, 0.3f, 0.04f ), 1.5f ),
            Solid( "HarborFlowerBed", new Vector3( 0f, 0.03f, 0f ), new Vector3( 0.62f, 0.06f, 0.2f ), 1.2f ),
            Solid( "HarborCart", new Vector3( 0f, 0.1f, 0f ), new Vector3( 0.52f, 0.2f, 0.26f ), 1.4f ),
            Solid( "HarborDryingRack", new Vector3( 0f, 0.15f, 0f ), new Vector3( 0.5f, 0.3f, 0.06f ), 1.6f ),
            Walkable( "HarborBoat", 1.5f ),
        };

        [ MenuItem( "Tools/Ports/Build harbour prefabs" ) ]
        public static void BuildAll()
        {
            Build( false );
        }

        // Puts the layouts of the ports back to the defaults of this file, throwing away what was drawn in the POI editor.
        [ MenuItem( "Tools/Ports/Reset harbour layouts to the defaults" ) ]
        public static void ResetLayouts()
        {
            if( EditorUtility.DisplayDialog( "Harbour layouts", "Replace the pieces and paths of all harbour POIs with the defaults of HarborAssetBuilder? Changes made in the POI editor are lost.", "Reset", "Cancel" ) )
            {
                Build( true );
            }
        }

        private static void Build( bool resetLayouts )
        {
            if( !AssetDatabase.IsValidFolder( PrefabFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/_Prefabs/Environment", "Harbor" );
            }
            ImportModels();
            var prefabs = new Dictionary<string, GameObject>();
            foreach( var spec in Specs )
            {
                prefabs[ spec.Name ] = BuildPrefab( spec );
            }
            foreach( var port in Ports )
            {
                ApplyLayout( prefabs, port, resetLayouts );
            }
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( $"[HarborAssetBuilder] {prefabs.Count} harbour prefabs are ready" );
        }

        internal static void ImportModels( string folder = ModelFolder )
        {
            foreach( var guid in AssetDatabase.FindAssets( "t:Model", new[] { folder } ) )
            {
                var path = AssetDatabase.GUIDToAssetPath( guid );
                if( AssetImporter.GetAtPath( path ) is not ModelImporter importer )
                {
                    continue;
                }
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = false;
                importer.generateSecondaryUV = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
        }

        private static GameObject BuildPrefab( Spec spec )
        {
            string path = $"{PrefabFolder}/{spec.Name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>( path );
            if( existing != null )
            {
                var contents = PrefabUtility.LoadPrefabContents( path );
                Fit( contents, spec );
                PrefabUtility.SaveAsPrefabAsset( contents, path );
                PrefabUtility.UnloadPrefabContents( contents );
                return existing;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>( $"{ModelFolder}/{spec.Name}.fbx" );
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            var root = new GameObject( spec.Name );
            root.AddComponent<PortProp>();
            var visual = ( GameObject ) PrefabUtility.InstantiatePrefab( model, root.transform );
            visual.name = "Model";
            foreach( var renderer in visual.GetComponentsInChildren<Renderer>() )
            {
                renderer.sharedMaterial = material;
            }
            Fit( root, spec );
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, path );
            Object.DestroyImmediate( root );
            return prefab;
        }

        // Orientation, scale and collider of a prop; applied in place so that existing prefabs keep their guids.
        internal static void Fit( GameObject root, Spec spec )
        {
            var visual = root.transform.Find( "Model" );
            visual.localRotation = Quaternion.Euler( 0f, spec.ModelYaw + ModelFlip, 0f );
            visual.localScale = Vector3.one * spec.Scale;
            if( !spec.ColliderCenter.HasValue )
            {
                return;
            }
            var collider = root.TryGetComponent<BoxCollider>( out var existing ) ? existing : root.AddComponent<BoxCollider>();
            collider.center = spec.ColliderCenter.Value * spec.Scale;
            collider.size = spec.ColliderSize * spec.Scale;
        }

        // ---- layout ----

        private struct Placement
        {
            public string Prefab;
            public float X, Z, Yaw;
        }

        private static Placement At( string prefab, float x, float z, float yaw = 0f ) => new Placement { Prefab = prefab, X = x, Z = z, Yaw = yaw };

        // Cells from the centre of the site (x east, z north). The player starts on the west shore, 14 cells west of the centre, and
        // the ship lies 3.8 cells further out. Models face -Z (south) at yaw 0.
        private static IEnumerable<Placement> MosshollowPieces()
        {
            // Boardwalk along the shore and two jetties either side of the ship.
            for( int z = -5; z <= 5; z += 2 )
            {
                yield return At( "HarborQuay", -15.3f, z, 90f );
            }
            foreach( float side in new[] { -3.5f, 3.5f } )
            {
                yield return At( "HarborQuay", -17.5f, side );
                yield return At( "HarborQuay", -19.5f, side );
                yield return At( "HarborBollard", -20.7f, side + 0.7f );
                yield return At( "HarborBollard", -20.7f, side - 0.7f );
            }
            yield return At( "HarborBoat", -19.5f, 6.6f );
            yield return At( "HarborBoat", -20.0f, -6.6f, 8f );
            yield return At( "HarborBollard", -15.3f, 6.5f );
            yield return At( "HarborBollard", -15.3f, -6.5f );
            yield return At( "HarborBarrel", -14.2f, 5.4f );
            yield return At( "HarborBarrel", -13.5f, 5.9f );
            yield return At( "HarborCrate", -14.3f, -5.4f );
            yield return At( "HarborCrate", -13.5f, -5.8f, 25f );
            yield return At( "HarborSacks", -13.4f, -4.4f, 40f );

            // The way in from the ship.
            yield return At( "HarborSignpost", -10.5f, -1.9f );
            yield return At( "HarborLantern", -9f, 1.8f );
            yield return At( "HarborLantern", -3f, -1.8f, 180f );
            yield return At( "HarborFlowerBed", -5.5f, 2.6f );
            yield return At( "HarborFlowerBed", -1.2f, -2.8f, 180f );

            // Market stalls on the north side of the plaza, facing south, with the traders behind the counters.
            yield return At( "HarborStallFruit", 2.4f, 3.4f );
            yield return At( "HarborStallHerbs", 6.0f, 3.4f );
            yield return At( "HarborStallTimber", 9.6f, 3.4f );
            yield return At( "HarborBarrel", 12.0f, 3.0f );
            yield return At( "HarborCrate", 0.0f, 3.0f, 15f );
            yield return At( "HarborCart", 9.5f, -3.4f, 160f );
            yield return At( "HarborSacks", 7.7f, -3.1f, 10f );
            yield return At( "HarborLantern", 2.0f, -2.9f );
            yield return At( "HarborLantern", 11.0f, 1.6f, 90f );

            // Houses: north and south rows face the street, the trading house faces the plaza from the east.
            yield return At( "HarborHouseA", 0.8f, 8.2f );
            yield return At( "HarborHouseC", 11.0f, 8.4f );
            yield return At( "HarborHouseB", 1.0f, -8.0f, 180f );
            yield return At( "HarborHouseA", 11.2f, -8.0f, 180f );
            yield return At( "HarborHouseLarge", 14.0f, 0.0f, 90f );
            yield return At( "HarborDryingRack", 4.0f, 6.0f );
            yield return At( "HarborFlowerBed", 6.2f, -6.2f, 180f );
            yield return At( "HarborLantern", 4.8f, 7.6f );
        }

        // The further ports reuse the layout and the pieces of Mosshollow until they get their own models; they differ in which stall and
        // which house stands where. (Mirroring the layout would turn the stalls away from the camera, which looks from the south.)
        private static readonly string[] Ports = { "Mosshollow", "Rimehaven", "Dunegate" };

        private static readonly Dictionary<string, Dictionary<string, string>> Swaps = new Dictionary<string, Dictionary<string, string>>
        {
            { "Rimehaven", new Dictionary<string, string> {
                { "HarborStallFruit", "HarborStallHerbs" }, { "HarborStallHerbs", "HarborStallFruit" },
                { "HarborHouseA", "HarborHouseC" }, { "HarborHouseC", "HarborHouseB" }, { "HarborHouseB", "HarborHouseA" } } },
            { "Dunegate", new Dictionary<string, string> {
                { "HarborStallFruit", "HarborStallTimber" }, { "HarborStallTimber", "HarborStallFruit" },
                { "HarborHouseA", "HarborHouseB" }, { "HarborHouseB", "HarborHouseC" }, { "HarborHouseC", "HarborHouseA" } } },
        };

        private static void ApplyLayout( Dictionary<string, GameObject> prefabs, string port, bool reset )
        {
            string path = $"Assets/ScriptableObjects/Map/Poi/Harbor_{port}.asset";
            var harbor = AssetDatabase.LoadAssetAtPath<PoiSO>( path );
            if( harbor == null )
            {
                UnityEngine.Debug.LogWarning( $"[HarborAssetBuilder] {path} is missing, no layout for {port} (run the Tools > Ports map builders first)" );
                return;
            }
            // The layout is the work of the POI editor (Tools > POI): the defaults below only fill a POI that has nothing yet.
            if( !reset && ( harbor.pieces.Count > 0 || harbor.paths.Count > 0 ) )
            {
                return;
            }
            string Prefab( string name ) => Swaps.TryGetValue( port, out var swaps ) && swaps.TryGetValue( name, out var swapped ) ? swapped : name;

            harbor.pieces.Clear();
            foreach( var placement in MosshollowPieces() )
            {
                harbor.pieces.Add( new PoiSO.Piece { prefab = prefabs[ Prefab( placement.Prefab ) ], offset = new Vector2( placement.X, placement.Z ), rotation = placement.Yaw } );
            }
            // The traders (Tools > Characters > Build character kit and traders) stand in front of the first two stalls: the awnings
            // would hide them from the camera. They face south, like the stalls.
            var orchardist = AssetDatabase.LoadAssetAtPath<GameObject>( CharacterAssetBuilder.OrchardistPrefabPath );
            var herbalist = AssetDatabase.LoadAssetAtPath<GameObject>( CharacterAssetBuilder.HerbalistPrefabPath );
            if( orchardist != null && herbalist != null )
            {
                harbor.pieces.Add( new PoiSO.Piece { prefab = orchardist, offset = new Vector2( 3.2f, 1.9f ), rotation = 0f } );
                harbor.pieces.Add( new PoiSO.Piece { prefab = herbalist, offset = new Vector2( 6.8f, 1.9f ), rotation = 0f } );
            }
            else
            {
                UnityEngine.Debug.LogWarning( "[HarborAssetBuilder] The trader prefabs are missing, the harbour has no traders (run Tools > Characters > Build character kit and traders first)" );
            }

            harbor.paths.Clear();
            // Dirt street from the shore past the plaza to the trading house, a stone plaza, a cross lane and lanes to the houses.
            harbor.paths.Add( Stripe( -14f, 0f, 12.5f, 0f, 1.2f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( 6f, 0f, 6f, 0f, 4.2f, BiomeId.Stone ) );
            harbor.paths.Add( Stripe( 6f, -9f, 6f, 9f, 1f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( 0.8f, 5f, 0.8f, 7f, 0.8f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( 11f, 5f, 11f, 7f, 0.8f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( 1f, -5f, 1f, -7f, 0.8f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( 11.2f, -5f, 11.2f, -7f, 0.8f, BiomeId.Dirt ) );
            harbor.paths.Add( Stripe( -15.3f, -6f, -15.3f, 6f, 1.3f, BiomeId.Dirt ) ); // under the boardwalk
            EditorUtility.SetDirty( harbor );
        }

        private static PoiSO.PathStripe Stripe( float fromX, float fromZ, float toX, float toZ, float halfWidth, BiomeId look )
        {
            return new PoiSO.PathStripe { from = new Vector2( fromX, fromZ ), to = new Vector2( toX, toZ ), halfWidth = halfWidth, look = look };
        }
    }
}
