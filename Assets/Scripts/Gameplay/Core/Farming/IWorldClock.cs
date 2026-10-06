namespace Hearthglade.Core.Farming {

    // Monotonic world time in game minutes (days * minutes per day + time of day), saved with the game.
    public interface IWorldClock {
        long NowMinutes { get; }
    }
}
