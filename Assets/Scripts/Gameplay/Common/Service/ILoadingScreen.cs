using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;

namespace Hearthglade.Gameplay.Common.Service {
    public interface ILoadingScreen {
        UniTask LoadAsync( Func<UniTask> routine, CancellationToken ct = default );
        UniTask LoadAsync( Action work, CancellationToken ct = default );
        UniTask WaitForFadeInAsync( CancellationToken ct = default );
        void Show( );
        void Hide( );

        /// <summary>Drives the loading bar/step label, if the prefab has them wired up.</summary>
        void ReportProgress( PipelineProgress progress );
    }
}
