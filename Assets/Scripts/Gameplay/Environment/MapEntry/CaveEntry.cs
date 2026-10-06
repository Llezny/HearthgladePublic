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
    public class CaveEntry : MapEntryBase, ISceneObjectWithData {

        public override string DestinationMapType => "Cave";
        private MapManager mapManager;
        
        [ Inject ]
        public void Construct( MapManager mapManager ) {
            this.mapManager = mapManager;
        }

        public override void InteractionStart(){
            mapManager.ChangeMap( this );
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
