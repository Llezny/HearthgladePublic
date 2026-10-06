using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Farming
{
    public class SeedTile : MonoBehaviour {

        [ SerializeField ] GameObject recipeTile = null;
        [ SerializeField ] TextMeshProUGUI itemNameText = null;
        [ SerializeField ] Button plantButton = null;

        public void SetTile( CropSO crop, int ownedSeeds, string climateNote, SeedPickerMenu menu ) {
            recipeTile.GetComponent<Image>().sprite = crop.seed.icon;
            var slot = recipeTile.GetComponent<RecipeSlot>();
            slot.EnableQuantityText();
            slot.SetQuantityText( 1, ownedSeeds );

            itemNameText.text = $"{crop.seed.itemName} <size=70%><alpha=#B0>{climateNote}";

            plantButton.onClick.RemoveAllListeners();
            plantButton.onClick.AddListener( () => menu.Plant( crop ) );
            plantButton.interactable = ownedSeeds >= 1;
        }
    }
}
