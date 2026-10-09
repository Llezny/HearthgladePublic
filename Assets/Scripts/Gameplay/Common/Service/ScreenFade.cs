using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.Common.Service {

    /// <summary>
    /// A plain black blink for short transitions (going into a house): the screen fades out, the work runs behind the black, the screen
    /// fades back. Builds its own overlay, so it needs no scene wiring. It has the shape of <see cref="ILoadingScreen"/> on purpose, so
    /// that it can replace the animated loading screen later by changing one registration.
    /// </summary>
    public class ScreenFade : MonoBehaviour, ILoadingScreen {

        private const float FadeOutSeconds = 0.18f;
        private const float FadeInSeconds = 0.22f;
        // The shortest time the screen stays fully black, so a very fast job does not look like a glitch.
        private const float MinBlackSeconds = 0.08f;
        private const int SortingOrder = 4000;

        private CanvasGroup group;
        private Canvas canvas;
        private float alpha;
        private float target;
        private float speed;

        private void Awake( ) {
            var canvasObject = new GameObject( "ScreenFadeCanvas" );
            canvasObject.transform.SetParent( transform, false );
            canvas = canvasObject.AddComponent<Canvas>( );
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvasObject.AddComponent<GraphicRaycaster>( );
            group = canvasObject.AddComponent<CanvasGroup>( );

            var imageObject = new GameObject( "Black", typeof( RectTransform ) );
            imageObject.transform.SetParent( canvasObject.transform, false );
            var rect = ( RectTransform ) imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = imageObject.AddComponent<Image>( );
            image.color = Color.black;
            image.raycastTarget = true;

            Apply( );
        }

        // Unscaled, so a fade still finishes while the game is paused.
        private void Update( ) {
            if ( Mathf.Approximately( alpha, target ) ) {
                return;
            }
            alpha = Mathf.MoveTowards( alpha, target, speed * Time.unscaledDeltaTime );
            Apply( );
        }

        private void Apply( ) {
            group.alpha = alpha;
            // Blocks the UI (the joystick, the buttons) only while there is something to see.
            group.blocksRaycasts = alpha > 0.001f;
            canvas.enabled = alpha > 0.001f;
        }

        private void FadeTo( float value, float seconds ) {
            target = value;
            speed = 1f / Mathf.Max( 0.01f, seconds );
        }

        public void Show( ) {
            FadeTo( 1f, FadeOutSeconds );
        }

        public void Hide( ) {
            FadeTo( 0f, FadeInSeconds );
        }

        /// <summary>Runs <paramref name="routine"/> behind a black screen; returns when the screen is clear again.</summary>
        public async UniTask LoadAsync( Func<UniTask> routine, CancellationToken ct = default ) {
            Show( );
            await WaitForFadeInAsync( ct );
            float blackSince = Time.unscaledTime;
            try {
                await routine( );
                await UniTask.WaitUntil( ( ) => Time.unscaledTime - blackSince >= MinBlackSeconds, cancellationToken: ct );
            }
            finally {
                // A job that throws must not leave the player behind a black screen.
                Hide( );
            }
            await UniTask.WaitUntil( ( ) => Mathf.Approximately( alpha, 0f ), cancellationToken: ct );
        }

        public UniTask LoadAsync( Action work, CancellationToken ct = default ) {
            return LoadAsync( ( ) => {
                work( );
                return UniTask.CompletedTask;
            }, ct );
        }

        /// <summary>Completes when the screen is fully black.</summary>
        public async UniTask WaitForFadeInAsync( CancellationToken ct = default ) {
            await UniTask.WaitUntil( ( ) => Mathf.Approximately( alpha, 1f ), cancellationToken: ct );
        }

        // A blink has no steps to report.
        public void ReportProgress( PipelineProgress progress ) { }
    }
}
