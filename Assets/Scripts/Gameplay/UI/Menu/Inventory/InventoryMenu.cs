using Hearthglade.Gameplay.Events;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory {
    public class InventoryMenu : Common.Menu {
        
        // Dependencies
        private IUIItem inventoryGridView;
        private IUIItem equipmentView;
        //

        private ControlEvents controlEvents;

        [ Inject ]
        public void Construct( IUIItem inventoryGridView, EquipmentView equipmentView, ControlEvents controlEvents ) {
            this.inventoryGridView = inventoryGridView;
            this.equipmentView = equipmentView;
            this.controlEvents = controlEvents;
        }

        private void OnEnable() {
            controlEvents.onInventoryButton += OpenMenu;
            OnOpen += OpenCallback;
        }

        private void OnDisable() {
            controlEvents.onInventoryButton -= OpenMenu;
            OnOpen -= OpenCallback;
        }

        private void OpenCallback() {
            inventoryGridView.Show();
            equipmentView.Show();
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ) {
            base.CloseMenu( checkIfFadeOutShroud );
            inventoryGridView.Hide();
            equipmentView.Hide();
        }
    }
}