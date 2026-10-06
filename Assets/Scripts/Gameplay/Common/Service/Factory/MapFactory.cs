using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service.Factory {
    public class MapFactory {

        private readonly PlayerController playerController;

        public MapFactory( PlayerController playerController ) {
            this.playerController = playerController;
        }

        public Map.Map Get() {
            var newMap = new GameObject( );
            var mapComponent = newMap.AddComponent<Map.Map>();
            mapComponent.SetPlayerController( playerController );
            return mapComponent;
        }
    }
}