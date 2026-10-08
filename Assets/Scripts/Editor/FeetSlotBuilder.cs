using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Adds the boots slot to the equipment panel (docs/EQUIPMENT_PLAN.md, phase 5): a FeetSlot prefab made as a copy of the chest slot and put into
    // the EquipmentView of the Game scene. Does nothing when the slot is there already.
    public static class FeetSlotBuilder
    {
        private const string ChestSlotPath = "Assets/_Prefabs/Inventory/ChestSlot.prefab";
        private const string FeetSlotPath = "Assets/_Prefabs/Inventory/FeetSlot.prefab";

        [ MenuItem( "Tools/Agent Tools/Characters/Add the feet slot to the equipment panel" ) ]
        public static void Run()
        {
            if( AssetDatabase.LoadAssetAtPath<GameObject>( FeetSlotPath ) == null )
            {
                AssetDatabase.CopyAsset( ChestSlotPath, FeetSlotPath );
                AssetDatabase.ImportAsset( FeetSlotPath );
                var contents = PrefabUtility.LoadPrefabContents( FeetSlotPath );
                contents.name = "FeetSlot";
                PrefabUtility.SaveAsPrefabAsset( contents, FeetSlotPath );
                PrefabUtility.UnloadPrefabContents( contents );
            }
            var scene = EditorSceneManager.OpenScene( "Assets/Scenes/Game.unity" );
            var view = Object.FindFirstObjectByType<EquipmentView>( FindObjectsInactive.Include );
            var serialized = new SerializedObject( view );
            bool changed = false;
            var feet = serialized.FindProperty( "feet" );
            if( feet.objectReferenceValue == null )
            {
                feet.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>( FeetSlotPath );
                changed = true;
            }
            var summary = serialized.FindProperty( "protectionText" );
            if( summary.objectReferenceValue == null )
            {
                summary.objectReferenceValue = CreateProtectionText( view.transform );
                changed = true;
            }
            if( !changed )
            {
                UnityEngine.Debug.Log( "[FeetSlotBuilder] the equipment panel is complete already" );
                return;
            }
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty( scene );
            EditorSceneManager.SaveScene( scene );
            UnityEngine.Debug.Log( "[FeetSlotBuilder] the equipment panel has a feet slot and a protection summary" );
        }

        // A line of text under the figure, in the font of the item slots.
        private static TMP_Text CreateProtectionText( Transform panel )
        {
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>( FeetSlotPath );
            var font = slot.GetComponentInChildren<TMP_Text>( true );
            var created = new GameObject( "Protection", typeof( RectTransform ) );
            created.transform.SetParent( panel, false );
            var rect = ( RectTransform ) created.transform;
            rect.anchorMin = rect.anchorMax = new Vector2( 0.5f, 0.5f );
            rect.pivot = new Vector2( 0.5f, 0.5f );
            rect.anchoredPosition = new Vector2( 0f, -272f );
            rect.sizeDelta = new Vector2( 520f, 44f );
            var text = created.AddComponent<TextMeshProUGUI>();
            text.font = font.font;
            text.fontSharedMaterial = font.fontSharedMaterial;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }
    }
}
