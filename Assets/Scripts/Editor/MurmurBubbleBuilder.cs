using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.UI.HUD.Messages;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Hearthglade.EditorTools
{
    // Builds the murmur bubble (see MurmurService): 9-slices Assets/Arts/Sprites/UI/text_bubble_x2.png, makes the MurmurBubble prefab and
    // registers it in the messages dictionary. Safe to run again, it rebuilds the prefab in place.
    //
    // The sprite is 640x168 px with the tail pointing down from x = 320. The stretchable column and row sit in a gap of the dashed
    // stitching, so the tail and the rounded corners are never stretched; a bubble stays at its natural size up to 640 px wide and grows
    // from there. The tail is a fixed distance from the left and bottom edges, which is what the prefab relies on to put the tip at the root.
    public static class MurmurBubbleBuilder
    {
        private const string SpritePath = "Assets/Arts/Sprites/UI/text_bubble_x2.png";
        private const string PrefabPath = "Assets/_Prefabs/UI/Messages/MurmurBubble.prefab";
        private const string DictionaryPath = "Assets/ScriptableObjects/Messages/MessagesDictionary.asset";
        private const string FontSourcePath = "Assets/3rd-Party/TextMesh Pro/Fonts/RedHatDisplay-VariableFont_wght.ttf";
        private const string FontAssetPath = "Assets/Arts/Fonts/RedHatDisplay SDF.asset";

        private const int SpriteWidth = 640;
        private const int SpriteHeight = 168;
        // 9-slice borders in px (left, bottom, right, top); the 1 px stretchable cross lies at x = 544 and y = 72 from the top.
        private static readonly Vector4 SliceBorder = new Vector4( 544f, 95f, 95f, 72f );
        // Tail tip, measured from the bottom-left corner of the sprite.
        private static readonly Vector2 TailTip = new Vector2( 320f, 14f );

        // Bubble size on the 1080x2400 reference canvas, relative to the sprite's pixels.
        private const float BubbleScale = 0.9f;
        // Room between the bubble's edge and its text; the bottom one includes the tail's height.
        private static readonly RectOffset TextPadding = new RectOffset( 60, 60, 34, 64 );

        private const float FaceDilate = 0.4f;
        private const float TitleSize = 30f;
        private const float SubtitleSize = 21f;

        private static readonly Color TitleColor = new Color32( 75, 38, 16, 255 );
        private static readonly Color SubtitleColor = new Color32( 165, 80, 28, 255 );

        [ MenuItem( "Tools/Agent Tools/UI/Build murmur bubble" ) ]
        public static void Build()
        {
            var sprite = ConfigureSprite();
            var font = GetOrCreateFont();
            var prefab = BuildPrefab( sprite, font );
            RegisterInDictionary( prefab );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( "[MurmurBubbleBuilder] Murmur bubble is ready" );
        }

        private static Sprite ConfigureSprite()
        {
            var importer = ( TextureImporter ) AssetImporter.GetAtPath( SpritePath );
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = SliceBorder;
            importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings( settings );
            // Sliced sprites need the full rectangle, a tight mesh would cut into the borders.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings( settings );
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath< Sprite >( SpritePath );
        }

        // The bubble's geometric rounded look needs a font the project had no TMP asset for (RedHat Display ships with TextMesh Pro's fonts).
        // Dynamic, so any glyph the texts need (the Polish letters too) is added to its atlas on demand.
        private static TMP_FontAsset GetOrCreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath< TMP_FontAsset >( FontAssetPath );
            if( existing != null )
            {
                ThickenGlyphs( existing );
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath< Font >( FontSourcePath );
            var font = TMP_FontAsset.CreateFontAsset( source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true );
            AssetDatabase.CreateAsset( font, FontAssetPath );
            font.atlasTexture.name = "RedHatDisplay SDF Atlas";
            AssetDatabase.AddObjectToAsset( font.atlasTexture, font );
            font.material.name = "RedHatDisplay SDF Material";
            AssetDatabase.AddObjectToAsset( font.material, font );
            ThickenGlyphs( font );
            return font;
        }

        // The TTF is a variable font whose default instance is light and TextMesh Pro can't pick another one, so the glyphs are dilated
        // in the font's material to get the bold look of the design.
        private static void ThickenGlyphs( TMP_FontAsset font )
        {
            font.material.SetFloat( ShaderUtilities.ID_FaceDilate, FaceDilate );
            EditorUtility.SetDirty( font.material );
            EditorUtility.SetDirty( font );
        }

        private static GameObject BuildPrefab( Sprite sprite, TMP_FontAsset font )
        {
            var root = NewRect( "MurmurBubble", null );
            root.anchorMin = root.anchorMax = new Vector2( 0.5f, 0.5f );
            root.sizeDelta = Vector2.zero;
            root.localScale = Vector3.one * BubbleScale;
            var canvasGroup = root.gameObject.AddComponent< CanvasGroup >();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Pivot in the bottom-left corner, shifted so that the tail tip lands on the root.
            var bubble = NewRect( "Bubble", root );
            bubble.anchorMin = bubble.anchorMax = new Vector2( 0.5f, 0.5f );
            bubble.pivot = Vector2.zero;
            bubble.anchoredPosition = -TailTip;
            var image = bubble.gameObject.AddComponent< Image >();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var layout = bubble.gameObject.AddComponent< VerticalLayoutGroup >();
            layout.padding = TextPadding;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            var minSize = bubble.gameObject.AddComponent< LayoutElement >();
            minSize.minWidth = SpriteWidth;
            minSize.minHeight = SpriteHeight;
            var fitter = bubble.gameObject.AddComponent< ContentSizeFitter >();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = NewLabel( "Title", bubble, font, TitleSize, TitleColor, FontStyles.Normal );
            var subtitle = NewLabel( "Subtitle", bubble, font, SubtitleSize, SubtitleColor, FontStyles.Normal );
            title.text = "Title";
            subtitle.text = "Subtitle";

            var view = root.gameObject.AddComponent< MurmurBubble >();
            var serialized = new SerializedObject( view );
            serialized.FindProperty( "bubble" ).objectReferenceValue = bubble;
            serialized.FindProperty( "title" ).objectReferenceValue = title;
            serialized.FindProperty( "subtitle" ).objectReferenceValue = subtitle;
            serialized.FindProperty( "canvasGroup" ).objectReferenceValue = canvasGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset( root.gameObject, PrefabPath );
            Object.DestroyImmediate( root.gameObject );
            return prefab;
        }

        private static TextMeshProUGUI NewLabel( string name, RectTransform parent, TMP_FontAsset font, float size, Color color, FontStyles style )
        {
            var rect = NewRect( name, parent );
            var label = rect.gameObject.AddComponent< TextMeshProUGUI >();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            rect.gameObject.AddComponent< LayoutElement >();
            return label;
        }

        private static RectTransform NewRect( string name, RectTransform parent )
        {
            var gameObject = new GameObject( name, typeof( RectTransform ) );
            gameObject.layer = LayerMask.NameToLayer( "UI" );
            var rect = ( RectTransform ) gameObject.transform;
            if( parent != null )
            {
                rect.SetParent( parent, false );
            }
            return rect;
        }

        private static void RegisterInDictionary( GameObject prefab )
        {
            var dictionary = AssetDatabase.LoadAssetAtPath< MessagesDictionarySO >( DictionaryPath );
            var serialized = new SerializedObject( dictionary );
            var messages = serialized.FindProperty( "messages" );
            SerializedProperty entry = null;
            for( int i = 0; i < messages.arraySize; i++ )
            {
                if( messages.GetArrayElementAtIndex( i ).FindPropertyRelative( "item1" ).intValue == ( int ) MessageType.Murmur )
                {
                    entry = messages.GetArrayElementAtIndex( i );
                }
            }
            if( entry == null )
            {
                messages.arraySize++;
                entry = messages.GetArrayElementAtIndex( messages.arraySize - 1 );
                entry.FindPropertyRelative( "item1" ).intValue = ( int ) MessageType.Murmur;
            }
            entry.FindPropertyRelative( "item2" ).objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty( dictionary );
        }
    }
}
