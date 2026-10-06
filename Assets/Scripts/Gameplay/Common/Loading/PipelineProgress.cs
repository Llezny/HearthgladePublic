namespace Hearthglade.Gameplay.Common.Loading {

    /// <summary>Snapshot of a <see cref="LoadingPipeline"/> run, for driving a loading bar.</summary>
    public readonly struct PipelineProgress {
        public float Overall01 { get; }
        public string CurrentStepName { get; }

        public PipelineProgress( float overall01, string currentStepName ) {
            Overall01 = overall01;
            CurrentStepName = currentStepName;
        }
    }
}
