using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Generator.PerlinNoise;
using Hearthglade.Gameplay.Map.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Lays a point of interest out in a scene instead of in the fields of its PoiSO: the pieces are prefab instances you move, turn and
    // duplicate with the usual tools (snapped to the cells of the map), the paths are two ends and a width, and the island of the map
    // the POI stands on is drawn underneath. The PoiSO stays the source of truth: Load reads it, Save writes it (also on Ctrl+S).
    // See PoiAuthoring for what is drawn; Tools > POI.
    [ InitializeOnLoad ]
    public static class PoiEditorTool
    {
        private const string ScenePath = "Assets/Scenes/Authoring/PoiEditor.unity";
        private const string RootName = "POI layout";
        private const int PreviewSeed = 12345;

        static PoiEditorTool()
        {
            PoiAuthoring.TerrainProvider = ComputeTerrain;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.update += SnapSelection;
        }

        // ---- opening ----

        [ MenuItem( "Tools/POI/Edit selected POI in scene" ) ]
        public static void OpenSelected()
        {
            Open( Selection.activeObject as PoiSO );
        }

        [ MenuItem( "Tools/POI/Edit selected POI in scene", true ) ]
        private static bool OpenSelectedValid()
        {
            return Selection.activeObject is PoiSO;
        }

        public static void Open( PoiSO poi )
        {
            if( poi == null )
            {
                return;
            }
            var open = FindRoot( ActiveScene() );
            if( open != null && IsDirty( open ) && !AskAboutUnsavedLayout( open ) )
            {
                return;
            }
            // The authoring scene is only a working copy of the assets (the layout was dealt with above): other scenes are asked about.
            if( ActiveScene().path != ScenePath && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() )
            {
                return;
            }
            // Opening a scene unloads what nothing references any more, which may be the asset that was handed in: load it again.
            string assetPath = AssetDatabase.GetAssetPath( poi );
            OpenAuthoringScene();
            poi = AssetDatabase.LoadAssetAtPath<PoiSO>( assetPath );
            var root = FindRoot( ActiveScene() ) ?? CreateRoot();
            root.poi = poi;
            root.ForgetTerrain();
            Load( root );
            EditorSceneManager.SaveScene( ActiveScene() );
            Selection.activeGameObject = root.gameObject;
            Frame( root );
        }

        private static UnityEngine.SceneManagement.Scene ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        // The authoring scene is made on first use.
        private static void OpenAuthoringScene()
        {
            if( File.Exists( ScenePath ) )
            {
                EditorSceneManager.OpenScene( ScenePath, OpenSceneMode.Single );
                return;
            }
            Directory.CreateDirectory( Path.GetDirectoryName( ScenePath ) );
            var scene = EditorSceneManager.NewScene( NewSceneSetup.DefaultGameObjects, NewSceneMode.Single );
            EditorSceneManager.SaveScene( scene, ScenePath );
        }

        private static PoiAuthoring CreateRoot()
        {
            var go = new GameObject( RootName );
            return go.AddComponent<PoiAuthoring>();
        }

        private static PoiAuthoring FindRoot( UnityEngine.SceneManagement.Scene scene )
        {
            return scene.IsValid() ? scene.GetRootGameObjects().Select( go => go.GetComponent<PoiAuthoring>() ).FirstOrDefault( a => a != null ) : null;
        }

        public static void Frame( PoiAuthoring root )
        {
            var view = SceneView.lastActiveSceneView;
            if( view == null )
            {
                return;
            }
            view.LookAt( Vector3.zero, Quaternion.Euler( 60f, 0f, 0f ), root.viewRadius * PoiAuthoring.Cell * 0.8f, false, false );
        }

        // ---- load and save ----

        public static void Load( PoiAuthoring root )
        {
            if( root.poi == null )
            {
                return;
            }
            for( int i = root.transform.childCount - 1; i >= 0; i-- )
            {
                Object.DestroyImmediate( root.transform.GetChild( i ).gameObject );
            }
            foreach( var piece in root.poi.pieces )
            {
                if( piece.prefab != null )
                {
                    AddPiece( root, piece.prefab, piece.offset, piece.rotation, false );
                }
            }
            for( int i = 0; i < root.poi.paths.Count; i++ )
            {
                var stripe = root.poi.paths[ i ];
                AddPath( root, stripe.from, stripe.to, stripe.halfWidth, stripe.look, false );
            }
            root.savedLayout = Hash( root );
        }

        public static void Save( PoiAuthoring root )
        {
            var poi = root.poi;
            if( poi == null )
            {
                return;
            }
            Undo.RecordObject( poi, "Save POI layout" );
            poi.pieces = new List<PoiSO.Piece>();
            poi.paths = new List<PoiSO.PathStripe>();
            for( int i = 0; i < root.transform.childCount; i++ )
            {
                var child = root.transform.GetChild( i );
                if( child.TryGetComponent<PoiPathAuthoring>( out var path ) )
                {
                    if( path.From == null || path.To == null )
                    {
                        continue;
                    }
                    poi.paths.Add( new PoiSO.PathStripe
                    {
                        from = Round( PoiAuthoring.ToCells( path.From.position ) ), to = Round( PoiAuthoring.ToCells( path.To.position ) ),
                        halfWidth = path.halfWidth, look = path.look,
                    } );
                    continue;
                }
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource( child.gameObject );
                if( prefab == null )
                {
                    UnityEngine.Debug.LogWarning( $"[PoiEditor] {child.name} is not a prefab instance and is not saved", child );
                    continue;
                }
                poi.pieces.Add( new PoiSO.Piece
                {
                    prefab = prefab,
                    offset = Round( PoiAuthoring.ToCells( child.position ) ),
                    rotation = Mathf.Round( Mathf.Repeat( child.eulerAngles.y - prefab.transform.eulerAngles.y, 360f ) * 10f ) / 10f,
                } );
            }
            EditorUtility.SetDirty( poi );
            AssetDatabase.SaveAssets();
            root.savedLayout = Hash( root );
            UnityEngine.Debug.Log( $"[PoiEditor] Saved {poi.name}: {poi.pieces.Count} piece(s), {poi.paths.Count} path(s)" );
        }

        private static Vector2 Round( Vector2 cells )
        {
            return new Vector2( Mathf.Round( cells.x * 100f ) / 100f, Mathf.Round( cells.y * 100f ) / 100f );
        }

        // What the layout looks like as text: the editor compares it with the one of the last Load or Save to know about unsaved work.
        private static string Hash( PoiAuthoring root )
        {
            var text = new StringBuilder();
            for( int i = 0; i < root.transform.childCount; i++ )
            {
                var child = root.transform.GetChild( i );
                if( child.TryGetComponent<PoiPathAuthoring>( out var path ) && path.From != null && path.To != null )
                {
                    text.Append( $"P{Round( PoiAuthoring.ToCells( path.From.position ) )}{Round( PoiAuthoring.ToCells( path.To.position ) )}{path.halfWidth}{path.look};" );
                }
                else
                {
                    text.Append( $"{child.name}{Round( PoiAuthoring.ToCells( child.position ) )}{Mathf.Round( child.eulerAngles.y )};" );
                }
            }
            return text.ToString();
        }

        public static bool IsDirty( PoiAuthoring root )
        {
            return !string.IsNullOrEmpty( root.savedLayout ) && root.savedLayout != Hash( root );
        }

        private static bool AskAboutUnsavedLayout( PoiAuthoring root )
        {
            int choice = EditorUtility.DisplayDialogComplex( "POI editor", $"The layout of {root.poi.name} has changes that are not saved to the asset.", "Save to asset", "Discard", "Cancel" );
            if( choice == 0 )
            {
                Save( root );
            }
            return choice != 2;
        }

        private static void OnSceneSaving( UnityEngine.SceneManagement.Scene scene, string path )
        {
            var root = FindRoot( scene );
            if( root != null && root.saveToAssetWhenSceneIsSaved && root.poi != null && IsDirty( root ) )
            {
                Save( root );
            }
        }

        // ---- adding things ----

        public static GameObject AddPiece( PoiAuthoring root, GameObject prefab, Vector2 cells, float yaw, bool undo = true )
        {
            var instance = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab, root.transform );
            var euler = prefab.transform.eulerAngles;
            instance.transform.SetPositionAndRotation( PoiAuthoring.ToWorld( cells, prefab.transform.position.y ), Quaternion.Euler( euler.x, euler.y + yaw, euler.z ) );
            instance.name = prefab.name;
            if( undo )
            {
                Undo.RegisterCreatedObjectUndo( instance, "Add POI piece" );
            }
            return instance;
        }

        public static PoiPathAuthoring AddPath( PoiAuthoring root, Vector2 from, Vector2 to, float halfWidth, BiomeId look, bool undo = true )
        {
            var go = new GameObject( $"Path {root.GetComponentsInChildren<PoiPathAuthoring>().Length + 1}" );
            go.transform.SetParent( root.transform, false );
            var path = go.AddComponent<PoiPathAuthoring>();
            path.halfWidth = halfWidth;
            path.look = look;
            NewEnd( go.transform, "From", from );
            NewEnd( go.transform, "To", to );
            if( undo )
            {
                Undo.RegisterCreatedObjectUndo( go, "Add POI path" );
            }
            return path;
        }

        private static void NewEnd( Transform parent, string name, Vector2 cells )
        {
            var end = new GameObject( name );
            end.transform.SetParent( parent, false );
            end.transform.position = PoiAuthoring.ToWorld( cells );
        }

        /// <summary>Prefab instances dragged into the scene land at its root: put them under the POI.</summary>
        public static int AdoptLoosePrefabs( PoiAuthoring root )
        {
            int adopted = 0;
            foreach( var go in root.gameObject.scene.GetRootGameObjects() )
            {
                if( go == root.gameObject || go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null || !PrefabUtility.IsAnyPrefabInstanceRoot( go ) )
                {
                    continue;
                }
                Undo.SetTransformParent( go.transform, root.transform, "Adopt POI piece" );
                adopted++;
            }
            return adopted;
        }

        public static void SnapAll( PoiAuthoring root )
        {
            for( int i = 0; i < root.transform.childCount; i++ )
            {
                var child = root.transform.GetChild( i );
                if( child.TryGetComponent<PoiPathAuthoring>( out var path ) )
                {
                    Snap( root, path.From, false );
                    Snap( root, path.To, false );
                }
                else
                {
                    Snap( root, child, true );
                }
            }
        }

        // ---- snapping while moving ----

        private static void SnapSelection()
        {
            if( Application.isPlaying || Selection.transforms.Length == 0 )
            {
                return;
            }
            foreach( var selected in Selection.transforms )
            {
                var root = selected.GetComponentInParent<PoiAuthoring>();
                if( root == null || selected == root.transform )
                {
                    continue;
                }
                // Only what is placed by cells: the pieces (direct children) and the ends of a path.
                bool piece = selected.parent == root.transform && !selected.TryGetComponent<PoiPathAuthoring>( out _ );
                bool end = selected.parent != null && selected.parent.parent == root.transform && selected.parent.TryGetComponent<PoiPathAuthoring>( out _ );
                if( piece || end )
                {
                    Snap( root, selected, piece );
                }
            }
        }

        private static void Snap( PoiAuthoring root, Transform target, bool turn )
        {
            if( target == null )
            {
                return;
            }
            var position = target.position;
            if( root.snapStep > 0f )
            {
                float step = root.snapStep * PoiAuthoring.Cell;
                position.x = Mathf.Round( position.x / step ) * step;
                position.z = Mathf.Round( position.z / step ) * step;
            }
            var euler = target.eulerAngles;
            if( turn && root.rotationStep > 0f )
            {
                euler.y = Mathf.Round( euler.y / root.rotationStep ) * root.rotationStep;
            }
            if( ( position - target.position ).sqrMagnitude > 1e-10f )
            {
                target.position = position;
            }
            if( turn && Mathf.Abs( Mathf.DeltaAngle( euler.y, target.eulerAngles.y ) ) > 0.01f )
            {
                target.rotation = Quaternion.Euler( euler );
            }
        }

        // ---- the island under the layout ----

        private static PoiTerrain ComputeTerrain( PoiAuthoring authoring )
        {
            var poi = authoring.poi;
            if( poi == null )
            {
                return null;
            }
            var map = AssetDatabase.FindAssets( "t:MapSO" )
                .Select( guid => AssetDatabase.LoadAssetAtPath<MapSO>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .FirstOrDefault( m => m != null && m.perlinNoiseConfig != null && m.perlinNoiseConfig.Pois.Contains( poi ) );
            if( map == null )
            {
                return null;
            }
            var config = map.perlinNoiseConfig;
            int edge = ChunkManager.ChunkSize * map.SizeInChunks;
            var rules = PerlinNoiseGenerator.ToBiomeRules( config );
            var liquid = PerlinNoiseGenerator.ToLiquidFlags( config );
            int seed = MapGenerator.GetMapSeed( PreviewSeed, map.mapName, 0 );
            var terrain = TerrainGenerator.Generate( edge, seed, PerlinNoiseGenerator.ToSettings( config.HeightNoisePreset ),
                PerlinNoiseGenerator.ToSettings( config.TemperatureMapPreset ), PerlinNoiseGenerator.ToSettings( config.HumidityMapPreset ),
                rules, config.ToShape(), liquid, PerlinNoiseGenerator.ToPatches( config ) );
            var field = ResourceField.Build( terrain, seed, liquid, config.ResourceDensityScale );
            var pois = config.Pois.Where( p => p != null ).ToList();
            var sites = PoiPlacer.Place( field, pois.Select( p => p.ToRule( config.Biomes ) ).ToList(), seed );
            int type = pois.IndexOf( poi );
            int siteIndex = sites.ToList().FindIndex( s => s.Type == type );
            if( siteIndex < 0 )
            {
                return null;
            }
            var site = sites[ siteIndex ];

            var result = new PoiTerrain();
            for( int z = -authoring.viewRadius; z <= authoring.viewRadius; z++ )
            {
                int runStart = int.MinValue;
                for( int x = -authoring.viewRadius; x <= authoring.viewRadius + 1; x++ )
                {
                    bool land = false;
                    if( x <= authoring.viewRadius )
                    {
                        PoiLayout.Rotate( site.RotationY, x, z, out float dx, out float dz );
                        int cx = site.CellX + Mathf.RoundToInt( dx );
                        int cz = site.CellY + Mathf.RoundToInt( dz );
                        land = cx >= 0 && cz >= 0 && cx < edge && cz < edge && field.IsGround( cx, cz );
                    }
                    if( land && runStart == int.MinValue )
                    {
                        runStart = x;
                    }
                    else if( !land && runStart != int.MinValue )
                    {
                        result.LandRuns.Add( new Vector3Int( runStart, x - 1, z ) );
                        runStart = int.MinValue;
                    }
                }
            }
            PoiLayout.Rotate( -site.RotationY, field.StartX - site.CellX, field.StartZ - site.CellY, out float sx, out float sz );
            result.StartCells = new Vector2( sx, sz );
            return result;
        }
    }

    [ CustomEditor( typeof( PoiSO ) ) ]
    public class PoiSOInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if( GUILayout.Button( "Edit layout in scene", GUILayout.Height( 28 ) ) )
            {
                PoiEditorTool.Open( ( PoiSO ) target );
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    [ CustomEditor( typeof( PoiAuthoring ) ) ]
    public class PoiAuthoringInspector : UnityEditor.Editor
    {
        private BiomeId newPathLook = BiomeId.Dirt;

        public override void OnInspectorGUI()
        {
            var root = ( PoiAuthoring ) target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if( EditorGUI.EndChangeCheck() )
            {
                root.ForgetTerrain();
                SceneView.RepaintAll();
            }
            if( root.poi == null )
            {
                EditorGUILayout.HelpBox( "Pick a POI asset, or select one in the project and use Tools > POI > Edit selected POI in scene.", UnityEditor.MessageType.Info );
                return;
            }

            EditorGUILayout.Space();
            int paths = root.GetComponentsInChildren<PoiPathAuthoring>().Length;
            EditorGUILayout.LabelField( $"{root.transform.childCount - paths} piece(s), {paths} path(s)" + ( PoiEditorTool.IsDirty( root ) ? "  -  changed, not saved to the asset" : "" ) );
            if( !root.poi.anchored )
            {
                EditorGUILayout.HelpBox( "This POI is placed at random on its map: what is drawn is its own frame (the island below is the one of a sample seed, without the site's random turn).", UnityEditor.MessageType.None );
            }
            if( GUILayout.Button( "Save to POI asset", GUILayout.Height( 26 ) ) )
            {
                PoiEditorTool.Save( root );
            }
            if( GUILayout.Button( "Reload from POI asset" ) && ( !PoiEditorTool.IsDirty( root ) || EditorUtility.DisplayDialog( "POI editor", "Throw away the changes of the layout?", "Reload", "Cancel" ) ) )
            {
                PoiEditorTool.Load( root );
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField( "Add", EditorStyles.boldLabel );
            var picked = ( GameObject ) EditorGUILayout.ObjectField( "Piece (prefab)", null, typeof( GameObject ), false );
            if( picked != null )
            {
                var instance = PoiEditorTool.AddPiece( root, picked, ViewCentreCells(), 0f );
                Selection.activeGameObject = instance;
            }
            using( new EditorGUILayout.HorizontalScope() )
            {
                newPathLook = ( BiomeId ) EditorGUILayout.EnumPopup( newPathLook, GUILayout.Width( 90 ) );
                if( GUILayout.Button( "Path" ) )
                {
                    var centre = ViewCentreCells();
                    Selection.activeGameObject = PoiEditorTool.AddPath( root, centre - Vector2.right * 3f, centre + Vector2.right * 3f, 1f, newPathLook ).gameObject;
                }
                if( GUILayout.Button( "Round plaza" ) )
                {
                    var centre = ViewCentreCells();
                    Selection.activeGameObject = PoiEditorTool.AddPath( root, centre, centre, 3f, newPathLook ).gameObject;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField( "Tools", EditorStyles.boldLabel );
            if( GUILayout.Button( "Adopt prefab instances dragged into the scene" ) )
            {
                int count = PoiEditorTool.AdoptLoosePrefabs( root );
                UnityEngine.Debug.Log( $"[PoiEditor] Adopted {count} prefab instance(s)" );
            }
            if( GUILayout.Button( "Snap everything to the cells" ) )
            {
                Undo.RecordObjects( root.GetComponentsInChildren<Transform>(), "Snap POI" );
                PoiEditorTool.SnapAll( root );
            }
            using( new EditorGUILayout.HorizontalScope() )
            {
                if( GUILayout.Button( "Refresh island" ) )
                {
                    root.ForgetTerrain();
                    SceneView.RepaintAll();
                }
                if( GUILayout.Button( "Frame in Scene view" ) )
                {
                    PoiEditorTool.Frame( root );
                }
            }
        }

        // Where new things go: the middle of the Scene view, on the ground.
        private static Vector2 ViewCentreCells()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? PoiAuthoring.ToCells( view.pivot ) : Vector2.zero;
        }
    }

    [ CustomEditor( typeof( PoiPathAuthoring ) ) ]
    public class PoiPathAuthoringInspector : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var path = ( PoiPathAuthoring ) target;
            var from = path.From;
            var to = path.To;
            if( from == null || to == null )
            {
                return;
            }
            // The ends can be dragged right here; the width is the slider at the middle.
            EditorGUI.BeginChangeCheck();
            var newFrom = Handles.PositionHandle( from.position, Quaternion.identity );
            var newTo = Handles.PositionHandle( to.position, Quaternion.identity );
            if( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObjects( new Object[] { from, to }, "Move POI path" );
                from.position = new Vector3( newFrom.x, from.position.y, newFrom.z );
                to.position = new Vector3( newTo.x, to.position.y, newTo.z );
            }
            var middle = ( from.position + to.position ) / 2f;
            var along = to.position - from.position;
            var side = along.sqrMagnitude > 1e-8f ? Vector3.Cross( Vector3.up, along.normalized ) : Vector3.forward;
            EditorGUI.BeginChangeCheck();
            float width = Handles.ScaleValueHandle( path.halfWidth, middle + side * path.halfWidth * PoiAuthoring.Cell, Quaternion.identity, 0.08f, Handles.CubeHandleCap, 0f );
            if( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObject( path, "Resize POI path" );
                path.halfWidth = Mathf.Max( 0.5f, Mathf.Round( width * 2f ) / 2f );
            }
        }
    }
}
