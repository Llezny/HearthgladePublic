using UnityEngine;

namespace Hearthglade.Gameplay.Debug
{
    public class GraphicsSettings : MonoBehaviour {

        [SerializeField] int targetFrameRate = 90;
        [SerializeField] bool vsync = false;
    

        void Start() {
             Application.targetFrameRate = targetFrameRate;
             QualitySettings.vSyncCount = 0;// vsync ? 1 : 0;
        }

    }
}
