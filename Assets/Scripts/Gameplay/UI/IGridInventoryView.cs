using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public interface IInventoryGridView : IUIItem {
        public ScrollRect ItemListScroll { get; }
    }
}