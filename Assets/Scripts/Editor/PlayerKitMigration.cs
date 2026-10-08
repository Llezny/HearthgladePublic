using System.Linq;
using Animancer;
using Hearthglade.Gameplay.Animation;
using Hearthglade.Gameplay.Characters;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Puts the player on the character kit like the traders (docs/EQUIPMENT_PLAN.md, phase 5): the Mixamo model of the player (one skinned mesh) is replaced by
    // the kit's skeleton with a renderer per slot, so clothing can be swapped part by part. What the old model carried (Animancer, footstep sounds, the
    // audio source) moves to the new one, the tool socket goes into the right hand and ItemEquipper holds the tool there.
    // Run once (batch mode: -executeMethod Hearthglade.EditorTools.PlayerKitMigration.Run); running it again does nothing.
    public static class PlayerKitMigration
    {
        private const string PlayerPrefabPath = "Assets/_Prefabs/Player/Player.prefab";
        private const string OldModelName = "Player";
        private const string NewModelName = "Model";

        // The kit's figure is 1.2 m tall; the old model was about 0.31 m tall in the game.
        private const float ModelScale = 0.26f;

        [ MenuItem( "Tools/Agent Tools/Characters/Move the player onto the character kit" ) ]
        public static void Run()
        {
            var appearance = ClothingAssetBuilder.BuildDefaultAppearance();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>( CharacterAssetBuilder.ModelPath );
            var root = PrefabUtility.LoadPrefabContents( PlayerPrefabPath );
            try
            {
                if( root.transform.Find( NewModelName ) != null )
                {
                    UnityEngine.Debug.Log( "[PlayerKitMigration] the player is on the kit already" );
                    return;
                }
                var old = root.transform.Find( OldModelName );
                if( old == null )
                {
                    throw new System.InvalidOperationException( $"{PlayerPrefabPath} has no child '{OldModelName}'" );
                }
                int modelLayer = old.gameObject.layer;
                int meshLayer = old.GetComponentInChildren<SkinnedMeshRenderer>( true )?.gameObject.layer ?? modelLayer;

                var model = BuildModel( fbx, root.transform, appearance, modelLayer, meshLayer );
                MoveComponents( old.gameObject, model );

                var animator = model.GetComponent<Animator>();
                animator.applyRootMotion = false;
                var animancer = model.GetComponent<AnimancerComponent>();
                animancer.Animator = animator;
                Point( root.GetComponent<PlayerAnimationController>(), "animancerComponent", animancer );

                var hand = ToolPreviewRunner.FindBone( model.transform, "mixamorig:RightHand" );
                var socket = ToolSocket.Attach( hand );
                var holders = new SerializedObject( root.GetComponent<ItemEquipper>() ).FindProperty( "itemHolders" );
                holders.serializedObject.Update();
                holders.arraySize = 1;
                holders.GetArrayElementAtIndex( 0 ).FindPropertyRelative( "item1" ).intValue = ( int ) Hearthglade.Core.Items.SlotType.Hand;
                holders.GetArrayElementAtIndex( 0 ).FindPropertyRelative( "item2" ).objectReferenceValue = socket;
                holders.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                Object.DestroyImmediate( old.gameObject );
                PrefabUtility.SaveAsPrefabAsset( root, PlayerPrefabPath );
                UnityEngine.Debug.Log( "[PlayerKitMigration] the player is on the character kit" );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
            AssetDatabase.SaveAssets();
        }

        // The kit's skeleton with one renderer per slot, dressed in the player's own clothes and carrying the view and the outfit.
        private static GameObject BuildModel( GameObject fbx, Transform parent, CharacterAppearanceSO appearance, int modelLayer, int meshLayer )
        {
            var model = ( GameObject ) PrefabUtility.InstantiatePrefab( fbx, parent );
            PrefabUtility.UnpackPrefabInstance( model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction );
            model.name = NewModelName;
            model.layer = modelLayer;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * ModelScale;

            var material = AssetDatabase.LoadAssetAtPath<Material>( CharacterAssetBuilder.MaterialPath );
            var renderers = new SkinnedMeshRenderer[ 3 ];
            // The first outfit of the kit lends the renderers (the way the traders are made); the appearance then dresses them.
            string[] lent = { "Head_Orchardist", "Torso_Orchardist", "Shoes_Boots" };
            foreach( var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>( true ).ToArray() )
            {
                int slot = System.Array.IndexOf( lent, skinned.name );
                if( slot < 0 )
                {
                    Object.DestroyImmediate( skinned.gameObject );
                    continue;
                }
                skinned.gameObject.name = ( ( CharacterSlot ) slot ).ToString();
                skinned.sharedMaterial = material;
                skinned.gameObject.layer = meshLayer;
                renderers[ slot ] = skinned;
            }

            var view = model.AddComponent<CharacterView>();
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
            CharacterAssetBuilder.BakeRecolouredMeshes( view, "Player" );

            var outfit = model.AddComponent<PlayerOutfit>();
            Point( outfit, "view", view );
            return model;
        }

        // Everything the old model carried except what belongs to the skeleton itself (the transform and the animator).
        private static void MoveComponents( GameObject from, GameObject to )
        {
            foreach( var component in from.GetComponents<Component>() )
            {
                if( component is Transform || component is Animator )
                {
                    continue;
                }
                UnityEditorInternal.ComponentUtility.CopyComponent( component );
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew( to );
            }
        }

        private static void Point( Object target, string field, Object value )
        {
            var serialized = new SerializedObject( target );
            serialized.FindProperty( field ).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
