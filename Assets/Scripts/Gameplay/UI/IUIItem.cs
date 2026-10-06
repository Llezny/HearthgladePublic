using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public interface IUIItem {
        public void Show();
        public void Hide();
        public RectTransform RectTransform { get; }
    }
}