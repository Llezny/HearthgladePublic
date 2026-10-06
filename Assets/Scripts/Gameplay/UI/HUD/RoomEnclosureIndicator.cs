using TMPro;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD
{
    // POC "you're in home" HUD label (docs/BUILDING_SYSTEM_PLAN.md section 7). Runtime-created like
    // BuildGridVisualizer/BuildGridVisualizer.cs - no scene wiring needed, just an existing Canvas to parent
    // under. Purely a view: PlayerController owns the actual room-enclosure check and just flips this on/off.
    public class RoomEnclosureIndicator : MonoBehaviour
    {
        private TextMeshProUGUI label;

        public static RoomEnclosureIndicator Create( Transform parentCanvas ) {
            var go = new GameObject( "RoomEnclosureIndicator", typeof( RectTransform ) );
            go.transform.SetParent( parentCanvas, false );
            var indicator = go.AddComponent<RoomEnclosureIndicator>();
            indicator.Build();
            return indicator;
        }

        private void Build() {
            var rect = ( RectTransform ) transform;
            rect.anchorMin = new Vector2( 0.5f, 1f );
            rect.anchorMax = new Vector2( 0.5f, 1f );
            rect.pivot = new Vector2( 0.5f, 1f );
            rect.anchoredPosition = new Vector2( 0f, -24f );
            rect.sizeDelta = new Vector2( 320f, 48f );

            label = gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "you're in home";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            gameObject.SetActive( false );
        }

        public void SetEnclosed( bool enclosed ) {
            gameObject.SetActive( enclosed );
        }
    }
}
