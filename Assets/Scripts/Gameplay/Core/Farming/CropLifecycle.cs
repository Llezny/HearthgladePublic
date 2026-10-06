namespace Hearthglade.Core.Farming {

    public enum CropLifecycle {
        // One harvest, then the plant is gone and the plot is empty again.
        Annual,
        // Stays after harvest and keeps producing fruit.
        Perennial,
    }
}
