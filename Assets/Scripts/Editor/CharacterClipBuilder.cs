using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Makes the gesture clips of the NPC characters (docs/EXPLORATION_LOOP_PLAN.md, W5): Wave and Talk, as Humanoid clips of muscle curves
    // on top of the idle clip of the player, so every character with the kit's avatar can play them. Building again overwrites the
    // clips in place, so their guids stay.
    public static class CharacterClipBuilder
    {
        public const string IdleClipPath = "Assets/Arts/Animation/Animations/Player/Villager/Villager@Idle01.anim";
        public const string ClipFolder = "Assets/Arts/Animation/Characters";
        public const string WavePath = ClipFolder + "/Character_Wave.anim";
        public const string TalkPath = ClipFolder + "/Character_Talk.anim";

        private const float FrameRate = 30f;

        // What a gesture does to a muscle: its value in time (idle = the value of the idle clip at that time).
        private delegate float Muscle( float time, float idle );

        [ MenuItem( "Tools/Agent Tools/Characters/Build gesture clips" ) ]
        public static void BuildAll()
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>( IdleClipPath );
            Directory.CreateDirectory( ClipFolder );
            Save( WavePath, Make( "Character_Wave", idle, WaveMuscles( idle.length ), false ) );
            Save( TalkPath, Make( "Character_Talk", idle, TalkMuscles(), true ) );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "[CharacterClipBuilder] Wave and Talk are ready" );
        }

        // The right arm comes up with the forearm raised, the forearm swings from side to side, the arm goes down again.
        private static Dictionary<string, Muscle> WaveMuscles( float length )
        {
            float Envelope( float t ) => Mathf.SmoothStep( 0f, 1f, t / 0.4f ) * Mathf.SmoothStep( 1f, 0f, ( t - ( length - 0.5f ) ) / 0.4f );
            float Waggle( float t ) => Mathf.Sin( ( t - 0.4f ) * Mathf.PI * 2f * 2.2f );
            Muscle Blend( float target, float swing = 0f ) => ( t, idle ) => Mathf.Lerp( idle, target + swing * Waggle( t ), Envelope( t ) );
            return new Dictionary<string, Muscle>
            {
                { "Right Arm Down-Up", Blend( 0.9f ) },
                { "Right Arm Twist In-Out", Blend( 0.5f, 0.3f ) },
                { "Right Forearm Stretch", Blend( 0.5f ) },
                { "Right Hand In-Out", Blend( 0f, 0.4f ) },
                { "Head Tilt Left-Right", Blend( 0.1f ) },
            };
        }

        // The head nods and turns a little, the right arm lifts with the forearm up, as if explaining something.
        private static Dictionary<string, Muscle> TalkMuscles()
        {
            float Gesture( float t ) => Mathf.Pow( Mathf.Max( 0f, Mathf.Sin( t * Mathf.PI * 2f * 0.9f ) ), 0.7f );
            return new Dictionary<string, Muscle>
            {
                { "Head Nod Down-Up", ( t, idle ) => idle + 0.15f * Mathf.Sin( t * Mathf.PI * 2f * 1.8f ) },
                { "Head Turn Left-Right", ( t, idle ) => idle + 0.2f * Mathf.Sin( t * Mathf.PI * 2f * 0.9f ) },
                { "Right Arm Down-Up", ( t, idle ) => Mathf.Lerp( idle, 0.45f, Gesture( t ) ) },
                { "Right Arm Twist In-Out", ( t, idle ) => Mathf.Lerp( idle, 0.5f, Gesture( t ) ) },
                { "Right Forearm Stretch", ( t, idle ) => Mathf.Lerp( idle, 0.5f, Gesture( t ) ) },
                { "Right Hand In-Out", ( t, idle ) => idle + 0.3f * Gesture( t ) * Mathf.Sin( t * Mathf.PI * 2f * 3.6f ) },
            };
        }

        private static AnimationClip Make( string name, AnimationClip idle, Dictionary<string, Muscle> muscles, bool loop )
        {
            var clip = new AnimationClip { name = name, frameRate = FrameRate };
            int samples = Mathf.CeilToInt( idle.length * FrameRate );
            var bindings = AnimationUtility.GetCurveBindings( idle ).Where( b => b.type == typeof( Animator ) ).ToList();
            foreach( var muscle in muscles.Keys )
            {
                if( !bindings.Any( b => b.propertyName == muscle ) )
                {
                    bindings.Add( EditorCurveBinding.FloatCurve( "", typeof( Animator ), muscle ) );
                }
            }
            foreach( var binding in bindings )
            {
                var source = AnimationUtility.GetEditorCurve( idle, binding );
                muscles.TryGetValue( binding.propertyName, out var change );
                var keys = new Keyframe[ samples + 1 ];
                for( int i = 0; i <= samples; i++ )
                {
                    float t = Mathf.Min( idle.length * i / samples, idle.length );
                    float rest = source != null ? source.Evaluate( t ) : 0f;
                    keys[ i ] = new Keyframe( t, change != null ? change( t, rest ) : rest );
                }
                var curve = new AnimationCurve( keys );
                for( int i = 0; i < keys.Length; i++ )
                {
                    curve.SmoothTangents( i, 0f );
                }
                AnimationUtility.SetEditorCurve( clip, binding, curve );
            }
            var settings = AnimationUtility.GetAnimationClipSettings( clip );
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings( clip, settings );
            return clip;
        }

        private static void Save( string path, AnimationClip clip )
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>( path );
            if( existing == null )
            {
                AssetDatabase.CreateAsset( clip, path );
                return;
            }
            existing.ClearCurves();
            EditorUtility.CopySerialized( clip, existing );
            EditorUtility.SetDirty( existing );
        }
    }
}
