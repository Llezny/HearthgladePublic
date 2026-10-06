using Hearthglade.Core.Items;

namespace Hearthglade.Core.Farming {

    // What the view needs to draw a plot right now.
    public readonly struct PlotView {
        public bool Exists { get; }
        public PlotType Type { get; }
        public bool IsEmpty { get; }
        public ItemId CropId { get; }
        public int Stage { get; }
        public int StageCount { get; }
        // 0..1 through the current stage.
        public float Progress { get; }
        public int ReadyYield { get; }
        public int MaxYield { get; }
        // Game minutes until the first harvest, 0 when ripe.
        public long MinutesUntilRipe { get; }
        // Growth speed from the climate it was planted in: 100 = normal.
        public int SpeedPercent { get; }

        public PlotView( bool exists, PlotType type, bool isEmpty, ItemId cropId, int stage, int stageCount, float progress, int readyYield, int maxYield, long minutesUntilRipe = 0, int speedPercent = 100 ) {
            Exists = exists;
            Type = type;
            IsEmpty = isEmpty;
            CropId = cropId;
            Stage = stage;
            StageCount = stageCount;
            Progress = progress;
            ReadyYield = readyYield;
            MaxYield = maxYield;
            MinutesUntilRipe = minutesUntilRipe;
            SpeedPercent = speedPercent;
        }

        public static PlotView Missing => new PlotView( false, PlotType.Bed, true, default, 0, 0, 0f, 0, 0 );

        public static PlotView Empty( PlotType type ) => new PlotView( true, type, true, default, 0, 0, 0f, 0, 0 );
    }
}
