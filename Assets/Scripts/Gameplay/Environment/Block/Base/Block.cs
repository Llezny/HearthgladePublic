using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Block.Base
{
    /// <summary>
    /// Sits on a block prefab and points to its <see cref="BlockSO"/>. Blocks of a map are data
    /// (<see cref="Map.BlockModel"/>); this component is only read by <see cref="Map.BlockCatalog"/> to learn what a
    /// prefab is, and is present on the instances of blocks that are visible themselves (e.g. water).
    /// </summary>
    public class Block : MonoBehaviour {
        public BlockSO blockSO;
    }
}
