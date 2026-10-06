namespace Hearthglade.Core.Entities {

    // Seeking is walking to food (see ForageBrain); Eating is standing at it.
    public enum WanderState { Idle, Walking, Running, Seeking, Eating }

    /// <summary>
    /// The behaviour of a skittish, wandering animal without any engine types: it rests for a while, walks to a
    /// nearby spot, rests again, and runs off when something comes too close. The caller moves the body and tells
    /// the brain when the destination was reached; the brain only decides what the animal wants to do next.
    /// </summary>
    public sealed class WanderBrain {

        public WanderState State { get; private set; } = WanderState.Idle;

        /// <summary>Seconds left of the current rest.</summary>
        public float RestLeft { get; private set; }

        /// <summary>Seconds left of the current meal.</summary>
        public float EatLeft { get; private set; }

        public WanderBrain( float firstRestSeconds ) {
            RestLeft = firstRestSeconds;
        }

        /// <summary>True when a rest has just run out and the animal wants a new walking destination.</summary>
        public bool WantsToWalk( float deltaSeconds ) {
            if( State != WanderState.Idle ) {
                return false;
            }
            RestLeft -= deltaSeconds;
            return RestLeft <= 0f;
        }

        public void StartWalking() {
            State = WanderState.Walking;
        }

        /// <summary>Heading for something to eat.</summary>
        public void StartSeeking() {
            State = WanderState.Seeking;
        }

        public void StartEating( float seconds ) {
            State = WanderState.Eating;
            EatLeft = seconds;
        }

        /// <summary>True when the meal that was being eaten has just run out.</summary>
        public bool FinishedEating( float deltaSeconds ) {
            if( State != WanderState.Eating ) {
                return false;
            }
            EatLeft -= deltaSeconds;
            return EatLeft <= 0f;
        }

        /// <summary>Frightened animals run whatever they were doing.</summary>
        public void StartRunning() {
            State = WanderState.Running;
        }

        public void ReachedDestination( float nextRestSeconds ) {
            State = WanderState.Idle;
            RestLeft = nextRestSeconds;
        }

        /// <summary>Nothing to walk to (no free ground around): rest again instead of trying every frame.</summary>
        public void GiveUpWalking( float nextRestSeconds ) {
            ReachedDestination( nextRestSeconds );
        }

        public static bool IsThreatened( float distanceToThreat, float alertDistance ) {
            return alertDistance > 0f && distanceToThreat < alertDistance;
        }
    }
}
