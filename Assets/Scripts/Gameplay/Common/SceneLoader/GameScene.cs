using UnityEngine;

namespace Hearthglade.Gameplay.Common.SceneLoader {
    
    [CreateAssetMenu(fileName = "GameScene", menuName = "ScriptableObjects/GameScene")]
    public class GameScene : ScriptableObject {
        
        [Header("Information")]
        public Type SceneName;
        
        [TextArea]
        public string ShortDescription;
        
        public enum Type {
            Menu,
            Game,
            Initialization,
            PersistentManagers,
        }
    }
}
