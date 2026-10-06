using Hearthglade.Core.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // Fixed tab groups of the crafting screen (Figma: All / Tools / Materials / Survival / Buildings).
    public enum RecipeCategory
    {
        All,
        Tools,
        Materials,
        Survival,
        Buildings,
    }

    public static class RecipeCategories
    {
        public static readonly RecipeCategory[] Tabs = {
            RecipeCategory.All,
            RecipeCategory.Tools,
            RecipeCategory.Materials,
            RecipeCategory.Survival,
            RecipeCategory.Buildings,
        };

        public static string Label( RecipeCategory category ) {
            switch( category ) {
                case RecipeCategory.Tools:     return "Tools";
                case RecipeCategory.Materials: return "Materials";
                case RecipeCategory.Survival:  return "Survival";
                case RecipeCategory.Buildings: return "Buildings";
                default:                       return "All";
            }
        }

        // Which tab an item of the given type is listed under (besides "All").
        public static RecipeCategory FromItemType( ItemType itemType ) {
            switch( itemType ) {
                case ItemType.Tool:
                case ItemType.Weapon:
                case ItemType.Chestplate:
                case ItemType.Helmet:
                    return RecipeCategory.Tools;
                case ItemType.Food:
                case ItemType.Healing:
                    return RecipeCategory.Survival;
                case ItemType.Buildable:
                    return RecipeCategory.Buildings;
                default:
                    return RecipeCategory.Materials;
            }
        }

        public static bool Contains( RecipeCategory tab, RecipeCategory itemCategory ) {
            return tab == RecipeCategory.All || tab == itemCategory;
        }
    }
}
