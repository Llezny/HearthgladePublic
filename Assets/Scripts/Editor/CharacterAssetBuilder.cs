using System.IO;
using System.Linq;
using Animancer;
using Hearthglade.Gameplay.Characters;
using Hearthglade.Gameplay.Trade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Turns the character kit (Tools/Agent Tools/Blender/character_kit.py -> Character.fbx) into game assets (docs/EXPLORATION_LOOP_PLAN.md, W5):
    // a Humanoid avatar, one CharacterPartSO per part, one CharacterAppearanceSO per outfit and a trader prefab per outfit. The
    // assets that exist are updated in place, so their guids stay.
    public static class CharacterAssetBuilder
    {
        internal const string ModelPath = "Assets/Arts/Models/Characters/Character.fbx";
        internal const string PartFolder = "Assets/ScriptableObjects/Characters/Parts";
        internal const string AppearanceFolder = "Assets/ScriptableObjects/Characters";
        private const string PrefabFolder = "Assets/_Prefabs/Characters";
        internal const string MaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";
        private const string MeshFolder = "Assets/ScriptableObjects/Characters/Meshes";
        private const int ClickableLayer = 8;

        // The model is about 1.2 m tall (taller than the player's proportions), the player 0.3 m.
        private const float CharacterScale = 0.3f;

        public const string OrchardistPrefabPath = PrefabFolder + "/Trader_Orchardist.prefab";
        public const string HerbalistPrefabPath = PrefabFolder + "/Trader_Herbalist.prefab";

        private class Outfit
        {
            public string Name, Title, Prefab, Head, Torso, Shoes;
            // Colour roles painted other than the parts were made (role, row and column of the colour sheet).
            public ( CharacterColorRole role, int row, int column )[] Colors = { };
        }

        private static readonly Outfit[] Outfits =
        {
            new Outfit { Name = "Orchardist", Title = "Orchardist", Prefab = OrchardistPrefabPath, Head = "Head_Orchardist", Torso = "Torso_Orchardist", Shoes = "Shoes_Boots" },
            new Outfit { Name = "Herbalist", Title = "Herbalist", Prefab = HerbalistPrefabPath, Head = "Head_Herbalist", Torso = "Torso_Herbalist", Shoes = "Shoes_Low" },
        };

        [ MenuItem( "Tools/Agent Tools/Characters/Build character kit and traders" ) ]
        public static void BuildAll()
        {
            CharacterClipBuilder.BuildAll();
            ImportModel();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>( ModelPath );
            var parts = BuildParts( model );
            Directory.CreateDirectory( PrefabFolder );
            foreach( var outfit in Outfits )
            {
                var appearance = BuildAppearance( outfit, parts );
                BuildTraderPrefab( model, outfit, appearance );
            }
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( $"[CharacterAssetBuilder] {parts.Count} parts and {Outfits.Length} trader prefabs are ready" );
        }

        private static void ImportModel()
        {
            AssetDatabase.ImportAsset( ModelPath, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( ModelImporter ) AssetImporter.GetAtPath( ModelPath );
            // The avatar keeps the skeleton of the first import in its meta (the bone positions of the T-pose) and the Humanoid animation puts the
            // bones back there while it plays, so a model with other proportions would keep the old ones in the game. Making the avatar anew
            // refreshes it.
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
            importer = ( ModelImporter ) AssetImporter.GetAtPath( ModelPath );
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.isReadable = true; // the colour roles are applied to a copy of the mesh at runtime
            importer.generateSecondaryUV = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath( ModelPath ).OfType<Avatar>().FirstOrDefault();
            if( avatar == null || !avatar.isValid || !avatar.isHuman )
            {
                throw new System.InvalidOperationException( "The character model did not produce a valid Humanoid avatar" );
            }
        }

        // ---- parts and outfits ----

        private static System.Collections.Generic.Dictionary<string, CharacterPartSO> BuildParts( GameObject model )
        {
            Directory.CreateDirectory( PartFolder );
            var parts = new System.Collections.Generic.Dictionary<string, CharacterPartSO>();
            foreach( var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>( true ) )
            {
                var slot = SlotOf( skinned.name );
                if( slot == null )
                {
                    continue;
                }
                string path = $"{PartFolder}/{skinned.name}.asset";
                var part = LoadOrCreate<CharacterPartSO>( path );
                var serialized = new SerializedObject( part );
                serialized.FindProperty( "slot" ).enumValueIndex = ( int ) slot.Value;
                serialized.FindProperty( "mesh" ).objectReferenceValue = skinned.sharedMesh;
                var names = serialized.FindProperty( "boneNames" );
                names.arraySize = skinned.bones.Length;
                for( int i = 0; i < skinned.bones.Length; i++ )
                {
                    names.GetArrayElementAtIndex( i ).stringValue = skinned.bones[ i ].name;
                }
                serialized.FindProperty( "rootBoneName" ).stringValue = skinned.rootBone != null ? skinned.rootBone.name : "";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                parts[ skinned.name ] = part;
            }
            return parts;
        }

        private static CharacterSlot? SlotOf( string partName )
        {
            if( partName.StartsWith( "Head_" ) )
            {
                return CharacterSlot.Head;
            }
            if( partName.StartsWith( "Torso_" ) )
            {
                return CharacterSlot.Torso;
            }
            return partName.StartsWith( "Shoes_" ) ? CharacterSlot.Shoes : null;
        }

        private static CharacterAppearanceSO BuildAppearance( Outfit outfit, System.Collections.Generic.Dictionary<string, CharacterPartSO> parts )
        {
            var appearance = LoadOrCreate<CharacterAppearanceSO>( $"{AppearanceFolder}/{outfit.Name}.asset" );
            var serialized = new SerializedObject( appearance );
            serialized.FindProperty( "head" ).objectReferenceValue = parts[ outfit.Head ];
            serialized.FindProperty( "torso" ).objectReferenceValue = parts[ outfit.Torso ];
            serialized.FindProperty( "shoes" ).objectReferenceValue = parts[ outfit.Shoes ];
            var colors = serialized.FindProperty( "colors" );
            colors.arraySize = outfit.Colors.Length;
            for( int i = 0; i < outfit.Colors.Length; i++ )
            {
                var entry = colors.GetArrayElementAtIndex( i );
                entry.FindPropertyRelative( "role" ).enumValueIndex = ( int ) outfit.Colors[ i ].role;
                entry.FindPropertyRelative( "paletteCell" ).intValue = CharacterPalette.Cell( outfit.Colors[ i ].row, outfit.Colors[ i ].column );
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return appearance;
        }

        private static T LoadOrCreate<T>( string path ) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>( path );
            if( asset == null )
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset( asset, path );
            }
            return asset;
        }

        // ---- the trader prefab ----

        private static void BuildTraderPrefab( GameObject fbx, Outfit outfit, CharacterAppearanceSO appearance )
        {
            var root = new GameObject( Path.GetFileNameWithoutExtension( outfit.Prefab ) );
            root.layer = ClickableLayer;
            var trader = root.AddComponent<Trader>();
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.07f;
            collider.height = 0.36f;
            collider.center = new Vector3( 0f, 0.18f, 0f );

            var model = ( GameObject ) PrefabUtility.InstantiatePrefab( fbx, root.transform );
            PrefabUtility.UnpackPrefabInstance( model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction );
            model.name = "Model";
            model.transform.localScale = Vector3.one * CharacterScale;
            // The model faces +Z; at the yaw of 0 of a piece of the harbour the figures look south like the stalls, towards the camera.
            model.transform.localRotation = Quaternion.Euler( 0f, 180f, 0f );

            // Every slot keeps the renderer of the first outfit; the appearance then dresses it, which is the way any character is put on.
            var first = Outfits[ 0 ];
            var renderers = new SkinnedMeshRenderer[ 3 ];
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            foreach( var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>( true ).ToArray() )
            {
                CharacterSlot? slot = skinned.name == first.Head ? CharacterSlot.Head
                    : skinned.name == first.Torso ? CharacterSlot.Torso
                    : skinned.name == first.Shoes ? CharacterSlot.Shoes : null;
                if( slot == null )
                {
                    Object.DestroyImmediate( skinned.gameObject );
                    continue;
                }
                skinned.gameObject.name = slot.ToString();
                skinned.sharedMaterial = material;
                skinned.gameObject.layer = 0;
                renderers[ ( int ) slot.Value ] = skinned;
            }

            var animator = model.GetComponent<Animator>();
            animator.applyRootMotion = false;
            var animancer = model.AddComponent<AnimancerComponent>();
            animancer.Animator = animator;

            var view = root.AddComponent<CharacterView>();
            var viewSerialized = new SerializedObject( view );
            viewSerialized.FindProperty( "skeleton" ).objectReferenceValue = model.transform;
            viewSerialized.FindProperty( "headRenderer" ).objectReferenceValue = renderers[ ( int ) CharacterSlot.Head ];
            viewSerialized.FindProperty( "torsoRenderer" ).objectReferenceValue = renderers[ ( int ) CharacterSlot.Torso ];
            viewSerialized.FindProperty( "shoesRenderer" ).objectReferenceValue = renderers[ ( int ) CharacterSlot.Shoes ];
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            view.Apply( appearance );
            var appearanceSerialized = new SerializedObject( view );
            appearanceSerialized.FindProperty( "appearance" ).objectReferenceValue = appearance;
            appearanceSerialized.ApplyModifiedPropertiesWithoutUndo();

            BakeRecolouredMeshes( view, Path.GetFileNameWithoutExtension( outfit.Prefab ) );

            var brain = root.AddComponent<NpcBrain>();
            var brainSerialized = new SerializedObject( brain );
            brainSerialized.FindProperty( "animancer" ).objectReferenceValue = animancer;
            brainSerialized.FindProperty( "body" ).objectReferenceValue = model.transform;
            brainSerialized.FindProperty( "idle" ).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>( CharacterClipBuilder.IdleClipPath );
            brainSerialized.FindProperty( "wave" ).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>( CharacterClipBuilder.WavePath );
            brainSerialized.FindProperty( "talk" ).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>( CharacterClipBuilder.TalkPath );
            brainSerialized.ApplyModifiedPropertiesWithoutUndo();

            var traderSerialized = new SerializedObject( trader );
            traderSerialized.FindProperty( "title" ).stringValue = outfit.Title;
            traderSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset( root, outfit.Prefab );
            Object.DestroyImmediate( root );
        }

        // A part painted in other colours than it was made with is a mesh copy made in memory; a prefab needs it as an asset.
        internal static void BakeRecolouredMeshes( CharacterView view, string prefabName )
        {
            var serialized = new SerializedObject( view );
            foreach( var field in new[] { "headRenderer", "torsoRenderer", "shoesRenderer" } )
            {
                var target = ( SkinnedMeshRenderer ) serialized.FindProperty( field ).objectReferenceValue;
                if( target.sharedMesh == null || EditorUtility.IsPersistent( target.sharedMesh ) )
                {
                    continue;
                }
                Directory.CreateDirectory( MeshFolder );
                string path = $"{MeshFolder}/{prefabName}_{target.gameObject.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>( path );
                if( existing == null )
                {
                    AssetDatabase.CreateAsset( target.sharedMesh, path );
                }
                else
                {
                    existing.Clear();
                    EditorUtility.CopySerialized( target.sharedMesh, existing );
                    target.sharedMesh = existing;
                }
            }
        }

        // ---- preview ----

        private class PreviewRow
        {
            public string Prefab;
            public CharacterAppearanceSO Appearance; // null = as the prefab was baked
            public AnimationClip Clip;
        }

        // Both traders, and Orchardist recoloured, from the front, three quarters, side and back in the idle pose. Needs a graphics
        // device. CHARACTER_PREVIEW is the output file.
        [ MenuItem( "Tools/Agent Tools/Characters/Render trader preview" ) ]
        public static void RenderPreview()
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>( CharacterClipBuilder.IdleClipPath );
            var rows = new[]
            {
                new PreviewRow { Prefab = OrchardistPrefabPath, Clip = idle },
                new PreviewRow { Prefab = HerbalistPrefabPath, Clip = idle },
                new PreviewRow { Prefab = OrchardistPrefabPath, Clip = idle, Appearance = RecolouredDemo() },
            };
            var columns = new[] { ( 0f, 0.3f ), ( 40f, 0.3f ), ( 90f, 0.3f ), ( 180f, 0.3f ) };
            RenderSheet( rows, columns );
        }

        // The idle, wave and talk clips at five moments each, seen from the front.
        [ MenuItem( "Tools/Agent Tools/Characters/Render gesture preview" ) ]
        public static void RenderGesturePreview()
        {
            var clips = new[] { CharacterClipBuilder.IdleClipPath, CharacterClipBuilder.WavePath, CharacterClipBuilder.TalkPath }
                .Select( AssetDatabase.LoadAssetAtPath<AnimationClip> ).ToArray();
            var rows = clips.Select( clip => new PreviewRow { Prefab = HerbalistPrefabPath, Clip = clip } ).ToArray();
            var columns = new[] { ( 20f, 0.1f ), ( 20f, 0.3f ), ( 20f, 0.5f ), ( 20f, 0.7f ), ( 20f, 0.9f ) };
            RenderSheet( rows, columns );
        }

        // An appearance of the orchardist's parts in other colours (not saved), to see the colour roles work.
        private static CharacterAppearanceSO RecolouredDemo()
        {
            var demo = ScriptableObject.CreateInstance<CharacterAppearanceSO>();
            demo.hideFlags = HideFlags.HideAndDontSave; // creating the preview scene would otherwise unload it
            var source = AssetDatabase.LoadAssetAtPath<CharacterAppearanceSO>( $"{AppearanceFolder}/Orchardist.asset" );
            var serialized = new SerializedObject( demo );
            serialized.FindProperty( "head" ).objectReferenceValue = source.Head;
            serialized.FindProperty( "torso" ).objectReferenceValue = source.Torso;
            serialized.FindProperty( "shoes" ).objectReferenceValue = source.Shoes;
            var picks = new[]
            {
                ( CharacterColorRole.Skin, CharacterPalette.Cell( 1, 6 ) ), ( CharacterColorRole.Hair, CharacterPalette.Cell( 5, 4 ) ),
                ( CharacterColorRole.Top, CharacterPalette.Cell( 5, 3 ) ), ( CharacterColorRole.TopTrim, CharacterPalette.Cell( 6, 0 ) ),
                ( CharacterColorRole.Legs, CharacterPalette.Cell( 2, 4 ) ), ( CharacterColorRole.Hat, CharacterPalette.Cell( 3, 4 ) ),
                ( CharacterColorRole.HatBand, CharacterPalette.Cell( 5, 0 ) ), ( CharacterColorRole.Shoes, CharacterPalette.Cell( 0, 1 ) ),
            };
            var colors = serialized.FindProperty( "colors" );
            colors.arraySize = picks.Length;
            for( int i = 0; i < picks.Length; i++ )
            {
                var entry = colors.GetArrayElementAtIndex( i );
                entry.FindPropertyRelative( "role" ).enumValueIndex = ( int ) picks[ i ].Item1;
                entry.FindPropertyRelative( "paletteCell" ).intValue = picks[ i ].Item2;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return demo;
        }

        // Rows are figures, columns are (yaw of the camera around the figure, 0 = from the front; moment of the row's clip as a fraction).
        private static void RenderSheet( PreviewRow[] rows, ( float yaw, float time )[] columns )
        {
            string output = System.Environment.GetEnvironmentVariable( "CHARACTER_PREVIEW" );
            if( string.IsNullOrEmpty( output ) )
            {
                output = "Temp/trader_preview.png";
            }
            var scene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
            var lightObject = new GameObject( "Light" );
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler( 50f, -30f, 0f );
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color( 0.75f, 0.75f, 0.75f );

            var cameraObject = new GameObject( "Camera" );
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color( 0.62f, 0.78f, 0.6f );

            const int cell = 300;
            var sheet = new Texture2D( cell * columns.Length, cell * rows.Length, TextureFormat.RGB24, false );
            var target = new RenderTexture( cell, cell, 24 );
            camera.targetTexture = target;
            for( int row = 0; row < rows.Length; row++ )
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( rows[ row ].Prefab );
                for( int col = 0; col < columns.Length; col++ )
                {
                    // A new figure for every picture: the skinning of a mesh is refreshed once per frame and a batch has no frames.
                    var figure = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab, scene );
                    if( rows[ row ].Appearance != null )
                    {
                        figure.GetComponent<CharacterView>().Apply( rows[ row ].Appearance );
                    }
                    var animator = figure.GetComponentInChildren<Animator>();
                    var centre = figure.transform.position + new Vector3( 0f, 0.16f, 0f );
                    var clip = rows[ row ].Clip;
                    clip.SampleAnimation( animator.gameObject, clip.length * columns[ col ].time );
                    // The figures face -Z, so the camera starts on the -Z side.
                    var direction = Quaternion.Euler( 12f, columns[ col ].yaw, 0f ) * new Vector3( 0f, 0f, -1f );
                    cameraObject.transform.position = centre + direction * 3f;
                    cameraObject.transform.LookAt( centre );
                    camera.Render();
                    RenderTexture.active = target;
                    var piece = new Texture2D( cell, cell, TextureFormat.RGB24, false );
                    piece.ReadPixels( new Rect( 0, 0, cell, cell ), 0, 0 );
                    piece.Apply();
                    sheet.SetPixels( col * cell, ( rows.Length - 1 - row ) * cell, cell, cell, piece.GetPixels() );
                    Object.DestroyImmediate( piece );
                    Object.DestroyImmediate( figure );
                }
            }
            RenderTexture.active = null;
            Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( output ) ) );
            File.WriteAllBytes( output, sheet.EncodeToPNG() );
            UnityEngine.Debug.Log( "[CharacterAssetBuilder] Preview written to " + Path.GetFullPath( output ) );
        }
    }
}
