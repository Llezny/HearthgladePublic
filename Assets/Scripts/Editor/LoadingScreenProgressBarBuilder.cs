using Hearthglade.Gameplay.Common.Service;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Adds a bottom-of-screen progress bar (background + fillAmount Image) to LoadingScreenCanvas.prefab
// and wires it to LoadingScreen.progressFillImage. Run from Hearthglade > UI > Add Loading Progress Bar;
// safe to run again (it replaces the bar if one is already there).
public static class LoadingScreenProgressBarBuilder {
    const string CanvasPrefabPath = "Assets/_Prefabs/UI/LoadingScreenCanvas.prefab";

    [ MenuItem( "Hearthglade/UI/Add Loading Progress Bar" ) ]
    public static void Build() {
        var root = PrefabUtility.LoadPrefabContents( CanvasPrefabPath );
        try {
            var loadingScreen = root.GetComponentInChildren< LoadingScreen >( true );
            if( loadingScreen == null ) {
                Debug.LogError( "No LoadingScreen component found under " + CanvasPrefabPath );
                return;
            }

            var canvasRect = ( RectTransform ) root.transform;
            var existing = canvasRect.Find( "ProgressBar" );
            if( existing != null ) {
                Object.DestroyImmediate( existing.gameObject );
            }

            // Bottom-center bar, independent of the spinning icon's own (animator-driven) local
            // coordinate space, so it stays put regardless of the FadeIn/Loading/FadeOut clip.
            var bar = new GameObject( "ProgressBar", typeof( RectTransform ) );
            bar.layer = LayerMask.NameToLayer( "UI" );
            var barRect = ( RectTransform ) bar.transform;
            barRect.SetParent( canvasRect, false );
            barRect.anchorMin = new Vector2( 0.2f, 0f );
            barRect.anchorMax = new Vector2( 0.8f, 0f );
            barRect.pivot = new Vector2( 0.5f, 0f );
            barRect.anchoredPosition = new Vector2( 0, 60 );
            barRect.sizeDelta = new Vector2( 0, 14 );

            var background = bar.AddComponent< Image >();
            background.color = new Color( 1f, 1f, 1f, 0.25f );
            background.raycastTarget = false;

            var fillGO = new GameObject( "Fill", typeof( RectTransform ) );
            fillGO.layer = LayerMask.NameToLayer( "UI" );
            var fillRect = ( RectTransform ) fillGO.transform;
            fillRect.SetParent( barRect, false );
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fill = fillGO.AddComponent< Image >();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = ( int ) Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = Color.white;
            fill.raycastTarget = false;

            var so = new SerializedObject( loadingScreen );
            so.FindProperty( "progressFillImage" ).objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset( root, CanvasPrefabPath );
            Debug.Log( "Added the loading progress bar to " + CanvasPrefabPath );
        }
        finally {
            PrefabUtility.UnloadPrefabContents( root );
        }
    }
}
