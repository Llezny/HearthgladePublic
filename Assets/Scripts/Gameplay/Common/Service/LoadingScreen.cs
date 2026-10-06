using System;
using System.Threading;
using CaptureTheFlag.Common;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Hearthglade.Gameplay.Common.Service {

    [RequireComponent(typeof(Animator))]
    public class LoadingScreen : PersistentSingleton<LoadingScreen>, ILoadingScreen {

        [ Header( "Progress bar (optional; left unassigned, ReportProgress is a no-op)" ) ]
        // A fillable Image rather than a Slider: this is a passive readout, not an interactive
        // control, and it matches how bars are already built elsewhere in this project (see Crafting.cs).
        [ SerializeField ] private Image progressFillImage;
        [ SerializeField ] private TextMeshProUGUI stepLabel;

        private Animator loadingScreenAnimator;
        private static readonly int IsLoadingHash = Animator.StringToHash( "IsLoading" );

        protected override void Awake( ) {
            base.Awake( );
            loadingScreenAnimator = GetComponent<Animator>( );
            // The Animator's clips only ever drove the icon (Icon/Left/Right); it knows nothing about
            // the step label or progress bar, so their visibility has to be handled here explicitly.
            SetProgressUIActive( false );
        }

        public async UniTask LoadAsync( Func<UniTask> routine, CancellationToken ct = default ) {
            Show( );
            await WaitForFadeInAsync( ct );
            // A step that throws must not leave the player stuck behind a loading screen forever.
            try {
                await routine( );
            }
            finally {
                Hide( );
            }
        }

        public async UniTask LoadAsync( Action work, CancellationToken ct = default ) {
            Show( );
            await WaitForFadeInAsync( ct );
            try {
                work( );
            }
            finally {
                Hide( );
            }
        }

        public void Show( ) {
            SetProgressUIActive( true );
            ReportProgress( new PipelineProgress( 0f, null ) );
            if ( !loadingScreenAnimator.GetBool( IsLoadingHash ) ) {
                loadingScreenAnimator.SetBool( IsLoadingHash, true );
            }
        }

        public void Hide( ) {
            loadingScreenAnimator.SetBool( IsLoadingHash, false );
            SetProgressUIActive( false );
        }

        public void ReportProgress( PipelineProgress progress ) {
            if ( progressFillImage != null ) {
                progressFillImage.fillAmount = progress.Overall01;
            }
            if ( stepLabel != null && !string.IsNullOrEmpty( progress.CurrentStepName ) ) {
                stepLabel.text = progress.CurrentStepName;
            }
        }

        // progressFillImage's parent is the bar's background; SetActive on it toggles both at once.
        private void SetProgressUIActive( bool active ) {
            if ( stepLabel != null ) {
                stepLabel.gameObject.SetActive( active );
            }
            if ( progressFillImage != null ) {
                progressFillImage.transform.parent.gameObject.SetActive( active );
            }
        }

        public async UniTask WaitForFadeInAsync( CancellationToken ct = default ) {
            // one frame to let the animator pick up the SetBool from Show()
            await UniTask.Yield( PlayerLoopTiming.Update, ct );
            while ( true ) {
                if ( loadingScreenAnimator.IsInTransition( 0 ) ) {
                    await UniTask.Yield( PlayerLoopTiming.Update, ct );
                    continue;
                }
                var stateInfo = loadingScreenAnimator.GetCurrentAnimatorStateInfo( 0 );
                if ( stateInfo.loop ) {
                    // entered a looping state (Loading) — FadeIn is done
                    return;
                }
                await UniTask.Yield( PlayerLoopTiming.Update, ct );
            }
        }
    }
}
