using Hearthglade.Core.World;
using UnityEngine;

namespace Hearthglade.Gameplay.Items.BuildableItems
{
    /// <summary>Where a buildable item may be put down.</summary>
    public enum BuildPlace
    {
        /// <summary>On the island, on the build grid of the map.</summary>
        Outdoors,
        /// <summary>In the room of the player's house (docs/HOME_ISLAND_PLAN.md, phase 5): furniture.</summary>
        Indoors,
    }

    [CreateAssetMenu(fileName = "BuildableItem", menuName = "ScriptableObjects/Items/BuildableItem")]
    public class BuildableItemSO : ItemSO{
        public GameObject buildingPrefab;

        // Footprint + height in build-grid cells (docs/BUILDING_SYSTEM_PLAN.md section 4), e.g. a wall is
        // 1x2x1, a table 3x2x2. Defaults to a single cell so existing buildable assets (Fence, Well, ...)
        // keep working until someone sets a real size on them.
        public Vector3Int sizeInCells = Vector3Int.one;
        public BuildCategory category = BuildCategory.Decoration;

        [Tooltip("Outdoors items are listed and placed on the island, Indoors items only inside the house.")]
        public BuildPlace place = BuildPlace.Outdoors;

        [Tooltip("Indoors only: a rug or a mat. Lies on the floor, other furniture may stand on it and the player walks over it.")]
        public bool lyingFlat;
    }
}
