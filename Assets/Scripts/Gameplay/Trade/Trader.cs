using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Trade;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Trade
{
    /// <summary>A trader of a port: tapping them opens the trade menu of the port whose island the player is on.</summary>
    public class Trader : SceneObject
    {
        [SerializeField] string title = "Trader";

        private PortService portService;
        private MapManager mapManager;
        private PortMenu portMenu;

        /// <summary>Raised when the player starts dealing with the trader (the character talks).</summary>
        public event System.Action Interacted;

        public string TooltipTitle => title;
        public string TooltipDescription => "Tap to trade";
        public float InteractionDistance => 0.6f;

        [Inject]
        public void Construct(PortService portService, MapManager mapManager, PortMenu portMenu)
        {
            this.portService = portService;
            this.mapManager = mapManager;
            this.portMenu = portMenu;
        }

        public override bool CanInteract()
        {
            return portService.FindByMap(mapManager.CurrentMapId) != null;
        }

        public override void InteractionStart()
        {
            var port = portService.FindByMap(mapManager.CurrentMapId);
            if (port != null)
            {
                portMenu.Open(port);
                Interacted?.Invoke();
            }
        }
    }
}
