using Hearthglade.Gameplay.Player.Stats;
using UnityEngine;

namespace Hearthglade.Gameplay.Player.Controller
{
    public class Player : MonoBehaviour {

        //TODO Remove this legacy class
    
        public static Player instance;

        void Awake() {
            if (instance == null) {
                instance = this;
            }
            else {
                GameObject.Destroy(this);
            }

        }

    }
} 