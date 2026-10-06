using Hearthglade.Gameplay.Entities;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Fishing
{
    public class FishHole : Entity {

        FishingPopup fishingPopup;

        private void Start() {
            fishingPopup = FindObjectOfType<FishingPopup>();
        }

        public void InteractionStart() {
            fishingPopup.OpenMenu( this );
        }

        public void Destroy() {
            Die();
        }
    }
}
