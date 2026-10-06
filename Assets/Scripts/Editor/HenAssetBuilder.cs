using System;
using Hearthglade.Core.Farming;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEngine;
using static Hearthglade.EditorTools.AnimalAssetKit;

namespace Hearthglade.EditorTools
{
    /// <summary>Builds the hen (model: Tools/Blender/hen.py): its clips, its prefab and its place in the biome tables.</summary>
    public static class HenAssetBuilder {

        private static readonly AnimalSpec Spec = new() {
            Name = "Hen",
            ModelPath = "Assets/Arts/Models/Animals/Hen/Hen.fbx",
            ClipsFolder = "Assets/Arts/Animation/Hen",
            PrefabPath = "Assets/_Prefabs/Entities/Hen.prefab",
            Scale = 0.4f,
            Joints = new[] { "Body", "Neck", "Head", "Tail", "WingL", "WingR", "LegL", "LegR", "FootL", "FootR" },
            Clips = new[] {
                new ClipDefinition { Name = "Idle", Length = 3f, Define = Idle },
                new ClipDefinition { Name = "Walk", Length = 0.7f, Define = Walk },
                new ClipDefinition { Name = "Run", Length = 0.35f, Define = Run },
                new ClipDefinition { Name = "Eat", Length = 2.4f, Define = Eat },
            },
            ColliderCenter = new Vector3( 0f, 0.22f, 0f ),
            ColliderSize = new Vector3( 0.3f, 0.45f, 0.5f ),
        };

        // Meadows and forests. (biome asset, most alive on the map at once, seconds between spawns)
        private static readonly (string biome, int maxAlive, float cooldown)[] BiomeTables = {
            ( "Meadow", 3, 6f ), ( "Forest", 2, 8f )
        };

        [ MenuItem( "LuakszTools/Animals/Build hen" ) ]
        public static void Build() {
            var hen = Build<Hen>( Spec, ( serialized, built ) => {
                WireClipAnimal( serialized, built );
                serialized.FindProperty( "movement.walkSpeed" ).floatValue = 0.2f;
                serialized.FindProperty( "movement.runSpeed" ).floatValue = 0.5f;
                serialized.FindProperty( "movement.wanderRadius" ).floatValue = 2f;
                serialized.FindProperty( "movement.fleeDistance" ).floatValue = 2f;
                serialized.FindProperty( "movement.alertDistance" ).floatValue = 0f;
                serialized.FindProperty( "movement.minRestSeconds" ).floatValue = 1f;
                serialized.FindProperty( "movement.maxRestSeconds" ).floatValue = 10f;
                // Pecks at grain and vegetables of the garden, close by.
                serialized.FindProperty( "foraging.diet" ).intValue = ( int ) ( ForageKind.Grain | ForageKind.Vegetable );
                serialized.FindProperty( "foraging.radius" ).floatValue = 1.5f;
                serialized.FindProperty( "foraging.eatSeconds" ).floatValue = 2.4f;
                serialized.FindProperty( "foraging.forageChance" ).floatValue = 0.5f;
                serialized.FindProperty( "foraging.cooldownSeconds" ).floatValue = 45f;
                serialized.FindProperty( "foraging.blockedCooldownSeconds" ).floatValue = 120f;
            } );
            foreach( var ( biome, maxAlive, cooldown ) in BiomeTables ) {
                EnsureInBiome( biome, hen, maxAlive, cooldown );
            }
            AssetDatabase.SaveAssets();
        }

        [ MenuItem( "LuakszTools/Animals/Render hen preview" ) ]
        public static void RenderHenPreview() {
            RenderPreview( Spec, Environment.GetEnvironmentVariable( "ANIMAL_PREVIEW" ) ?? "Temp/hen_preview.png", 0.22f, 0.6f );
        }

        // Stands, breathes, pecks twice now and then and looks around.
        private static void Idle( AnimalMotion m, float length ) {
            m.BodyLift = t => 0.002f * Mathf.Sin( 2f * Mathf.PI * 2f * t / length );
            m.Rotate( "Neck", 0, t => -Pulse( t, 0.5f, 0.3f, 35f ) - Pulse( t, 1.0f, 0.3f, 35f ) + 3f * Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Head", 0, t => -Pulse( t, 0.5f, 0.3f, 15f ) - Pulse( t, 1.0f, 0.3f, 15f ) );
            m.Rotate( "Head", 1, t => t > 1.6f ? 22f * Mathf.Sin( Mathf.PI * ( t - 1.6f ) / 1.4f ) : 0f );
            m.Rotate( "Tail", 2, t => Pulse( t, 2.4f, 0.4f, 10f ) );
        }

        // Alternating legs, feet kept flat, the head bobbing like a hen's does.
        private static void Walk( AnimalMotion m, float length ) {
            foreach( var ( leg, foot, phase ) in new[] { ( "LegL", "FootL", 0f ), ( "LegR", "FootR", 0.5f ) } ) {
                float Angle( float t ) => 2f * Mathf.PI * ( t / length + phase );
                m.Rotate( leg, 0, t => Forward * 28f * Mathf.Sin( Angle( t ) ) );
                m.Rotate( foot, 0, t => -Forward * 28f * Mathf.Sin( Angle( t ) ) - 22f * Mathf.Max( 0f, Mathf.Cos( Angle( t ) ) ) );
            }
            m.BodyLift = t => 0.006f * Mathf.Abs( Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Neck", 0, t => 9f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
            m.Rotate( "Head", 0, t => -9f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
            m.Rotate( "Tail", 2, t => 6f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
        }

        // Bent over the ground, pecking again and again.
        private static void Eat( AnimalMotion m, float length ) {
            float Peck( float t ) => Mathf.Max( 0f, Mathf.Sin( 2f * Mathf.PI * 4f * t / length ) );
            m.BodyLift = t => 0f;
            m.Rotate( "Body", 0, t => -8f );
            m.Rotate( "Neck", 0, t => -25f - 30f * Peck( t ) );
            m.Rotate( "Head", 0, t => -12f - 14f * Peck( t ) );
            m.Rotate( "Tail", 0, t => 8f );
        }

        // A flapping dash: quick legs, wings out, the neck stretched forward.
        private static void Run( AnimalMotion m, float length ) {
            foreach( var ( leg, foot, phase ) in new[] { ( "LegL", "FootL", 0f ), ( "LegR", "FootR", 0.5f ) } ) {
                float Angle( float t ) => 2f * Mathf.PI * ( t / length + phase );
                m.Rotate( leg, 0, t => Forward * 42f * Mathf.Sin( Angle( t ) ) );
                m.Rotate( foot, 0, t => -Forward * 42f * Mathf.Sin( Angle( t ) ) - 30f * Mathf.Max( 0f, Mathf.Cos( Angle( t ) ) ) );
            }
            m.BodyLift = t => 0.012f * Mathf.Abs( Mathf.Sin( 2f * Mathf.PI * t / length ) );
            m.Rotate( "Body", 0, t => -12f );
            m.Rotate( "Neck", 0, t => -14f );
            m.Rotate( "Head", 0, t => 8f );
            m.Rotate( "WingL", 1, t => 30f + 12f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
            m.Rotate( "WingR", 1, t => -30f - 12f * Mathf.Sin( 4f * Mathf.PI * t / length ) );
            m.Rotate( "Tail", 0, t => 12f );
        }
    }
}
