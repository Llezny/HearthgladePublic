using Hearthglade.Core.Stats;
using TMPro;
using UnityEngine;

namespace Hearthglade.Gameplay.Player.Stats
{
    // The HUD line with the temperature around the player, and why they are losing health to the weather: "-6°C  Freezing", "27°C  Warm".
    // Runtime-created like RoomEnclosureIndicator, under the HUD canvas; ExposureService owns the state and only tells it what to show.
    public class ExposureIndicator : MonoBehaviour
    {
        private const string ColdColour = "#9ED4FF";
        private const string HeatColour = "#FFB259";

        private TextMeshProUGUI label;

        public static ExposureIndicator Create( Transform parentCanvas ) {
            var go = new GameObject( "ExposureIndicator", typeof( RectTransform ) );
            go.transform.SetParent( parentCanvas, false );
            var indicator = go.AddComponent<ExposureIndicator>();
            indicator.Build();
            return indicator;
        }

        private void Build() {
            var rect = ( RectTransform ) transform;
            rect.anchorMin = new Vector2( 0.5f, 1f );
            rect.anchorMax = new Vector2( 0.5f, 1f );
            rect.pivot = new Vector2( 0.5f, 1f );
            // Under the "you're in home" label.
            rect.anchoredPosition = new Vector2( 0f, -72f );
            rect.sizeDelta = new Vector2( 320f, 48f );

            label = gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            gameObject.SetActive( false );
        }

        public void Show( float? temperature, ExposureState state ) {
            gameObject.SetActive( temperature.HasValue );
            if( !temperature.HasValue ) {
                return;
            }
            var text = $"{Mathf.RoundToInt( temperature.Value )}°C";
            if( state.Kind != Discomfort.None ) {
                bool cold = state.Kind == Discomfort.Cold;
                var word = cold ? ( state.IsHarmful ? "Freezing" : "Chilly" ) : ( state.IsHarmful ? "Too hot" : "Warm" );
                text += $"  <color={( cold ? ColdColour : HeatColour )}>{word}</color>";
            }
            label.text = text;
        }
    }
}
