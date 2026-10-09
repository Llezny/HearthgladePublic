using Hearthglade.Gameplay.Items.BuildableItems;
using UnityEngine;

namespace Hearthglade.Gameplay.Housing
{
    /// <summary>
    /// A bigger house (docs/HOME_ISLAND_PLAN.md, phase 6). It is listed in the building menu inside the house, next to the furniture, and
    /// building it does not put anything down: the house grows to this level (the recipe is the price).
    /// </summary>
    [ CreateAssetMenu( fileName = "HouseUpgrade", menuName = "ScriptableObjects/Items/HouseUpgrade" ) ]
    public class HouseUpgradeItemSO : BuildableItemSO
    {
        [ Tooltip( "The level of the house after the upgrade (2 and up). It is only offered when the house is one level below." ) ]
        [ Min( 2 ) ] public int level = 2;
    }
}
