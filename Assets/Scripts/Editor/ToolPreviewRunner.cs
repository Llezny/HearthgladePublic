using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace Hearthglade.EditorTools
{
    // Renders a contact sheet of a clip played on a character of the kit with a tool in its hand, so animations and the grip can be judged
    // without playing the game. Run in batch mode WITHOUT -nographics:
    //   Unity.exe -batchmode -quit -projectPath ... -executeMethod Hearthglade.EditorTools.ToolPreviewRunner.Render
    // Settings come from environment variables: TOOL_PREVIEW_TOOL (prefab name, e.g. StoneAxe, empty = no tool), TOOL_PREVIEW_CLIP (asset path of
    // the clip, empty = the idle clip), TOOL_PREVIEW_FRAMES, TOOL_PREVIEW_OUT (png path), TOOL_PREVIEW_VIEW (front / side / back).
    public static class ToolPreviewRunner
    {
        private const string ToolFolder = "Assets/_Prefabs/Items";

        [ MenuItem( "Tools/Agent Tools/Characters/Render tool preview" ) ]
        public static void Render()
        {
            string tool = Env( "TOOL_PREVIEW_TOOL", "" );
            string clipPath = Env( "TOOL_PREVIEW_CLIP", CharacterClipBuilder.IdleClipPath );
            int frames = int.Parse( Env( "TOOL_PREVIEW_FRAMES", "5" ) );
            var variants = Env( "TOOL_SOCKET_EULERS", "" ).Split( new[] { ';' }, StringSplitOptions.RemoveEmptyEntries );
            if( variants.Length > 0 )
            {
                frames = variants.Length;
            }
            // TOOL_PREVIEW_POSES: "Muscle=value,Muscle=value|Muscle=value" - one column per pose, set on top of the idle pose (to find the values of a clip).
            var poses = Env( "TOOL_PREVIEW_POSES", "" ).Split( new[] { '|' }, StringSplitOptions.RemoveEmptyEntries );
            if( poses.Length > 0 )
            {
                frames = poses.Length;
            }
            float startTime = float.Parse( Env( "TOOL_PREVIEW_TIME", "0" ), System.Globalization.CultureInfo.InvariantCulture );
            string outPath = Env( "TOOL_PREVIEW_OUT", "C:/Temp/tool_preview/preview.png" );
            string view = Env( "TOOL_PREVIEW_VIEW", "front" );

            EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
            var lightObject = new GameObject( "Light" );
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler( 45f, 30f, 0f );
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color( 0.6f, 0.6f, 0.62f );

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>( Env( "TOOL_PREVIEW_PREFAB", CharacterAssetBuilder.OrchardistPrefabPath ) );
            var character = ( GameObject ) PrefabUtility.InstantiatePrefab( prefab );
            foreach( var behaviour in character.GetComponentsInChildren<MonoBehaviour>() )
            {
                behaviour.enabled = false;
            }
            var animator = character.GetComponentInChildren<Animator>();

            // TOOL_PREVIEW_WEAR: names of kit parts ("Head_Traveller,Torso_Traveller") put on a character that has a CharacterView.
            var wear = Env( "TOOL_PREVIEW_WEAR", "" );
            var characterView = character.GetComponentInChildren<Hearthglade.Gameplay.Characters.CharacterView>();
            foreach( var partName in wear.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries ) )
            {
                characterView.Wear( AssetDatabase.LoadAssetAtPath<Hearthglade.Gameplay.Characters.CharacterPartSO>( $"{CharacterAssetBuilder.PartFolder}/{partName.Trim()}.asset" ) );
            }
            var hand = FindBone( character.transform, "mixamorig:RightHand" );
            if( hand == null )
            {
                throw new InvalidOperationException( "The character has no mixamorig:RightHand" );
            }
            LogHand( hand );
            Transform socket = null;
            if( !string.IsNullOrEmpty( tool ) )
            {
                ToolSocket.OverrideFromEnvironment();
                socket = ToolSocket.Attach( hand );
                var toolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>( $"{ToolFolder}/{tool}.prefab" );
                var toolObject = ( GameObject ) PrefabUtility.InstantiatePrefab( toolPrefab, socket );
                toolObject.transform.localPosition = Vector3.zero;
                toolObject.transform.localRotation = Quaternion.identity;
            }

            var clip = clipPath.EndsWith( ".fbx" ) ? ToolClipBuilder.Load( clipPath ) : AssetDatabase.LoadAssetAtPath<AnimationClip>( clipPath );
            if( clip == null )
            {
                throw new InvalidOperationException( $"No clip at {clipPath}" );
            }

            // TOOL_PREVIEW_DUMP=1: prints the trunk and root curves of the clip at its middle (to see what the import kept).
            if( Env( "TOOL_PREVIEW_DUMP", "" ) == "1" )
            {
                foreach( var binding in AnimationUtility.GetCurveBindings( clip ) )
                {
                    var name = binding.propertyName;
                    if( name.Contains( "Spine" ) || name.Contains( "Arm" ) || name.Contains( "Shoulder" ) || name.StartsWith( "Root" ) || name.Contains( "Hand" ) )
                    {
                        var curve = AnimationUtility.GetEditorCurve( clip, binding );
                        UnityEngine.Debug.Log( $"[ToolPreviewRunner] {name}: {curve.Evaluate( 0f ):F3} -> {curve.Evaluate( clip.length * 0.5f ):F3}" );
                    }
                }
            }

            var cameraObject = new GameObject( "Camera" );
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.27f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color( 0.62f, 0.78f, 0.62f );
            var target = new Vector3( 0f, 0.2f, 0f );
            Vector3 direction = view == "side" ? new Vector3( -1f, 0.15f, 0f ) : view == "back" ? new Vector3( 0.3f, 0.2f, 1f ) : new Vector3( 0.45f, 0.2f, -1f );
            cameraObject.transform.position = target + direction.normalized * 3f;
            cameraObject.transform.LookAt( target );

            const int size = 320;
            var sheet = new Texture2D( size * frames, size, TextureFormat.RGB24, false );
            var texture = new RenderTexture( size, size, 24 );
            var read = new Texture2D( size, size, TextureFormat.RGB24, false );
            camera.targetTexture = texture;
            // A playable graph evaluates the clip the way the game does, the body's position and orientation included (sampling in the
            // animation mode leaves those out, so a bent trunk would show upright).
            animator.applyRootMotion = false;
            // Out of play mode a skinned mesh does not follow bones moved by a graph unless told to.
            foreach( var skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>() )
            {
                skinned.forceMatrixRecalculationPerRender = true;
            }
            var graph = UnityEngine.Playables.PlayableGraph.Create( "ToolPreview" );
            graph.SetTimeUpdateMode( UnityEngine.Playables.DirectorUpdateMode.Manual );
            var playable = UnityEngine.Animations.AnimationClipPlayable.Create( graph, clip );
            UnityEngine.Animations.AnimationPlayableOutput.Create( graph, "Preview", animator ).SetSourcePlayable( playable );
            graph.Play();
            // The first evaluation and the first render still show the bind pose.
            graph.Evaluate();
            camera.Render();
            // TOOL_PREVIEW_SPEED=1: how fast the right hand and the head move over two cycles (to find pauses, at the seam of the loop too).
            if( Env( "TOOL_PREVIEW_SPEED", "" ) == "1" )
            {
                var rightHand = animator.GetBoneTransform( HumanBodyBones.RightHand );
                var head = animator.GetBoneTransform( HumanBodyBones.Head );
                Vector3 lastHand = default, lastHead = default;
                const int steps = 66;
                var line = new System.Text.StringBuilder( $"[ToolPreviewRunner] looping {clip.isLooping}, speeds (model units per second):" );
                for( int i = 0; i <= steps; i++ )
                {
                    float t = clip.length * 2f * i / steps;
                    playable.SetTime( t );
                    graph.Evaluate();
                    var handAt = animator.transform.InverseTransformPoint( rightHand.position );
                    var headAt = animator.transform.InverseTransformPoint( head.position );
                    if( i > 0 )
                    {
                        float dt = clip.length * 2f / steps;
                        line.Append( $"\n  t {t:F2} ({t / clip.length % 1f:F2}): hand {( handAt - lastHand ).magnitude / dt:F2}, head {( headAt - lastHead ).magnitude / dt:F2}" );
                    }
                    lastHand = handAt;
                    lastHead = headAt;
                }
                UnityEngine.Debug.Log( line.ToString() );
            }
            try
            {
                for( int i = 0; i < frames; i++ )
                {
                    float time = variants.Length > 0 ? startTime : clip.length * i / Mathf.Max( 1, frames );
                    if( variants.Length > 0 && socket != null )
                    {
                        var parts = variants[ i ].Split( ',' );
                        socket.localRotation = Quaternion.Euler( float.Parse( parts[ 0 ], System.Globalization.CultureInfo.InvariantCulture ), float.Parse( parts[ 1 ], System.Globalization.CultureInfo.InvariantCulture ), float.Parse( parts[ 2 ], System.Globalization.CultureInfo.InvariantCulture ) );
                    }
                    playable.SetTime( time );
                    graph.Evaluate();
                    if( poses.Length > 0 )
                    {
                        ApplyPose( animator, poses[ i ] );
                    }
                    camera.Render();
                    RenderTexture.active = texture;
                    read.ReadPixels( new Rect( 0, 0, size, size ), 0, 0 );
                    read.Apply();
                    sheet.SetPixels( i * size, 0, size, size, read.GetPixels() );
                }
            }
            finally
            {
                graph.Destroy();
                RenderTexture.active = null;
            }
            Directory.CreateDirectory( Path.GetDirectoryName( outPath ) );
            File.WriteAllBytes( outPath, sheet.EncodeToPNG() );
            UnityEngine.Debug.Log( $"[ToolPreviewRunner] {frames} frames of {clip.name} ({clip.length:F2}s) with '{tool}' -> {outPath}" );
        }

        private static void ApplyPose( Animator animator, string assignments )
        {
            var handler = new HumanPoseHandler( animator.avatar, animator.transform );
            var pose = new HumanPose();
            handler.GetHumanPose( ref pose );
            foreach( var assignment in assignments.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries ) )
            {
                var parts = assignment.Split( '=' );
                int index = Array.IndexOf( HumanTrait.MuscleName, parts[ 0 ].Trim() );
                if( index < 0 )
                {
                    throw new InvalidOperationException( $"No muscle called '{parts[ 0 ]}'" );
                }
                pose.muscles[ index ] = float.Parse( parts[ 1 ], System.Globalization.CultureInfo.InvariantCulture );
            }
            var rotation = animator.transform.localRotation;
            var position = animator.transform.localPosition;
            handler.SetHumanPose( ref pose );
            animator.transform.localRotation = rotation;
            animator.transform.localPosition = position;
            handler.Dispose();
        }

        private static string Env( string name, string fallback )
        {
            var value = Environment.GetEnvironmentVariable( name );
            return string.IsNullOrEmpty( value ) ? fallback : value;
        }

        public static Transform FindBone( Transform root, string name )
        {
            return root.GetComponentsInChildren<Transform>( true ).FirstOrDefault( t => t.name == name );
        }

        // Prints which way the forearm points in the frame of the hand (the way the fingers point), to work out how a tool has to sit in it.
        private static void LogHand( Transform hand )
        {
            var forearm = hand.parent;
            var along = hand.InverseTransformDirection( ( hand.position - forearm.position ).normalized );
            UnityEngine.Debug.Log( $"[ToolPreviewRunner] the forearm points {along:F2} in the hand frame; hand scale {hand.lossyScale:F3}" );
        }

    }
}
