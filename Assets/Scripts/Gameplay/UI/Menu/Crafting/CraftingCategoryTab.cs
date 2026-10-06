using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    public class CraftingCategoryTab : MonoBehaviour
    {
        [ SerializeField ] Button button = null;
        [ SerializeField ] Image border = null;
        [ SerializeField ] Image background = null;
        [ SerializeField ] Image icon = null;
        [ SerializeField ] TextMeshProUGUI label = null;
        [ SerializeField ] GameObject glow = null; // amber glow of the active tab

        Sprite inactiveIcon;
        Sprite activeIcon;

        public RecipeCategory Category { get; private set; }

        // Icon tint of an inactive tab, per category (the design colours each icon differently).
        static Color InactiveIconColor( RecipeCategory category ) {
            switch( category ) {
                case RecipeCategory.Tools:     return CraftingPalette.Amber600;
                case RecipeCategory.Materials: return CraftingPalette.Cyan600;
                case RecipeCategory.Survival:  return CraftingPalette.Rose500;
                case RecipeCategory.Buildings: return CraftingPalette.Emerald600;
                default:                       return CraftingPalette.Amber600;
            }
        }

        // activeIcon is optional: a variant drawn for the amber active tab; without it the same sprite is tinted dark.
        public void Bind( RecipeCategory category, Sprite categoryIcon, Sprite categoryActiveIcon, Action<RecipeCategory> onClick ) {
            Category = category;
            inactiveIcon = categoryIcon;
            activeIcon = categoryActiveIcon != null ? categoryActiveIcon : categoryIcon;
            label.text = RecipeCategories.Label( category );
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener( () => onClick( category ) );
            SetActive( false );
        }

        public void SetActive( bool active ) {
            border.color = active ? Color.clear : CraftingPalette.Border;
            background.color = active ? CraftingPalette.AmberTab : CraftingPalette.Surface;
            icon.sprite = active ? activeIcon : inactiveIcon;
            icon.color = active ? CraftingPalette.OnAmber : InactiveIconColor( Category );
            label.color = active ? CraftingPalette.OnAmber : CraftingPalette.Ink700;
            glow.SetActive( active );
        }

        void OnDestroy() {
            button.onClick.RemoveAllListeners();
        }
    }
}
