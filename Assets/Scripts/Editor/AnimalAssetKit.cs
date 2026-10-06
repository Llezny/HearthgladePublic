using System;
using System.Collections.Generic;
using System.IO;
using Animancer;
using Hearthglade.Core.Entities;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    /// <summary>
    /// What every creature built from a Blender model of rigid parts needs (see Tools/Agent Tools/Blender/animal_common.py): import the FBX,
    /// make its clips as transform curves on the joints, build the prefab and render a contact sheet of the clips. The creatures
    /// themselves (<see cref="DeerAssetBuilder"/>, <see cref="HenAssetBuilder"/>, <see cref="ButterflyAssetBuilder"/>) only describe
    /// their motion. Building again overwrites the assets in place, so their GUIDs stay.
    /// </summary>
    public static class AnimalAssetKit {

        private const string MaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";
        private const int ClickableLayer = 8;

        // Positive rotation about the joints' side axis (local X) swings a hanging hoof forwards and tips the nose up.
        public const float Forward = 1f;
        public const float Back = -1f;

        /// <summary>What a clip changes on top of the rest pose: degrees of rotation per joint and axis, and how far the body is lifted (metres).</summary>
        public class AnimalMotion {
            public readonly Dictionary<(string joint, int axis), Func<float, float>> Rotation = new();
            public Func<float, float> BodyLift = _ => 0f;

            public void Rotate( string joint, int axis, Func<float, float> degrees ) {
                Rotation[ ( joint, axis ) ] = degrees;
            }
        }

        public class ClipDefinition {
            public string Name;
            public float Length;
            public Action<AnimalMotion, float> Define;
        }

        public class AnimalSpec {
            public string Name;
            public string ModelPath;
            public string ClipsFolder;
            public string PrefabPath;
            public float Scale;
            public string[] Joints;
            public ClipDefinition[] Clips;

            // A trigger collider so the player can click the animal; a zero size means none (ambient creatures).
            public Vector3 ColliderCenter, ColliderSize;

            // Where the preview camera sits, in the frame of the prefab (+Z is the way the creature looks).
            public Vector3 PreviewDirection = Vector3.right;
        }

        /// <summary>What a built prefab got: the Animancer component and its clips by name, for the tune callback to wire up.</summary>
        public class BuildResult {
            public AnimancerComponent Animancer;
            public Dictionary<string, AnimationClip> Clips;
        }

        /// <summary>Imports the model and builds the clips and the prefab. tune sets the fields of the component on the prefab.</summary>
        public static T Build<T>( AnimalSpec spec, Action<SerializedObject, BuildResult> tune ) where T : Entity {
            ImportModel( spec.ModelPath );
            var model = AssetDatabase.LoadAssetAtPath<GameObject>( spec.ModelPath );

            // The prefab root has no rotation of its own: the game turns it to face where the creature goes. The model
            // (which carries the axis conversion of the FBX) hangs below it.
            var instance = new GameObject( spec.Name );
            instance.transform.localScale = Vector3.one * spec.Scale;
            var modelInstance = ( GameObject ) PrefabUtility.InstantiatePrefab( model );
            PrefabUtility.UnpackPrefabInstance( modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction );
            modelInstance.name = "Model";
            modelInstance.transform.SetParent( instance.transform, false );
            // The model looks along -Z after the import; the game moves creatures along +Z.
            modelInstance.transform.localRotation = Quaternion.Euler( 0f, 180f, 0f ) * modelInstance.transform.localRotation;

            var clips = new Dictionary<string, AnimationClip>();
            foreach( var definition in spec.Clips ) {
                clips[ definition.Name ] = SaveClip( spec, definition.Name, MakeClip( spec, definition, instance.transform ) );
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            foreach( var renderer in instance.GetComponentsInChildren<MeshRenderer>() ) {
                renderer.sharedMaterial = material;
            }

            var animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            var animancer = instance.AddComponent<AnimancerComponent>();
            animancer.Animator = animator;
            var creature = instance.AddComponent<T>();

            if( spec.ColliderSize != Vector3.zero ) {
                SetLayerRecursively( instance, ClickableLayer );
                var collider = instance.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = spec.ColliderCenter;
                collider.size = spec.ColliderSize;
            }

            var serialized = new SerializedObject( creature );
            tune( serialized, new BuildResult { Animancer = animancer, Clips = clips } );
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory( Path.GetDirectoryName( spec.PrefabPath ) );
            var prefab = PrefabUtility.SaveAsPrefabAsset( instance, spec.PrefabPath );
            UnityEngine.Object.DestroyImmediate( instance );
            UnityEngine.Debug.Log( "[AnimalAssetKit] Built " + spec.PrefabPath );
            return prefab.GetComponent<T>();
        }

        /// <summary>Wires the Animancer component and the Idle / Walk / Run clips of a <see cref="ClipAnimal"/>.</summary>
        public static void WireClipAnimal( SerializedObject serialized, BuildResult built ) {
            serialized.FindProperty( "animancer" ).objectReferenceValue = built.Animancer;
            serialized.FindProperty( "clips.idle" ).objectReferenceValue = built.Clips[ "Idle" ];
            serialized.FindProperty( "clips.walk" ).objectReferenceValue = built.Clips[ "Walk" ];
            serialized.FindProperty( "clips.run" ).objectReferenceValue = built.Clips[ "Run" ];
            if( built.Clips.TryGetValue( "Eat", out var eat ) ) {
                serialized.FindProperty( "clips.eat" ).objectReferenceValue = eat;
            }
            serialized.FindProperty( "clips.fadeSeconds" ).floatValue = 0.2f;
        }

        /// <summary>Puts the creature into the entity table of a biome (or updates its entry there).</summary>
        public static void EnsureInBiome( string biomeName, Entity creature, int maxAlive, float cooldownSeconds,
            SpawnTime time = SpawnTime.Always ) {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeSO>( $"Assets/ScriptableObjects/Map/Biomes/{biomeName}.asset" );
            if( biome == null ) {
                throw new InvalidOperationException( "Biome not found: " + biomeName );
            }
            var entry = biome.Entities.Find( e => e.prefab == creature );
            if( entry == null ) {
                entry = new EntitySpawnData();
                biome.Entities.Add( entry );
            }
            entry.prefab = creature;
            entry.maxAlive = maxAlive;
            entry.cooldownSeconds = cooldownSeconds;
            entry.time = time;
            EditorUtility.SetDirty( biome );
        }

        /// <summary>A short bump: rises to peak degrees and falls back within the duration, zero outside.</summary>
        public static float Pulse( float t, float start, float duration, float peak ) {
            float x = ( t - start ) / duration;
            return x <= 0f || x >= 1f ? 0f : peak * Mathf.Sin( Mathf.PI * x );
        }

        private static void ImportModel( string path ) {
            AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( ModelImporter ) AssetImporter.GetAtPath( path );
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.bakeAxisConversion = true;
            importer.isReadable = false;
            importer.generateSecondaryUV = false;
            importer.SaveAndReimport();
        }

        private static AnimationClip MakeClip( AnimalSpec spec, ClipDefinition definition, Transform root ) {
            var motion = new AnimalMotion();
            definition.Define( motion, definition.Length );
            float length = definition.Length;

            var clip = new AnimationClip { name = $"{spec.Name}_{definition.Name}", frameRate = 30f };
            int samples = Mathf.Max( 8, Mathf.CeilToInt( length * 30f ) );

            // Every clip writes every joint (unmoved ones as constants): Animancer does not reset what a clip leaves alone,
            // so a joint missing from Walk would stay bent after a fade to Idle.
            foreach( var joint in spec.Joints ) {
                var node = FindDeep( root, joint );
                if( node == null ) {
                    throw new InvalidOperationException( "Joint not found: " + joint );
                }
                string path = AnimationUtility.CalculateTransformPath( node, root );
                var rest = node.localEulerAngles;
                for( int axis = 0; axis < 3; axis++ ) {
                    motion.Rotation.TryGetValue( ( joint, axis ), out var delta );
                    float restAngle = axis == 0 ? rest.x : axis == 1 ? rest.y : rest.z;
                    var curve = Sample( samples, length, t => restAngle + ( delta != null ? delta( t ) : 0f ) );
                    clip.SetCurve( path, typeof( Transform ), "localEulerAnglesRaw." + "xyz"[ axis ], curve );
                }
                if( joint == "Body" ) {
                    var restPosition = node.localPosition;
                    clip.SetCurve( path, typeof( Transform ), "localPosition.x", Sample( samples, length, _ => restPosition.x ) );
                    clip.SetCurve( path, typeof( Transform ), "localPosition.y", Sample( samples, length, t => restPosition.y + motion.BodyLift( t ) ) );
                    clip.SetCurve( path, typeof( Transform ), "localPosition.z", Sample( samples, length, _ => restPosition.z ) );
                }
            }

            var settings = AnimationUtility.GetAnimationClipSettings( clip );
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings( clip, settings );
            return clip;
        }

        // Evenly spaced keys with smooth tangents; the last key repeats the first so the loop closes.
        private static AnimationCurve Sample( int samples, float length, Func<float, float> value ) {
            var keys = new Keyframe[ samples + 1 ];
            for( int i = 0; i <= samples; i++ ) {
                float t = length * i / samples;
                keys[ i ] = new Keyframe( t, i == samples ? value( 0f ) : value( t ) );
            }
            var curve = new AnimationCurve( keys );
            for( int i = 0; i < keys.Length; i++ ) {
                curve.SmoothTangents( i, 0f );
            }
            return curve;
        }

        private static AnimationClip SaveClip( AnimalSpec spec, string name, AnimationClip clip ) {
            Directory.CreateDirectory( spec.ClipsFolder );
            string path = $"{spec.ClipsFolder}/{spec.Name}_{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>( path );
            if( existing == null ) {
                AssetDatabase.CreateAsset( clip, path );
                return clip;
            }
            EditorUtility.CopySerialized( clip, existing );
            EditorUtility.SetDirty( existing );
            return existing;
        }

        /// <summary>
        /// Renders the clips of the built prefab into one PNG (a row per clip, five frames each). Needs a graphics device,
        /// so run Unity without -nographics. lookAtHeight and viewHeight frame the creature (world units at scale 1).
        /// </summary>
        public static void RenderPreview( AnimalSpec spec, string output, float lookAtHeight, float viewHeight ) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( spec.PrefabPath );
            var scene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );

            var lightObject = new GameObject( "Light" );
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler( 50f, -30f, 0f );
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color( 0.75f, 0.75f, 0.75f );

            var creature = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab, scene );
            var cameraObject = new GameObject( "Camera" );
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = viewHeight * spec.Scale / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color( 0.62f, 0.78f, 0.6f );
            var lookAt = new Vector3( 0f, lookAtHeight * spec.Scale, 0f );
            cameraObject.transform.position = lookAt + spec.PreviewDirection.normalized * 3f;
            cameraObject.transform.LookAt( lookAt );

            const int cellW = 320, cellH = 288, frames = 5;
            var sheet = new Texture2D( cellW * frames, cellH * spec.Clips.Length, TextureFormat.RGB24, false );
            var target = new RenderTexture( cellW, cellH, 24 );
            camera.targetTexture = target;
            camera.aspect = cellW / ( float ) cellH;

            for( int row = 0; row < spec.Clips.Length; row++ ) {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>( $"{spec.ClipsFolder}/{spec.Name}_{spec.Clips[ row ].Name}.anim" );
                for( int col = 0; col < frames; col++ ) {
                    clip.SampleAnimation( creature, clip.length * col / frames );
                    camera.Render();
                    RenderTexture.active = target;
                    var cell = new Texture2D( cellW, cellH, TextureFormat.RGB24, false );
                    cell.ReadPixels( new Rect( 0, 0, cellW, cellH ), 0, 0 );
                    cell.Apply();
                    sheet.SetPixels( col * cellW, ( spec.Clips.Length - 1 - row ) * cellH, cellW, cellH, cell.GetPixels() );
                    UnityEngine.Object.DestroyImmediate( cell );
                }
            }
            RenderTexture.active = null;
            Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( output ) ) );
            File.WriteAllBytes( output, sheet.EncodeToPNG() );
            UnityEngine.Debug.Log( "[AnimalAssetKit] Preview written to " + Path.GetFullPath( output ) );
        }

        private static Transform FindDeep( Transform root, string name ) {
            if( root.name == name ) {
                return root;
            }
            foreach( Transform child in root ) {
                var found = FindDeep( child, name );
                if( found != null ) {
                    return found;
                }
            }
            return null;
        }

        private static void SetLayerRecursively( GameObject target, int layer ) {
            target.layer = layer;
            foreach( Transform child in target.transform ) {
                SetLayerRecursively( child.gameObject, layer );
            }
        }
    }
}
