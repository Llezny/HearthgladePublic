using Animancer;
using UnityEngine;

namespace Hearthglade.Gameplay.Animation
{
    public class AnimancerHelper : MonoBehaviour {
        public static AnimancerComponent AddAnimancer( GameObject target ) {
            return target.AddComponent<AnimancerComponent>();
        }
    }
} 