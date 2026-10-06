using Animancer;
using Hearthglade.Core.Entities;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>The clips of a wandering animal that are assigned on its prefab.</summary>
    [System.Serializable]
    public class AnimalClips {
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        [Tooltip("Optional: head down at the food. Without it the animal just stands.")]
        public AnimationClip eat;

        [Tooltip("Seconds of cross fade between the clips.")]
        public float fadeSeconds = 0.2f;
    }

    /// <summary>Plays the clips of <see cref="AnimalClips"/> with Animancer.</summary>
    public class ClipAnimalAnimator : IAnimalAnimator {

        private readonly AnimancerComponent animancer;
        private readonly AnimalClips clips;

        public ClipAnimalAnimator( AnimancerComponent animancer, AnimalClips clips ) {
            this.animancer = animancer;
            this.clips = clips;
        }

        public void Play( WanderState state ) {
            var clip = state switch {
                WanderState.Walking => clips.walk,
                WanderState.Seeking => clips.walk,
                WanderState.Eating => clips.eat != null ? clips.eat : clips.idle,
                WanderState.Running => clips.run,
                _ => clips.idle
            };
            if( clip != null ) {
                animancer.Play( clip, clips.fadeSeconds );
            }
        }
    }
}
