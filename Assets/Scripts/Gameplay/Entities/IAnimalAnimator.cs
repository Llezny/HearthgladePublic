using Hearthglade.Core.Entities;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>Plays the animation that matches what a wandering animal is doing.</summary>
    public interface IAnimalAnimator {
        void Play( WanderState state );
    }
}
