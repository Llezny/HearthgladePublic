using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.Ship;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.EditorTools
{
    // Builds the data and the menu of the ship tree (docs/EXPLORATION_LOOP_PLAN.md, phase 7): three branches (cheaper trips, a bigger backpack,
    // a keen eye for gathering) and a provisional menu placed in the Game scene next to the ship menu. Existing assets are left as they are.
    public static class ShipTreeBuilder
    {
        private const string NodeFolder = "Assets/Resources/ScriptableObjects/ShipTree";
        private const string MenuPrefabPath = "Assets/_Prefabs/UI/ShipTreeMenu.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [ MenuItem( "Tools/Agent Tools/Expedition/Create ship tree data and menu" ) ]
        public static void BuildAll()
        {
            CreateNodes();
            var prefab = BuildMenuPrefab();
            PlaceMenuInGameScene( prefab );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "[ShipTreeBuilder] Ship tree nodes and menu are ready" );
        }

        // The ore is a small part of the price: the points come from finished expeditions, the ore from what they bring back.
        private static void CreateNodes()
        {
            if( !AssetDatabase.IsValidFolder( NodeFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/Resources/ScriptableObjects", "ShipTree" );
            }
            CreateNode( "Provisions", "Cheaper trips", "Better stowed supplies: every expedition needs less food and wood.", ShipEffect.ExpeditionDiscount, 0,
                ( 3, 0.15f, "Iron", 2 ), ( 5, 0.15f, "Iron", 4 ), ( 8, 0.15f, "Crystal", 2 ) );
            CreateNode( "Hold", "Bigger backpack", "A hold for the crew's bags: a row of slots more in the backpack.", ShipEffect.BackpackRows, 1,
                ( 4, 1f, "Iron", 3 ), ( 7, 1f, "Gold", 2 ), ( 10, 1f, "Crystal", 3 ) );
            CreateNode( "KeenEye", "Keen eye", "The crew knows where the best plants and stones are: gathering sometimes gives one more.", ShipEffect.HarvestBonus, 2,
                ( 3, 0.1f, "Gold", 1 ), ( 6, 0.1f, "Gold", 2 ), ( 9, 0.1f, "Crystal", 2 ) );
        }

        private static void CreateNode( string name, string display, string description, ShipEffect effect, int order, params ( int points, float magnitude, string ore, int count )[] tiers )
        {
            string path = $"{NodeFolder}/{name}.asset";
            if( AssetDatabase.LoadAssetAtPath<ShipNodeSO>( path ) != null )
            {
                return;
            }
            var node = ScriptableObject.CreateInstance<ShipNodeSO>();
            node.displayName = display;
            node.description = description;
            node.effect = effect;
            node.order = order;
            node.tiers = new List<ShipNodeSO.Tier>();
            foreach( var ( points, magnitude, ore, count ) in tiers )
            {
                node.tiers.Add( new ShipNodeSO.Tier { points = points, magnitude = magnitude, ore = new List<Requirement> { new Requirement( ore, count ) } } );
            }
            AssetDatabase.CreateAsset( node, path );
        }

        private static GameObject BuildMenuPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>( MenuPrefabPath );
            if( existing != null )
            {
                return existing;
            }
            var root = PortMenuBuilder.NewRect( "ShipTreeMenu", null );
            PortMenuBuilder.Stretch( root );
            var menu = root.gameObject.AddComponent<ShipTreeMenu>();

            var content = PortMenuBuilder.NewRect( "Content", root );
            PortMenuBuilder.Stretch( content );
            content.anchoredPosition = new Vector2( 0f, -3000f ); // where Menu.OpenMenu slides it in from
            content.gameObject.AddComponent<Image>().color = new Color( 0f, 0f, 0f, 0.55f );

            var panel = PortMenuBuilder.NewRect( "Panel", content );
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2( 0.5f, 0.5f );
            panel.sizeDelta = new Vector2( 366f, 520f );
            panel.localScale = Vector3.one * 2f; // the canvas is 1080 wide: the layout is drawn at phone-design size and scaled up
            panel.gameObject.AddComponent<Image>().color = CraftingPalette.Header;
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset( 10, 10, 10, 10 );
            column.spacing = 6;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var title = PortMenuBuilder.AddLabel( panel, "Title", "Ship upgrades", 20f, CraftingPalette.Ink900, TextAlignmentOptions.Center, 28f );
            var points = PortMenuBuilder.AddLabel( panel, "Points", "", 11f, CraftingPalette.Ink600, TextAlignmentOptions.Center, 16f );
            var rows = PortMenuBuilder.AddRow( panel, flexibleHeight: 1f );
            var list = PortMenuBuilder.AddScroll( rows, "Branches" );
            var buttons = PortMenuBuilder.AddRow( panel, preferredHeight: 40f );
            var close = PortMenuBuilder.AddButton( buttons, "Close", "Close", CraftingPalette.Sand, CraftingPalette.Ink700 );

            var serialized = new SerializedObject( menu );
            PortMenuBuilder.Assign( serialized, "titleText", title );
            PortMenuBuilder.Assign( serialized, "pointsText", points );
            PortMenuBuilder.Assign( serialized, "list", list );
            PortMenuBuilder.Assign( serialized, "closeButton", close );
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset( root.gameObject, MenuPrefabPath );
            Object.DestroyImmediate( root.gameObject );
            return prefab;
        }

        private static void PlaceMenuInGameScene( GameObject menuPrefab )
        {
            var scene = EditorSceneManager.OpenScene( GameScenePath, OpenSceneMode.Single );
            if( Object.FindFirstObjectByType<ShipTreeMenu>( FindObjectsInactive.Include ) != null )
            {
                return;
            }
            var ship = Object.FindFirstObjectByType<ShipMenu>( FindObjectsInactive.Include );
            if( ship == null )
            {
                UnityEngine.Debug.LogError( "[ShipTreeBuilder] No ShipMenu in the Game scene to find the menu canvas by" );
                return;
            }
            var instance = ( GameObject ) PrefabUtility.InstantiatePrefab( menuPrefab, ship.transform.parent );
            instance.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty( scene );
            EditorSceneManager.SaveScene( scene );
        }
    }
}
