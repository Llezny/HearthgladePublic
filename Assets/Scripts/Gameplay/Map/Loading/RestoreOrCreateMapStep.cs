using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;
using VContainer;

namespace Hearthglade.Gameplay.Map.Loading {

    /// <summary>Restores the saved maps (new game: generates the home map instead). By far the
    /// slowest part of loading a game, so it carries most of the GameLoadPipeline's weight.</summary>
    public sealed class RestoreOrCreateMapStep : LoadingStepBase {
        private readonly MapManager mapManager;

        public override string Name => "Loading map";
        public override float Weight => 5f;

        [ Inject ]
        public RestoreOrCreateMapStep( MapManager mapManager ) {
            this.mapManager = mapManager;
        }

        public override async UniTask ExecuteAsync( IProgress<float> progress, CancellationToken ct ) {
            await mapManager.SetupAsync( progress, ct );
        }
    }
}
