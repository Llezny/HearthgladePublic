namespace Hearthglade.Core.Farming {

    // The whole mutable state of a planted crop. Everything else is derived from it by CropGrowth.
    public sealed class CropState {
        public string CropId;
        public long PlantedAtMinute;
        // Perennials: fruit ripens from this moment, one per FruitRegrowMinutes. Moves forward on harvest.
        public long FruitClockStartMinute;
        // Fixed when planted from the climate of the cell: 100 = normal speed, below = slower.
        public int SpeedPercent;

        public CropState( string cropId, long plantedAtMinute, long fruitClockStartMinute, int speedPercent = 100 ) {
            CropId = cropId;
            PlantedAtMinute = plantedAtMinute;
            FruitClockStartMinute = fruitClockStartMinute;
            SpeedPercent = speedPercent < 1 ? 100 : speedPercent;
        }
    }
}
