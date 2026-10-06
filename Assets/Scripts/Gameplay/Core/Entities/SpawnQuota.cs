namespace Hearthglade.Core.Entities {

    /// <summary>How many entities of one kind may be alive and how often a new one may appear.</summary>
    public sealed class SpawnQuota {

        private readonly int maxAlive;
        private readonly float cooldownSeconds;

        public float SecondsSinceSpawn { get; private set; }

        public SpawnQuota( int maxAlive, float cooldownSeconds ) {
            this.maxAlive = maxAlive;
            this.cooldownSeconds = cooldownSeconds;
            // The first one may appear at once.
            SecondsSinceSpawn = cooldownSeconds;
        }

        public void Advance( float seconds ) {
            SecondsSinceSpawn += seconds;
        }

        public bool CanSpawn( int alive ) {
            return alive < maxAlive && SecondsSinceSpawn >= cooldownSeconds;
        }

        public void Spawned() {
            SecondsSinceSpawn = 0f;
        }
    }
}
