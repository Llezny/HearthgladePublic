using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Ship {

    /// <summary>One row of the ship's destination list: a label and a click callback, instantiated
    /// once per <see cref="Environment.ShipDestination"/> under ShipMenu's destinationsHolder.</summary>
    public class ShipDestinationButtonView : MonoBehaviour {

        [ SerializeField ] Button button = null;
        [ SerializeField ] TextMeshProUGUI label = null;

        public void Setup( string text, bool selected, UnityAction onClick ) {
            label.text = selected ? $"> {text}" : text;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener( onClick );
        }
    }
}
