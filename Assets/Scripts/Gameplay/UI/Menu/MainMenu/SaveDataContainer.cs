using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.MainMenu {
    
    [CreateAssetMenu(fileName = "SaveData", menuName = "ScriptableObjects/SaveDataContainer")]
    public class SaveDataContainer : ScriptableObject {
        public SaveHeader SaveHeader;

        #if UNITY_EDITOR
        private void OnEnable( ) {
            this.SaveHeader = new SaveHeader {
                NewGame = true
            };
        }
        #endif
    }
}