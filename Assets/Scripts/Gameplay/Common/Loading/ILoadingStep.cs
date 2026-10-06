using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hearthglade.Gameplay.Common.Loading {

    /// <summary>A single unit of loading work, run and tracked by a <see cref="LoadingPipeline"/>.</summary>
    public interface ILoadingStep {
        /// <summary>Shown on the loading screen while this step is the last one to report progress.</summary>
        string Name { get; }

        /// <summary>Share of the pipeline's overall progress this step accounts for, relative to sibling steps.</summary>
        float Weight { get; }

        UniTask ExecuteAsync( IProgress<float> progress, CancellationToken ct );
    }
}
