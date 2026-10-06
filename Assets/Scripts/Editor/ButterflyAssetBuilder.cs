using System;
using Hearthglade.Core.Entities;
using Hearthglade.Gameplay.Entities;
using UnityEditor;
using UnityEngine;
using static Hearthglade.EditorTools.AnimalAssetKit;

namespace Hearthglade.EditorTools
{
    /// <summary>Builds the butterflies (model: Tools/Agent Tools/Blender/butterfly.py, three colours): the wing beat clip, the prefabs and their place in the biome tables.</summary>
    public static class ButterflyAssetBuilder {

        private static readonly string[] Variants = { "Orange", "Blue", "Yellow" };

        // Butterflies are out in the meadows by day. (biome asset, most alive of each colour, seconds between spawns)
        private static readonly (string biome, int maxAlive, float cooldown)[] BiomeTables = {
            ( "Meadow", 2, 8f )
        };

        private static AnimalSpec SpecOf( string variant ) {
            return new AnimalSpec {
                Name = "Butterfly" + variant,
                ModelPath = $"Assets/Arts/Models/Animals/Butterfly/Butterfly{variant}.fbx",
                ClipsFolder = "Assets/Arts/Animation/Butterfly",
                PrefabPath = $"Assets/_Prefabs/Entities/Butterfly{variant}.prefab",
                Scale = 0.8f,
                Joints = new[] { "Body", "WingL", "WingR" },
                Clips = new[] { new ClipDefinition { Name = "Fly", Length = 0.25f, Define = Fly } },
                PreviewDirection = new Vector3( 0f, 0.5f, 1f ),
            };
        }

        [ MenuItem( "Tools/Agent Tools/Animals/Build butterflies" ) ]
        public static void Build() {
            foreach( var variant in Variants ) {
                var butterfly = Build<AmbientFlyer>( SpecOf( variant ), ( serialized, built ) => {
                    serialized.FindProperty( "animancer" ).objectReferenceValue = built.Animancer;
                    serialized.FindProperty( "flyClip" ).objectReferenceValue = built.Clips[ "Fly" ];
                    serialized.FindProperty( "movement.speed" ).floatValue = 0.25f;
                    serialized.FindProperty( "movement.wanderRadius" ).floatValue = 1.2f;
                    serialized.FindProperty( "movement.minHeight" ).floatValue = 0.1f;
                    serialized.FindProperty( "movement.maxHeight" ).floatValue = 0.4f;
                } );
                foreach( var ( biome, maxAlive, cooldown ) in BiomeTables ) {
                    EnsureInBiome( biome, butterfly, maxAlive, cooldown, SpawnTime.Day );
                }
            }
            AssetDatabase.SaveAssets();
        }

        [ MenuItem( "Tools/Agent Tools/Animals/Render butterfly preview" ) ]
        public static void RenderButterflyPreview() {
            RenderPreview( SpecOf( Variants[ 0 ] ), Environment.GetEnvironmentVariable( "ANIMAL_PREVIEW" ) ?? "Temp/butterfly_preview.png", 0f, 0.3f );
        }

        // Both wings beat together between above the body and a little below it.
        private static void Fly( AnimalMotion m, float length ) {
            float Beat( float t ) => 20f + 45f * Mathf.Sin( 2f * Mathf.PI * t / length );
            m.Rotate( "WingL", 1, t => Beat( t ) );
            m.Rotate( "WingR", 1, t => -Beat( t ) );
            m.BodyLift = t => 0.006f * Mathf.Sin( 2f * Mathf.PI * t / length + 1.2f );
        }
    }
}
