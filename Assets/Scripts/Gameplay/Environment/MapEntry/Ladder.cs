using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Environment
{
    public class Ladder : MapEntryBase, ISceneObjectWithData {
        
        public string TooltipDescription => "Click to get out of the cave";
        private MapManager mapManager;
        
        [ Inject ]
        public void Construct( MapManager mapManager ) {
            this.mapManager = mapManager;
        }
        
        public override void InteractionStart() {
            mapManager.ChangeMap(this);
        }

        public override Vector3 GetEntryPos( BlockModel playerNode, Map.Map currentMap ) {
            UnityEngine.Debug.Log($"player node is : {playerNode.gridX}, {playerNode.gridY}");
            return playerNode.WorldPosition + new Vector3( 0.15f, 0.45f, 0.15f );
        }

        public object CaptureState() {
            var json = JsonConvert.SerializeObject(DestinationMapId, Formatting.Indented);
            return json;
        }

        public void RestoreState(object state) {
            if( state is null ) {
                return;
            }
            DestinationMapId = JsonConvert.DeserializeObject<int>(state.ToString()); 
        }

    }
}
