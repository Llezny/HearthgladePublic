using System;
using Hearthglade.Core.Farming;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEngine;
using static Hearthglade.EditorTools.AnimalAssetKit;

namespace Hearthglade.EditorTools
{
    /// <summary>Builds the roe deer (model: Tools/Agent Tools/Blender/deer.py): its clips, its prefab and its place in the biome tables.</summary>
    public static class DeerAssetBuilder {

        private static readonly AnimalSpec Spec = new() {
            Name = "Deer",
            ModelPath = "Assets/Arts/Models/Animals/Deer/Deer.fbx",
            ClipsFolder = "Assets/Arts/Animation/Deer",
            PrefabPath = "Assets/_Prefabs/Entities/Deer.prefab",
            Scale = 0.25f,
            Joints = new[] { "Body", "Neck", "Head", "EarL", "EarR", "Tail", "FL_Up", "FL_Low", "FR_Up", "FR_Low", "BL_Up", "BL_Low", "BR_Up", "BR_Low" },
            Clips = new[] {
                new ClipDefinition { Name = "Idle", Length = 3f, Define = Idle },
                new ClipDefinition { Name = "Walk", Length = 1f, Define = Walk },
                new ClipDefinition { Name = "Run", Length = 0.55f, Define = Run },
                new ClipDefinition { Name = "Eat", Length = 3f, Define = Eat },
            },
            ColliderCenter = new Vector3( 0f, 0.5f, 0f ),
            ColliderSize = new Vector3( 0.5f, 1f, 1.1f ),
        };

        // Where deer live: the open, quiet biomes. (biome asset, most alive on the map at once, seconds between spawns)
        private static readonly (string biome, int maxAlive, float cooldown)[] BiomeTables = {
            ( "Meadow", 2, 12f ), ( "Forest", 3, 10f ), ( "Taiga", 2, 12f )
        };

        [ MenuItem( "Tools/Agent Tools/Animals/Build deer" ) ]
        public static void Build() {
            var deer = Build<Deer>( Spec, ( serialized, built ) => {
                WireClipAnimal( serialized, built );
                // A shy animal: it bolts well before the player gets close.
                serialized.FindProperty( "movement.walkSpeed" ).floatValue = 0.18f;
                serialized.FindProperty( "movement.runSpeed" ).floatValue = 0.8f;
                serialized.FindProperty( "movement.wanderRadius" ).floatValue = 1.5f;
                serialized.FindProperty( "movement.fleeDistance" ).floatValue = 1.8f;
                serialized.FindProperty( "movement.alertDistance" ).floatValue = 0.9f;
                serialized.FindProperty( "movement.minRestSeconds" ).floatValue = 2f;
                serialized.FindProperty( "movement.maxRestSeconds" ).floatValue = 9f;
                // Eats anything ripe in the garden, apples and grapes included (they hang low).
                serialized.FindProperty( "foraging.diet" ).intValue = ( int ) ForageKind.Any;
                serialized.FindProperty( "foraging.radius" ).floatValue = 2.5f;
                serialized.FindProperty( "foraging.eatSeconds" ).floatValue = 4f;
                serialized.FindProperty( "foraging.forageChance" ).floatValue = 0.6f;
                serialized.FindProperty( "foraging.cooldownSeconds" ).floatValue = 60f;
                serialized.FindProperty( "foraging.blockedCooldownSeconds" ).floatValue = 120f;
            } );
            foreach( var ( biome, maxAlive, cooldown ) in BiomeTables ) {
                EnsureInBiome( biome, deer, maxAlive, cooldown );
            }
            AssetDatabase.SaveAssets();
        }

        [ MenuItem( "Tools/Agent Tools/Animals/Render deer preview" ) ]
        public static void RenderDeerPreview() {
            RenderPreview( Spec, Environment.GetEnvironmentVariable( "ANIMAL_PREVIEW" ) ?? "Temp/deer_preview.png", 0.55f, 1.3f );
        }

