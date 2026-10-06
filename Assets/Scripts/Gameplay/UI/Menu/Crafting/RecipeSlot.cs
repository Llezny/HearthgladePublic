using Hearthglade.Gameplay.UI.Menu.Inventory;
using TMPro;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // An ingredient tile that shows "owned / required" (ship, farming and build-block menus).
    public class RecipeSlot : Slot
    {
        [ SerializeField ] TextMeshProUGUI quantityText = null;

        public void EnableQuantityText(){
            quantityText.gameObject.SetActive(true);
        }

        public void SetQuantityText(int _required, int _owned){
            quantityText.color =  _required > _owned ? Color.red : Color.green;
            quantityText.text = _owned + " / " + _required;
        }
        public bool CanPlayerAffordItem(int _required, int _owned){
            return  _required <= _owned;
        }
    }
}
