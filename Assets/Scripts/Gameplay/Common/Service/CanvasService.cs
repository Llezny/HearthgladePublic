using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service
{
    public class CanvasService : MonoBehaviour {

        [field: SerializeField]
        public Canvas PlayerCanvas { get; private set; }

        [field: SerializeField]
        public Canvas WorldSpaceCanvas { get; private set; }

        [field: SerializeField]
        public Canvas ShroudCanvas { get; private set; }

        [field: SerializeField]
        public Canvas BottombarCanvas { get; private set; }

        [field: SerializeField]
        public Canvas MenuCanvas { get; private set; }
        
        [field: SerializeField]
        public Canvas TopUICanvas { get; private set; }

        [field: SerializeField]
        public Canvas HudCanvas { get; private set; }

        [field: SerializeField]
        public Canvas InteractionIconCanvas { get; private set; }
    }
}