        // Rests, breathes, looks around and flicks an ear and the tail once in a while.
        private static void Idle( AnimalMotion m, float length ) {
            m.BodyLift = t => 0.004f * Mathf.Sin( 2f * Mathf.PI * 2f * t / length );
            m.Rotate( "Neck", 0, t => 3f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Head", 0, t => -4f * Mathf.Sin( 2f * Mathf.PI * t / length + 0.8f ) );
            m.Rotate( "Head", 1, t => 16f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "EarL", 2, t => Pulse( t, 1.2f, 0.25f, 14f ) );
            m.Rotate( "EarR", 2, t => -Pulse( t, 2.1f, 0.25f, 12f ) );
            m.Rotate( "Tail", 2, t => Pulse( t, 2.4f, 0.4f, 12f ) );
        }

        // Head down to the ground, chewing and flicking an ear.
        private static void Eat( AnimalMotion m, float length ) {
            m.BodyLift = t => 0f;
            m.Rotate( "Neck", 0, t => -90f + 3f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Head", 0, t => -8f + 5f * Mathf.Sin( 2f * Mathf.PI * 4f * t / length ) );
            m.Rotate( "EarL", 2, t => Pulse( t, 1.0f, 0.25f, 14f ) );
            m.Rotate( "EarR", 2, t => -Pulse( t, 2.0f, 0.25f, 12f ) );
            m.Rotate( "Tail", 2, t => Pulse( t, 1.6f, 0.4f, 12f ) );
        }

        private static readonly (string leg, float phase, bool hind)[] Legs = {
            ( "FL", 0f, false ), ( "BR", 0f, true ), ( "FR", 0.5f, false ), ( "BL", 0.5f, true )
        };

        // A diagonal gait: front left with back right, then front right with back left.
        private static void Walk( AnimalMotion m, float length ) {
            foreach( var ( leg, phase, hind ) in Legs ) {
                float swingDegrees = hind ? 24f : 22f;
                float kneeDegrees = hind ? 38f : 46f;
                float Angle( float t ) => 2f * Mathf.PI * ( t / length + phase );
                m.Rotate( leg + "_Up", 0, t => Forward * swingDegrees * Mathf.Sin( Angle( t ) ) );
                m.Rotate( leg + "_Low", 0, t => Back * kneeDegrees * Mathf.Max( 0f, Mathf.Cos( Angle( t ) ) ) );
            }
            m.BodyLift = t => 0.012f * Mathf.Cos( 4f * Mathf.PI * t / length );
            m.Rotate( "Body", 2, t => 1.5f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Neck", 0, t => 3f * Mathf.Sin( 4f * Mathf.PI * t / length + 0.6f ) );
            m.Rotate( "Head", 0, t => -3f * Mathf.Sin( 4f * Mathf.PI * t / length + 0.6f ) );
            m.Rotate( "Tail", 2, t => 8f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
        }

        // A bounding run: the front legs reach together, the hind legs push together, the body rocks and stretches.
        private static void Run( AnimalMotion m, float length ) {
            foreach( var ( leg, _, hind ) in Legs ) {
                float phase = hind ? 0.42f : 0f;
                float swingDegrees = hind ? 38f : 42f;
                float kneeDegrees = hind ? 55f : 65f;
                float Angle( float t ) => 2f * Mathf.PI * ( t / length + phase );
                m.Rotate( leg + "_Up", 0, t => Forward * swingDegrees * Mathf.Sin( Angle( t ) ) );
                m.Rotate( leg + "_Low", 0, t => Back * kneeDegrees * Mathf.Max( 0f, Mathf.Cos( Angle( t ) ) ) );
            }
            m.BodyLift = t => 0.045f * Mathf.Abs( Mathf.Sin( Mathf.PI * t / length + 0.4f ) );
            m.Rotate( "Body", 0, t => 6f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Neck", 0, t => -12f + 5f * Mathf.Sin( 2f * Mathf.PI * t / length + 1f ) );
            m.Rotate( "Head", 0, t => 6f - 4f * Mathf.Sin( 2f * Mathf.PI * t / length + 1f ) );
            m.Rotate( "EarL", 1, t => 10f );
            m.Rotate( "EarR", 1, t => -10f );
            m.Rotate( "Tail", 0, t => -25f );
        }
    }
}
