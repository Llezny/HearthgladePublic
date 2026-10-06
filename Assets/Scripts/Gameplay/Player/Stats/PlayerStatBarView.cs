using Hearthglade.Core.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.Player.Stats {

    public class PlayerStatBarView : MonoBehaviour {

        // Inspector dependencies
        [ SerializeField ] StatsMap targetStatName;
        [ SerializeField ] Slider linkedStatBarSlider;
        [ SerializeField ] TextMeshProUGUI valueText;

        // Dependencies
        private Stat linkedStat;

        public void Construct(Stat linkedStat) {
            this.linkedStat = linkedStat;
            linkedStat.OnChanged += UpdateStatBar;
            UpdateStatBar(linkedStat.CurrentValue);
        }

        private void OnEnable() {
            if(linkedStat != null ) {
                linkedStat.OnChanged += UpdateStatBar;
            }
        }

        private void OnDisable() {
            linkedStat.OnChanged -= UpdateStatBar;
        }

        private void UpdateStatBar( float value ) {
            linkedStatBarSlider.value = value / linkedStat.MaxValue;
            if ( valueText != null ) {
                valueText.text = $"{Mathf.RoundToInt( value )}<color=#888b8f>/{Mathf.RoundToInt( linkedStat.MaxValue )}</color>";
            }
        }
    }
}
