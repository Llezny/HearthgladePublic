
using UnityEngine;
using UnityEngine.Serialization;

namespace Hearthglade.Gameplay.Player.Stats
{
    [System.Serializable]
    public struct AttributeModifier {
        public StatsMap TargetStat;
        
        [Tooltip("Indicates how much an item will enhance a given character attribute," +
                 " the more the better, 0 = 0%, 1 = 100%")]
        [Range(0, 1f)]
        public float Multiplier;
    }
}