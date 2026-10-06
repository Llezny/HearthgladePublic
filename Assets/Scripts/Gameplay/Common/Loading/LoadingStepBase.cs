using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hearthglade.Gameplay.Common.Loading {
    public abstract class LoadingStepBase : ILoadingStep {
        public abstract string Name { get; }
        public virtual float Weight => 1f;

        public abstract UniTask ExecuteAsync( IProgress<float> progress, CancellationToken ct );
    }
}
