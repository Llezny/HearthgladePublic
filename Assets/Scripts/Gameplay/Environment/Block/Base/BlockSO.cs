using Hearthglade.Gameplay.Map.Visual;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Block.Base
{
    /// <summary>The ground of a block: its look, walkability and elevation. What grows on it is decided by the biome (BiomeSO.Resources).</summary>
    [CreateAssetMenu(fileName = "NewBlock", menuName = "ScriptableObjects/Block")]
    public class BlockSO : ScriptableObject {

        public bool IsGround;
        public bool IsWalkable = true;
        public BiomeId Biome = BiomeId.Grass;
        public bool IsElevated = false;
    }
}
