using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // Small "owned/required" pill for one ingredient on a recipe card.
    public class RequirementChip : MonoBehaviour
    {
        [ SerializeField ] Image border = null;
        [ SerializeField ] Image background = null;
        [ SerializeField ] Image icon = null;
        [ SerializeField ] TextMeshProUGUI amountText = null;

        public void Set( Sprite itemIcon, int owned, int required ) {
            var enough = owned >= required;
            icon.sprite = itemIcon;
            amountText.text = owned + "/" + required;
            amountText.color = enough ? CraftingPalette.Emerald700 : CraftingPalette.Rose600;
            border.color = enough ? CraftingPalette.Emerald200 : CraftingPalette.Rose300;
            background.color = enough ? CraftingPalette.Emerald50 : CraftingPalette.Rose50;
        }
    }
}
