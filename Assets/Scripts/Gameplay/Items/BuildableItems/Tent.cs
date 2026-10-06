using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Items.BuildableItems
{
    public class Tent : SceneObject {
        public override void InteractionStart( ){
            UnityEngine.Debug.Log("Interacted with Tent");
        }
    }
}
