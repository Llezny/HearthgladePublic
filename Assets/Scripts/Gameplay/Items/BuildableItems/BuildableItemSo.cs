using Hearthglade.Core.World;
using UnityEngine;

namespace Hearthglade.Gameplay.Items.BuildableItems
{
    [CreateAssetMenu(fileName = "BuildableItem", menuName = "ScriptableObjects/Items/BuildableItem")]
    public class BuildableItemSO : ItemSO{
        public GameObject buildingPrefab;

        // Footprint + height in build-grid cells (docs/BUILDING_SYSTEM_PLAN.md section 4), e.g. a wall is
        // 1x2x1, a table 3x2x2. Defaults to a single cell so existing buildable assets (Fence, Well, ...)
        // keep working until someone sets a real size on them.
        public Vector3Int sizeInCells = Vector3Int.one;
        public BuildCategory category = BuildCategory.Decoration;
    }
}
