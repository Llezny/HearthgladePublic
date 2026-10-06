using UnityEngine;

namespace Hearthglade.Gameplay.Common
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig")]
    public class GameConfig : ScriptableObject {

        public const float SUN_TRANSITION_TIME = 15; // In seconds
        public const float DEFAULT_CLOCK_SPEED = 3;
        public const float DEFAULT_TIMESCALE = 0.1f;
        public const int MINUTES_PER_DAY = 1440;
        public const bool HELP_SCREEN_ENABLED = false;

        public const float SECONDS_PER_TICK = 1;


    }
}
