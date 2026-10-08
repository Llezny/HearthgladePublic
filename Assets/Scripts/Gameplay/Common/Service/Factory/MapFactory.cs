using Hearthglade.Gameplay.Entities;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service.Factory {
    public class MapFactory {

        private readonly PlayerController playerController;
        private readonly HuntingService hunting;

        public MapFactory( PlayerController playerController, HuntingService hunting ) {
            this.playerController = playerController;
            this.hunting = hunting;
        }

        public Map.Map Get() {
            var newMap = new GameObject( );
            var mapComponent = newMap.AddComponent<Map.Map>();
            mapComponent.SetPlayerController( playerController );
            mapComponent.SetHunting( hunting );
            return mapComponent;
        }
    }
}
