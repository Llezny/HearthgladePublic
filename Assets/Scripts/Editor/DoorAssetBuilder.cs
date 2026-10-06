using System.IO;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Items.BuildableItems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    /// <summary>
    /// Rebuilds WoodenDoor.prefab from the split frame/leaf FBX (Tools/Agent Tools/Blender/building_wood_poc.py) so the
    /// leaf can swing open on interaction while the frame (posts + lintel) stays fixed in the wall opening.
    /// Edits the existing prefab's root in place (PrefabUtility.LoadPrefabContents) instead of building a new
    /// GameObject from scratch, so the root's fileID - and with it WoodenDoor.asset's buildingPrefab reference
    /// - survives the rebuild.
    /// </summary>
    public static class DoorAssetBuilder {

        private const string FramePath = "Assets/Arts/Models/Environment/WoodenDoorFrame.fbx";
        private const string LeafPath = "Assets/Arts/Models/Environment/WoodenDoorLeaf.fbx";
        private const string PrefabPath = "Assets/_Prefabs/Environment/Structures/WoodenDoor.prefab";
        private const string MaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";

        // Same building-grid constants as Tools/Agent Tools/Blender/building_wood_poc.py.
        private const float Cell = 0.3675f;
        private const float PostW = Cell * 0.14f;
        private const float HingeX = -( Cell / 2 - PostW );

        [ MenuItem( "Tools/Agent Tools/Building/Rebuild WoodenDoor prefab" ) ]
        public static void Build() {
            ImportModel( FramePath );
            ImportModel( LeafPath );

            var frameMesh = LoadMesh( FramePath );
            var leafMesh = LoadMesh( LeafPath );
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );

            var root = PrefabUtility.LoadPrefabContents( PrefabPath );
            try {
                root.GetComponent<MeshFilter>().sharedMesh = frameMesh;
                root.GetComponent<MeshRenderer>().sharedMaterial = material;

                // Was a solid BoxCollider blocking the whole cell (the door was always "modelled shut").
                // Now it only needs to detect clicks/proximity - the leaf's own collider does the blocking,
                // and moves with it when the door swings open.
                var rootCollider = root.GetComponent<BoxCollider>();
                rootCollider.isTrigger = true;

                var oldPiece = root.GetComponent<BuildingPiece>();
                if( oldPiece != null ) {
                    Object.DestroyImmediate( oldPiece, true );
                }
                var door = GetOrAddComponent<Door>( root );

                var leafTransform = root.transform.Find( "Leaf" );
                var leaf = leafTransform != null ? leafTransform.gameObject : CreateLeafObject( root );
                leaf.transform.localPosition = new Vector3( HingeX, 0, 0 );
                // Matches DoorAnimation's closedRotation default so the prefab already looks "closed" in the
                // Scene view, not just at runtime (DoorAnimation.Awake also snaps to this on spawn).
                leaf.transform.localRotation = Quaternion.Euler( ClosedRotation );
                leaf.GetComponent<MeshFilter>().sharedMesh = leafMesh;
                leaf.GetComponent<MeshRenderer>().sharedMaterial = material;

                var leafCollider = GetOrAddComponent<BoxCollider>( leaf );
                leafCollider.isTrigger = false;
                // Same box the old fused mesh used (full cell width), just re-centred on the leaf's own
                // hinge-relative origin instead of the cell's centre.
                leafCollider.size = new Vector3( Cell, 0.735f, 0.042f );
                leafCollider.center = new Vector3( Cell / 2 - PostW, 0.3675f, 0 );

                var doorAnimation = GetOrAddComponent<DoorAnimation>( root );
                var animationSO = new SerializedObject( doorAnimation );
                animationSO.FindProperty( "doorLeafTransform" ).objectReferenceValue = leaf.transform;
                animationSO.FindProperty( "doorLeafCollider" ).objectReferenceValue = leafCollider;
                animationSO.ApplyModifiedPropertiesWithoutUndo();

                var doorSO = new SerializedObject( door );
                doorSO.FindProperty( "doorAnimation" ).objectReferenceValue = doorAnimation;
                doorSO.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset( root, PrefabPath );
            }
            finally {
                PrefabUtility.UnloadPrefabContents( root );
            }
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "WoodenDoor.prefab rebuilt with a hinged leaf." );
        }

        /// <summary>Renders the door closed and open side by side, so the hinge/frame split can be checked
        /// without the game view. Needs a graphics device, so run Unity without -nographics.</summary>
        [ MenuItem( "Tools/Agent Tools/Building/Render WoodenDoor preview" ) ]
        public static void RenderPreview() {
            RenderPreview( System.Environment.GetEnvironmentVariable( "DOOR_PREVIEW" ) ?? "Temp/door_preview.png" );
        }

        public static void RenderPreview( string output ) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( PrefabPath );
            var scene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );

            var lightObject = new GameObject( "Light" );
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler( 50f, -30f, 0f );
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color( 0.75f, 0.75f, 0.75f );

            var door = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab, scene );
            var leaf = door.transform.Find( "Leaf" );

            var cameraObject = new GameObject( "Camera" );
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = HEIGHT_FOR_VIEW;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color( 0.62f, 0.78f, 0.6f );
            var lookAt = new Vector3( 0f, HEIGHT_FOR_VIEW * 0.6f, 0f );
            cameraObject.transform.position = lookAt + new Vector3( 0.5f, 0.3f, -1f ).normalized * 1.2f;
            cameraObject.transform.LookAt( lookAt );

            const int cellW = 480, cellH = 480;
            var sheet = new Texture2D( cellW * 2, cellH, TextureFormat.RGB24, false );
            var target = new RenderTexture( cellW, cellH, 24 );
            camera.targetTexture = target;
            camera.aspect = cellW / ( float ) cellH;

            void RenderCell( int col ) {
                camera.Render();
                RenderTexture.active = target;
                var cell = new Texture2D( cellW, cellH, TextureFormat.RGB24, false );
                cell.ReadPixels( new Rect( 0, 0, cellW, cellH ), 0, 0 );
                cell.Apply();
                sheet.SetPixels( col * cellW, 0, cellW, cellH, cell.GetPixels() );
                Object.DestroyImmediate( cell );
            }

            leaf.localRotation = Quaternion.Euler( ClosedRotation );
            RenderCell( 0 );
            leaf.localRotation = Quaternion.Euler( OpenRotation );
            RenderCell( 1 );

            RenderTexture.active = null;
            Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( output ) ) );
            File.WriteAllBytes( output, sheet.EncodeToPNG() );
            UnityEngine.Debug.Log( "[DoorAssetBuilder] Preview written to " + Path.GetFullPath( output ) );
        }

        // Same values DoorAnimation's closedRotation/openRotation default to - kept here too since those
        // fields are private (and the leaf's baked-in rotation needs to match, see Build() above).
        private static readonly Vector3 ClosedRotation = new Vector3( 0, 180, 0 );
        private static readonly Vector3 OpenRotation = new Vector3( 0, 60, 0 );
        private const float HEIGHT_FOR_VIEW = 0.5f;

        // GetComponent(...) ?? AddComponent(...) is unreliable on UnityEngine.Object: the null-coalescing
        // operator's null test bypasses Unity's overloaded equality, so an explicit check is needed here.
        private static T GetOrAddComponent<T>( GameObject go ) where T : Component {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static GameObject CreateLeafObject( GameObject root ) {
            // Layer 0 (Default), not root's Clickable layer: the leaf's own solid collider would otherwise
            // compete with the root's trigger collider for click raycasts (both occupy the same space when
            // closed) and could shadow it, same convention as Chest's "Lid" child.
            var leaf = new GameObject( "Leaf" );
            leaf.transform.SetParent( root.transform, false );
            leaf.AddComponent<MeshFilter>();
            leaf.AddComponent<MeshRenderer>();
            return leaf;
        }

        private static Mesh LoadMesh( string path ) {
            foreach( var asset in AssetDatabase.LoadAllAssetsAtPath( path ) ) {
                if( asset is Mesh mesh ) {
                    return mesh;
                }
            }
            throw new System.InvalidOperationException( "No mesh found in " + path );
        }

        private static void ImportModel( string path ) {
            AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( ModelImporter ) AssetImporter.GetAtPath( path );
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.generateSecondaryUV = false;
            importer.SaveAndReimport();
        }
    }
}
