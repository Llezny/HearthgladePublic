using System;

namespace Hearthglade.Gameplay.Animation {
    public interface IAnimationController {
        public void PlayAnimation( string animName, float normalizedEndTime = 0.5f, Action animationCallback = null );
    }
}