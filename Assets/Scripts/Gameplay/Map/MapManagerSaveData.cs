using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Hearthglade.Gameplay.Map {
    [System.Serializable]
    public class MapManagerSaveData {
        public int CurrentMapId;
        public int HomeMapId;

        // How many maps this game has generated so far; part of each new map's seed, so two caves
        // entered one after another differ. Missing in saves that predate it (deserializes as 0).
        public int MapsGenerated;
        public List<MapModel> MapModels = new();
            
        // During deserialization the constructor will be invoked,
        // if it has any parameters, they will be default, which may result in nullref,
        // so this default constructor is necessary
        public MapManagerSaveData( ) { }
            
        public MapManagerSaveData( MapManager mapManager ) {
            this.CurrentMapId = mapManager.CurrentMapId;
            this.HomeMapId = mapManager.HomeMapId;
            this.MapsGenerated = mapManager.MapsGenerated;
            foreach( var m in mapManager.MapsDictionary) {
                MapModels.Add( new MapModel( m.Value ) );
            }
        }
    }
}