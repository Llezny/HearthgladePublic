using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.UI.Menu.Crafting;

namespace Hearthglade.Gameplay.Environment
{
    /// <summary>
    /// One place a ship can sail to: which map type it generates and what it costs to set sail.
    /// A ship lists these instead of a single hardcoded destination, so a second destination is
    /// just a new list entry, not a code change (see docs/WORLD_GEN_PLAN.md, Phase 6).
    /// </summary>
    [System.Serializable]
    public class ShipDestination {
        public string displayName;
        public string mapTypeName;
        public string description;
        public List<Requirement> cost = new List<Requirement>();

        [ UnityEngine.Tooltip( "Hunger points of food the trip also costs (an expedition; ports and authored destinations pay items only)." ) ]
        public float foodPoints;

        [ UnityEngine.Tooltip( "Set for a port: the id of its PortSO. Port destinations are added by ShipService once the port is discovered, not authored." ) ]
        public string portId;

        public bool IsPort => !string.IsNullOrEmpty( portId );

        /// <summary>Set for an expedition: the direction and depth it sails to. Created by ShipService, not authored.</summary>
        [ System.NonSerialized ] public ExpeditionTarget? trip;

        public bool IsExpedition => trip.HasValue;
    }
}
