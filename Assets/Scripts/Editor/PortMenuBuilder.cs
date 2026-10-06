using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.Ship;
using Hearthglade.Gameplay.UI.Menu.Trade;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.EditorTools
{
    // Builds the trade menu of the ports (docs/EXPLORATION_LOOP_PLAN.md, W6) as a prefab placed in the Game scene. Existing assets are
    // left as they are. The traders come from CharacterAssetBuilder.
    public static class PortMenuBuilder
    {
        private const string MenuPrefabPath = "Assets/_Prefabs/UI/PortMenu.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [ MenuItem( "Tools/Ports/Build trade menu" ) ]
        public static void BuildAll()
        {
            var menuPrefab = BuildMenuPrefab();
            PlaceMenuInGameScene( menuPrefab );
            global::Editor.EditorScripts.RefreshItemsDatabase();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "[PortMenuBuilder] Trade menu is ready" );
        }

        // ---- menu ----

        private static GameObject BuildMenuPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>( MenuPrefabPath );
            if( existing != null )
            {
                return existing;
            }

            var root = NewRect( "PortMenu", null );
            Stretch( root );
            var menu = root.gameObject.AddComponent<PortMenu>();

            var content = NewRect( "Content", root );
            Stretch( content );
            content.anchoredPosition = new Vector2( 0f, -3000f ); // where Menu.OpenMenu slides it in from
            var shroud = content.gameObject.AddComponent<Image>();
            shroud.color = new Color( 0f, 0f, 0f, 0.55f );

            var panel = NewRect( "Panel", content );
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2( 0.5f, 0.5f );
            panel.sizeDelta = new Vector2( 366f, 640f );
            panel.localScale = Vector3.one * 2f; // the canvas is 1080 wide: the layout is drawn at phone-design size and scaled up
            panel.gameObject.AddComponent<Image>().color = CraftingPalette.Header;
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset( 10, 10, 10, 10 );
            column.spacing = 6;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var title = AddLabel( panel, "Title", "Port", 20f, CraftingPalette.Ink900, TextAlignmentOptions.Center, 28f );
            var relation = AddLabel( panel, "Relation", "", 11f, CraftingPalette.Ink600, TextAlignmentOptions.Center, 16f );

            AddPairOfLabels( panel, "Your goods", "Their goods" );
            var goodsRow = AddRow( panel, flexibleHeight: 1f );
            var yourGoods = AddScroll( goodsRow, "YourGoods" );
            var theirGoods = AddScroll( goodsRow, "TheirGoods" );

            AddPairOfLabels( panel, "You give", "You get" );
            var offerRow = AddRow( panel, flexibleHeight: 0.7f );
            var giveList = AddScroll( offerRow, "Give" );
            var takeList = AddScroll( offerRow, "Take" );

            var balanceFill = AddBalanceBar( panel );
            var hint = AddLabel( panel, "Hint", "", 12f, CraftingPalette.Ink700, TextAlignmentOptions.Center, 18f );

            var buttons = AddRow( panel, preferredHeight: 40f );
            var close = AddButton( buttons, "Close", "Close", CraftingPalette.Sand, CraftingPalette.Ink700 );
            var trade = AddButton( buttons, "Trade", "Swap", CraftingPalette.Amber500, CraftingPalette.OnAmber );

            var serialized = new SerializedObject( menu );
            Assign( serialized, "titleText", title );
            Assign( serialized, "relationText", relation );
            Assign( serialized, "hintText", hint );
            Assign( serialized, "balanceFill", balanceFill );
            Assign( serialized, "yourGoodsList", yourGoods );
            Assign( serialized, "theirGoodsList", theirGoods );
            Assign( serialized, "giveList", giveList );
            Assign( serialized, "takeList", takeList );
            Assign( serialized, "tradeButton", trade );
            Assign( serialized, "closeButton", close );
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset( root.gameObject, MenuPrefabPath );
            Object.DestroyImmediate( root.gameObject );
            return prefab;
        }

        // Phase 6: a short list of the orders of the port under the title, added to the prefab that exists (the scene instance follows).
        [ MenuItem( "Tools/Ports/Add contracts list to the trade menu" ) ]
        public static void AddContractsSection()
        {
            var root = PrefabUtility.LoadPrefabContents( MenuPrefabPath );
            try
            {
                var menu = root.GetComponent<PortMenu>();
                var serialized = new SerializedObject( menu );
                if( serialized.FindProperty( "contractsList" ).objectReferenceValue != null )
                {
                    UnityEngine.Debug.Log( "[PortMenuBuilder] The contracts list is already in the trade menu" );
                    return;
                }
                var panel = ( RectTransform ) root.transform.Find( "Content/Panel" );
                var label = AddLabel( panel, "OrdersLabel", "Orders (tap one to hand it in)", 11f, CraftingPalette.Ink600, TextAlignmentOptions.MidlineLeft, 16f );
                label.fontStyle = FontStyles.Bold;
                var list = AddScroll( panel, "Orders" );
                var scroll = list.parent.parent;
                var element = scroll.GetComponent<LayoutElement>();
                element.flexibleHeight = 0f;
                element.preferredHeight = 100f;
                // Under the title and the trust line.
                label.transform.SetSiblingIndex( 2 );
                scroll.SetSiblingIndex( 3 );
                Assign( serialized, "contractsList", list );
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset( root, MenuPrefabPath );
                UnityEngine.Debug.Log( "[PortMenuBuilder] Added the contracts list to the trade menu" );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }

        private static void PlaceMenuInGameScene( GameObject menuPrefab )
        {
            var scene = EditorSceneManager.OpenScene( GameScenePath, OpenSceneMode.Single );
            if( Object.FindFirstObjectByType<PortMenu>( FindObjectsInactive.Include ) != null )
            {
                return;
            }
            // Next to the other menus: the same canvas as the ship menu.
            var ship = Object.FindFirstObjectByType<ShipMenu>( FindObjectsInactive.Include );
            if( ship == null )
            {
                UnityEngine.Debug.LogError( "[PortMenuBuilder] No ShipMenu in the Game scene to find the menu canvas by" );
                return;
            }
            var instance = ( GameObject ) PrefabUtility.InstantiatePrefab( menuPrefab, ship.transform.parent );
            instance.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty( scene );
            EditorSceneManager.SaveScene( scene );
        }

        // ---- building blocks ----

        internal static RectTransform NewRect( string name, Transform parent )
        {
            var go = new GameObject( name, typeof( RectTransform ) );
            go.transform.SetParent( parent, false );
            return ( RectTransform ) go.transform;
        }

        internal static void Stretch( RectTransform rect )
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void Assign( SerializedObject serialized, string field, Object value )
        {
            serialized.FindProperty( field ).objectReferenceValue = value;
        }

        internal static TextMeshProUGUI AddLabel( RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment, float height )
        {
            var rect = NewRect( name, parent );
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleWidth = 1;
            return label;
        }

        private static void AddPairOfLabels( RectTransform parent, string left, string right )
        {
            var row = AddRow( parent, preferredHeight: 16f );
            foreach( var text in new[] { left, right } )
            {
                var label = AddLabel( row, text, text, 11f, CraftingPalette.Ink600, TextAlignmentOptions.MidlineLeft, 16f );
                label.fontStyle = FontStyles.Bold;
            }
        }

        internal static RectTransform AddRow( RectTransform parent, float preferredHeight = -1f, float flexibleHeight = 0f )
        {
            var rect = NewRect( "Row", parent );
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.flexibleHeight = flexibleHeight;
            return rect;
        }

        // A scrolling list: the returned rect is the one the rows are added to.
        internal static RectTransform AddScroll( RectTransform parent, string name )
        {
            var scroll = NewRect( name, parent );
            scroll.gameObject.AddComponent<Image>().color = CraftingPalette.Cream;
            var element = scroll.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.flexibleHeight = 1;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();

            var viewport = NewRect( "Viewport", scroll );
            Stretch( viewport );
            viewport.gameObject.AddComponent<RectMask2D>();

            var list = NewRect( "List", viewport );
            list.anchorMin = new Vector2( 0f, 1f );
            list.anchorMax = new Vector2( 1f, 1f );
            list.pivot = new Vector2( 0.5f, 1f );
            list.offsetMin = list.offsetMax = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset( 3, 3, 3, 3 );
            layout.spacing = 3;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = list;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            return list;
        }

        private static Image AddBalanceBar( RectTransform parent )
        {
            var bar = NewRect( "Balance", parent );
            bar.gameObject.AddComponent<Image>().color = CraftingPalette.Tan;
            var element = bar.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 14f;
            element.flexibleWidth = 1;
            // The fill is as wide as its share of the bar: PortMenu moves the right anchor.
            var fillRect = NewRect( "Fill", bar );
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2( 0f, 1f );
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = CraftingPalette.Amber500;
            return fill;
        }

        internal static Button AddButton( RectTransform parent, string name, string text, Color color, Color textColor )
        {
            var rect = NewRect( name, parent );
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = AddLabel( rect, "Label", text, 14f, textColor, TextAlignmentOptions.Center, 40f );
            Stretch( ( RectTransform ) label.transform );
            Object.DestroyImmediate( label.GetComponent<LayoutElement>() );
            return button;
        }
    }
}
