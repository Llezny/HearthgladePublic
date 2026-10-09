using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.Environment;
using Lean.Touch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    /// <summary>
    /// The view of a murmur: a small speech bubble that floats over the player's head with a title and an optional subtitle.
    /// Lives on a screen-space canvas (so trees can't hide it and it stays crisp) and follows a world point every frame.
    /// The root sits exactly on the tip of the bubble's tail, so it scales and positions around the tail.
    /// Driven by <see cref="MurmurService"/>; built by Tools/Agent Tools/UI/Build murmur bubble.
    /// The same bubble also serves as a small menu (<see cref="ShowOptions"/>, driven by <see cref="ContextBubbleService"/>): the title is
    /// the name of the object and under it come tappable lines. A menu stays until the player picks a line or taps somewhere else.
    /// </summary>
    public class MurmurBubble : MonoBehaviour
    {
        private const float POP_IN_DURATION = 0.18f;
        private const float FADE_IN_DURATION = 0.12f;
        private const float FADE_OUT_DURATION = 0.2f;
        private const float POP_IN_START_SCALE = 0.7f;
        private const float REFRESH_PUNCH = 0.05f;
        private const float UNLIMITED_WIDTH = 10000f;
        private const float BALANCE_STEP = 10f;
        private const float OPTION_HEIGHT = 74f;
        private const float OPTION_TEXT_SCALE = 0.95f;
        private static readonly Color OptionHighlight = new Color32( 75, 38, 16, 40 );
        private static readonly Color OptionPressed = new Color32( 75, 38, 16, 90 );
        private static readonly Color DividerColor = new Color32( 165, 80, 28, 120 );

        [ SerializeField ] private RectTransform bubble;
        [ SerializeField ] private TextMeshProUGUI title;
        [ SerializeField ] private TextMeshProUGUI subtitle;
        [ SerializeField ] private CanvasGroup canvasGroup;
        [ Tooltip( "Widest a line of text may get, in canvas units; longer texts wrap and the bubble grows upwards." ) ]
        [ SerializeField ] private float maxTextWidth = 700f;
        [ Tooltip( "Gap between the top of the followed model and the tip of the tail, in world units." ) ]
        [ SerializeField ] private float headClearance = 0.12f;
        [ Tooltip( "Where the tail points when the followed object has no model to measure, relative to its pivot." ) ]
        [ SerializeField ] private Vector3 fallbackOffset = new Vector3( 0f, 1.6f, 0f );

        private RectTransform rootRect;
        private Canvas canvas;
        private Camera worldCamera;
        private Transform target;
        private Vector3 headOffset;
        private Vector3 baseScale;
        private Tween scaleTween;
        private Tween fadeTween;
        private Tween lifetimeTween;
        private bool isShown;
        private readonly List<GameObject> optionRows = new List<GameObject>();

        public bool IsShown => isShown;
        public bool HasOptions => isShown && optionRows.Count > 0;
        public string TitleText => title.text;
        public string SubtitleText => subtitle.gameObject.activeSelf ? subtitle.text : null;

        private void Awake()
        {
            rootRect = ( RectTransform ) transform;
            baseScale = rootRect.localScale;
            canvas = GetComponentInParent< Canvas >();
            gameObject.SetActive( false );
        }

        private void OnDestroy()
        {
            LeanTouch.OnFingerDown -= HandleFingerDown;
            KillTweens();
        }

        // A tap on anything but the menu closes it (the lines of the menu are on the GUI, so they do not count).
        private void HandleFingerDown( LeanFinger finger )
        {
            if( HasOptions && !finger.IsOverGui )
            {
                Hide();
            }
        }

        private void ClearOptions()
        {
            foreach( var row in optionRows )
            {
                Destroy( row );
            }
            optionRows.Clear();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void AddDivider()
        {
            var divider = new GameObject( "Divider", typeof( RectTransform ), typeof( Image ), typeof( LayoutElement ) ) { layer = gameObject.layer };
            divider.transform.SetParent( bubble, false );
            var image = divider.GetComponent< Image >();
            image.color = DividerColor;
            image.raycastTarget = false;
            var size = divider.GetComponent< LayoutElement >();
            size.minHeight = size.preferredHeight = 3f;
            size.flexibleWidth = 1f;
            optionRows.Add( divider );
        }

        // A line of the menu: the text of the title (same font and colour) on a row that tints when pressed.
        private void AddOption( ContextAction action )
        {
            var row = new GameObject( action.Label, typeof( RectTransform ), typeof( Image ), typeof( Button ), typeof( LayoutElement ) ) { layer = gameObject.layer };
            row.transform.SetParent( bubble, false );
            var size = row.GetComponent< LayoutElement >();
            size.minHeight = size.preferredHeight = OPTION_HEIGHT;
            size.flexibleWidth = 1f;
            var background = row.GetComponent< Image >();
            background.color = Color.white;
            var button = row.GetComponent< Button >();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.clear,
                highlightedColor = OptionHighlight,
                pressedColor = OptionPressed,
                selectedColor = Color.clear,
                disabledColor = Color.clear,
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };

            var label = Instantiate( title, row.transform );
            label.name = "Label";
            DestroyImmediate( label.GetComponent< LayoutElement >() );
            label.fontSize = title.fontSize * OPTION_TEXT_SCALE;
            label.text = action.Label;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var execute = action.Execute;
            button.onClick.AddListener( ( ) =>
            {
                Hide();
                execute?.Invoke();
            } );
            optionRows.Add( row );
        }

        /// <summary>Shows the bubble over <paramref name="followed"/>, or just swaps the text if it is already up.</summary>
        public void Show( string titleText, string subtitleText, float duration, Transform followed, Camera camera )
        {
            ClearOptions();
            SetTexts( titleText, subtitleText );
            PopIn( followed, camera );
            lifetimeTween = DOVirtual.DelayedCall( duration, Hide, ignoreTimeScale: true );
        }

        /// <summary>Shows the bubble over <paramref name="followed"/> as a menu: its name and a line to tap for every action.</summary>
        public void ShowOptions( string titleText, IReadOnlyList<ContextAction> actions, Transform followed, Camera camera )
        {
            ClearOptions();
            SetTexts( titleText, null );
            AddDivider();
            foreach( var action in actions )
            {
                AddOption( action );
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate( bubble );
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            // The canvas of the bubbles shows icons that nobody taps, so it may have no raycaster; the lines need one to be tapped.
            if( canvas.GetComponent< GraphicRaycaster >() == null )
            {
                canvas.gameObject.AddComponent< GraphicRaycaster >();
            }
            LeanTouch.OnFingerDown -= HandleFingerDown;
            LeanTouch.OnFingerDown += HandleFingerDown;
            PopIn( followed, camera );
        }

        private void PopIn( Transform followed, Camera camera )
        {
            target = followed;
            worldCamera = camera;
            headOffset = MeasureHeadOffset( followed );

            KillTweens();
            gameObject.SetActive( true );
            Follow();

            if( isShown )
            {
                rootRect.localScale = baseScale;
                scaleTween = rootRect.DOPunchScale( baseScale * REFRESH_PUNCH, 0.15f ).SetUpdate( true );
            }
            else
            {
                isShown = true;
                canvasGroup.alpha = 0f;
                rootRect.localScale = baseScale * POP_IN_START_SCALE;
                fadeTween = canvasGroup.DOFade( 1f, FADE_IN_DURATION ).SetUpdate( true );
                scaleTween = rootRect.DOScale( baseScale, POP_IN_DURATION ).SetEase( Ease.OutBack ).SetUpdate( true );
            }
        }

        public void Hide()
        {
            if( !isShown )
            {
                return;
            }
            isShown = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            LeanTouch.OnFingerDown -= HandleFingerDown;
            KillTweens();
            fadeTween = canvasGroup.DOFade( 0f, FADE_OUT_DURATION ).SetUpdate( true );
            scaleTween = rootRect.DOScale( baseScale * POP_IN_START_SCALE, FADE_OUT_DURATION ).SetUpdate( true );
            scaleTween.onComplete += () => gameObject.SetActive( false );
        }

        // Just before rendering every script and the camera have moved, so the bubble can't lag a frame behind the player.
        private void OnEnable()
        {
            Application.onBeforeRender += FollowWhenShown;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= FollowWhenShown;
        }

        private void FollowWhenShown()
        {
            if( isShown )
            {
                Follow();
            }
        }

        private void Follow()
        {
            if( target == null || worldCamera == null )
            {
                return;
            }
            Vector2 screenPoint = worldCamera.WorldToScreenPoint( target.position + headOffset );
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ( RectTransform ) canvas.transform, screenPoint, UiCamera( canvas ), out var localPoint );
            rootRect.localPosition = localPoint;
        }

        // The tail points just above the top of the model, whatever its size; measured on every show, not every frame,
        // so the bubble doesn't bob with the animation.
        private Vector3 MeasureHeadOffset( Transform followed )
        {
            bool found = false;
            float top = float.MinValue;
            foreach( var renderer in followed.GetComponentsInChildren< Renderer >() )
            {
                if( renderer is MeshRenderer || renderer is SkinnedMeshRenderer )
                {
                    top = Mathf.Max( top, renderer.bounds.max.y );
                    found = true;
                }
            }
            return found ? new Vector3( 0f, top - followed.position.y + headClearance, 0f ) : fallbackOffset;
        }

        // An overlay canvas works in screen pixels and must be given no camera, even when one is assigned to it.
        public static Camera UiCamera( Canvas canvas )
        {
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void SetTexts( string titleText, string subtitleText )
        {
            bool hasSubtitle = !string.IsNullOrEmpty( subtitleText );
            SetText( title, titleText );
            subtitle.gameObject.SetActive( hasSubtitle );
            if( hasSubtitle )
            {
                SetText( subtitle, subtitleText );
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate( bubble );
        }

        // A text is as wide as its content, up to maxTextWidth, and wraps beyond that; the bubble's layout group grows around it.
        private void SetText( TextMeshProUGUI label, string text )
        {
            label.text = text;
            label.GetComponent< LayoutElement >().preferredWidth = WrapWidth( label, text );
        }

        // A wrapped text gets the narrowest width that keeps its number of lines, so the lines come out even instead of
        // leaving one word alone on the last one.
        private float WrapWidth( TextMeshProUGUI label, string text )
        {
            var unwrapped = label.GetPreferredValues( text, UNLIMITED_WIDTH, 0f );
            if( unwrapped.x <= maxTextWidth )
            {
                return unwrapped.x;
            }
            int lines = Mathf.CeilToInt( unwrapped.x / maxTextWidth );
            for( float width = unwrapped.x / lines; width < maxTextWidth; width += BALANCE_STEP )
            {
                if( label.GetPreferredValues( text, width, 0f ).y <= unwrapped.y * lines + 0.5f )
                {
                    return width;
                }
            }
            return maxTextWidth;
        }

        private void KillTweens()
        {
            scaleTween?.Kill();
            fadeTween?.Kill();
            lifetimeTween?.Kill();
            scaleTween = null;
            fadeTween = null;
            lifetimeTween = null;
        }
    }
}
