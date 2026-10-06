using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;
using Hearthglade.Gameplay.Map.Loading;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service {

    /// <summary>
    /// Runs the GameLoadPipeline once the GameplayScope container is built, then hides the loading
    /// screen. This is the single place deciding when the Game scene is "ready" — individual steps
    /// (map restore, and whatever gets added later) no longer own the loading screen's lifecycle.
    /// To add a step: register it in GameplayScope, inject it here, and add it to a stage below
    /// (same stage as another step = runs in parallel with it).
    /// </summary>
    public sealed class GameLoadCoordinator : IAsyncStartable {
        private readonly ILoadingScreen loadingScreen;
        private readonly LoadingPipeline pipeline;

        [ Inject ]
        public GameLoadCoordinator( ILoadingScreen loadingScreen, RestoreOrCreateMapStep restoreOrCreateMapStep ) {
            this.loadingScreen = loadingScreen;
            pipeline = new LoadingPipeline( new IReadOnlyList<ILoadingStep>[] {
                new ILoadingStep[] { restoreOrCreateMapStep },
            } );
            pipeline.OnProgressChanged += loadingScreen.ReportProgress;
        }

        public async UniTask StartAsync( CancellationToken cancellation = default ) {
            // A step that throws must not leave the player stuck behind a loading screen forever.
            try {
                await pipeline.RunAsync( cancellation );
            }
            finally {
                loadingScreen.Hide( );
            }
        }
    }
}
