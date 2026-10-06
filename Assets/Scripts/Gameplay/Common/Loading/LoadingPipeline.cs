using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Loading {

    /// <summary>
    /// Runs a list of stages in order; the steps within one stage run in parallel. Aggregates every
    /// step's progress into a single weighted 0..1 value so a loading bar can subscribe to one event
    /// regardless of how many steps or stages are involved.
    /// </summary>
    public sealed class LoadingPipeline {

        private readonly IReadOnlyList<IReadOnlyList<ILoadingStep>> stages;
        private readonly Dictionary<ILoadingStep, float> stepProgress = new( );
        private readonly float totalWeight;
        private string lastStepName;

        public event Action<PipelineProgress> OnProgressChanged;

        public LoadingPipeline( IReadOnlyList<IReadOnlyList<ILoadingStep>> stages ) {
            this.stages = stages;
            foreach ( var step in stages.SelectMany( stage => stage ) ) {
                stepProgress[ step ] = 0f;
            }
            totalWeight = stepProgress.Keys.Sum( step => Mathf.Max( step.Weight, 0.0001f ) );
        }

        public async UniTask RunAsync( CancellationToken ct = default ) {
            foreach ( var stage in stages ) {
                await UniTask.WhenAll( stage.Select( step => RunStepAsync( step, ct ) ) );
            }
            Report( 1f );
        }

        private async UniTask RunStepAsync( ILoadingStep step, CancellationToken ct ) {
            // Switches the label to this step as soon as it starts, even if the step itself never
            // reports mid-way progress — otherwise the previous step's (or a past run's) name stays
            // on screen for the step's entire duration, which reads as a stuck loading screen.
            OnStepProgress( step, 0f );
            var reporter = new StepProgressReporter( this, step );
            await step.ExecuteAsync( reporter, ct );
            OnStepProgress( step, 1f );
        }

        private void OnStepProgress( ILoadingStep step, float value01 ) {
            stepProgress[ step ] = Mathf.Clamp01( value01 );
            lastStepName = step.Name;
            Report( Aggregate( ) );
        }

        private float Aggregate( ) {
            if ( totalWeight <= 0f ) {
                return 1f;
            }
            return stepProgress.Sum( pair => Mathf.Max( pair.Key.Weight, 0.0001f ) * pair.Value ) / totalWeight;
        }

        private void Report( float overall01 ) {
            OnProgressChanged?.Invoke( new PipelineProgress( overall01, lastStepName ) );
        }

        private sealed class StepProgressReporter : IProgress<float> {
            private readonly LoadingPipeline owner;
            private readonly ILoadingStep step;

            public StepProgressReporter( LoadingPipeline owner, ILoadingStep step ) {
                this.owner = owner;
                this.step = step;
            }

            public void Report( float value ) => owner.OnStepProgress( step, value );
        }
    }
}
