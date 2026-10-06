using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Bootstrap.Steps;
using Hearthglade.Gameplay.Common.Loading;

namespace Hearthglade.Gameplay.Common.Bootstrap {

    /// <summary>
    /// Runs once at app start (see Bootstrapper). Instantiates the persistent services, then runs the
    /// rest of app boot quietly in the background — no loading screen at app start, MainMenu is
    /// interactive immediately.
    /// To add a step: register it below, same stage as another step to run it in parallel with it.
    /// </summary>
    public static class AppBootstrapPipelineRunner {
        public static async UniTask RunAsync( ) {
            InstantiatePersistentServicesStep.Run( );

            var pipeline = new LoadingPipeline( new IReadOnlyList<ILoadingStep>[] {
                new ILoadingStep[] { new AddressablesInitStep( ) },
            } );

            try {
                await pipeline.RunAsync( );
            }
            catch ( Exception ex ) {
                // Fully qualified: Hearthglade.Gameplay.Debug (this project's namespace) shadows UnityEngine.Debug here.
                UnityEngine.Debug.LogException( ex );
            }
        }
    }
}
