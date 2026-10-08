using System.IO;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Animation;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Makes the GatherAnimationSet the player reads from Resources (docs/EQUIPMENT_PLAN.md, phase 3): picking up and gathering by hand play the
    // bare hands clips of ToolClipBuilder, every tool group its tool clip. The asset is updated in place, so its guid stays.
    public static class ToolAnimationSetBuilder
    {
        private const string AssetPath = "Assets/Resources/" + GatherAnimationSet.ResourcePath + ".asset";

        // The bare hands loops are played crouched: getting down into them (and up again after) takes this long. The tool loops start standing.
        private const float CrouchFade = 0.4f;
        private const float ToolFade = 0.15f;

        // Everything after a new export of tool_animations.py: the clips, this set and the socket in the player's hand.
        [ MenuItem( "Tools/Agent Tools/Characters/Import tool clips, set and socket" ) ]
        public static void RebuildAll()
        {
            ToolClipBuilder.BuildAll();
            Build();
            ToolSocket.FitToGrip();
        }

        [ MenuItem( "Tools/Agent Tools/Characters/Build gather animation set" ) ]
        public static void Build()
        {
            Directory.CreateDirectory( Path.GetDirectoryName( AssetPath ) );
            var set = AssetDatabase.LoadAssetAtPath<GatherAnimationSet>( AssetPath );
            if( set == null )
            {
                set = ScriptableObject.CreateInstance<GatherAnimationSet>();
                AssetDatabase.CreateAsset( set, AssetPath );
            }
            var serialized = new SerializedObject( set );
            SetMotion( serialized.FindProperty( "pickup" ), ToolClipBuilder.PickupPath, CrouchFade );
            SetMotion( serialized.FindProperty( "hands" ), ToolClipBuilder.HandsHarvestPath, CrouchFade );
            var entries = serialized.FindProperty( "entries" );
            var rows = new ( ToolGroup group, string clip )[]
            {
                ( ToolGroup.Axe, ToolClipBuilder.AxeChopPath ),
                ( ToolGroup.Pickaxe, ToolClipBuilder.PickaxeStrikePath ),
                ( ToolGroup.Sickle, ToolClipBuilder.SickleReapPath ),
                ( ToolGroup.Spear, ToolClipBuilder.SpearThrustPath ),
            };
            entries.arraySize = rows.Length;
            for( int i = 0; i < rows.Length; i++ )
            {
                var entry = entries.GetArrayElementAtIndex( i );
                entry.FindPropertyRelative( "Group" ).enumValueIndex = ( int ) rows[ i ].group;
                entry.FindPropertyRelative( "Clip" ).objectReferenceValue = ToolClipBuilder.Load( rows[ i ].clip );
                entry.FindPropertyRelative( "HitTime" ).floatValue = ToolClipBuilder.Strike;
                entry.FindPropertyRelative( "FadeIn" ).floatValue = ToolFade;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty( set );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( $"[ToolAnimationSetBuilder] picking up, gathering by hand and {rows.Length} tool groups have an animation" );
        }

        private static void SetMotion( SerializedProperty motion, string clipPath, float fadeIn )
        {
            motion.FindPropertyRelative( "Clip" ).objectReferenceValue = ToolClipBuilder.Load( clipPath );
            motion.FindPropertyRelative( "HitTime" ).floatValue = ToolClipBuilder.Strike;
            motion.FindPropertyRelative( "FadeIn" ).floatValue = fadeIn;
        }
    }
}
