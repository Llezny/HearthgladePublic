using Animancer;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>A wandering animal whose Idle / Walk / Run clips are assigned on its prefab and played with Animancer.</summary>
    public abstract class ClipAnimal : PassiveAnimal {

        [ SerializeField ] private AnimancerComponent animancer = null;
        [ SerializeField ] private AnimalClips clips = new();

        protected override IAnimalAnimator CreateAnimator( ) {
            return new ClipAnimalAnimator( animancer, clips );
        }
    }
}
