using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Loading;
using UnityEngine.AddressableAssets;

namespace Hearthglade.Gameplay.Common.Bootstrap.Steps {
    public sealed class AddressablesInitStep : LoadingStepBase {
        public override string Name => "Initializing resources";

        // AsyncOperationHandle.ToUniTask() needs the UNITASK_ADDRESSABLE_SUPPORT define (set only
        // once the Addressables package is resolved), so this wraps the handle's own Completed
        // event instead — the same pattern AddressableService.Download already uses.
        public override async UniTask ExecuteAsync( IProgress<float> progress, CancellationToken ct ) {
            var tcs = new UniTaskCompletionSource<bool>( );
            var handle = Addressables.InitializeAsync( );
            handle.Completed += _ => tcs.TrySetResult( true );
            await tcs.Task;
            progress.Report( 1f );
        }
    }
}
