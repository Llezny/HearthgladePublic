using DG.Tweening;
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

        public bool IsShown => isShown;
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
            KillTweens();
        }

        /// <summary>Shows the bubble over <paramref name="followed"/>, or just swaps the text if it is already up.</summary>
        public void Show( string titleText, string subtitleText, float duration, Transform followed, Camera camera )
        {
            target = followed;
            worldCamera = camera;
            headOffset = MeasureHeadOffset( followed );
            SetTexts( titleText, subtitleText );

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
            lifetimeTween = DOVirtual.DelayedCall( duration, Hide, ignoreTimeScale: true );
        }

        public void Hide()
        {
            if( !isShown )
            {
                return;
            }
            isShown = false;
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
