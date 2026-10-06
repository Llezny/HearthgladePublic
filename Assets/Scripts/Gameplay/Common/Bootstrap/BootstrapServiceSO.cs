using UnityEngine;

namespace Hearthglade.Gameplay.Common.Bootstrap {
    
    [CreateAssetMenu(fileName = "NewBootstrapService", menuName = "ScriptableObjects/BootstrapService")]
    public class BootstrapServiceSO : ScriptableObject {
        public bool Autorun;
        public GameObject ServicePrefab;
    }
}