using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Map;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    public abstract class MapEntryBase : SceneObject {

        [field: SerializeField ]
        public int DestinationMapId { get; set; } = MapManager.NotGeneratedMapId;
        
        public virtual string DestinationMapType => MapManager.HomeMapName;
        public virtual bool NewMapOnEachEntry => false;

        /// <summary>The expedition a new island behind this entry is generated for (null = a plain map).</summary>
        public virtual ExpeditionTarget? DestinationTrip => null;

        public virtual Vector3 GetEntryPos( BlockModel playerNode, Map.Map currentMap = null ) {
            return Vector3.zero;
        }
    }
}
