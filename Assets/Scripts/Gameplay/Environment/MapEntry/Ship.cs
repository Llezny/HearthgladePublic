using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Environment
{
    public class Ship : MapEntryBase, IInteractable {

        [ Tooltip( "Where this ship can sail. Its menu lists these; picking one only changes which map " +
                    "'Explore' generates, so a second world is a new entry here, not a code change." ) ]
        public List<ShipDestination> destinations = new List<ShipDestination>();

        private int selectedDestinationIndex;

        // V Dependencies
        private ShipService shipService;

        [ Inject ]
        public void Construct( ShipService shipService ) {
            this.shipService = shipService;
        }

        // ^ Dependencies

        /// <summary>What the menu lists: the authored destinations plus the discovered ports; none away from Home.</summary>
        public IReadOnlyList<ShipDestination> Destinations => shipService != null ? shipService.GetDestinations( this ) : destinations;

        public ShipDestination SelectedDestination {
            get {
                var all = Destinations;
                return all.Count == 0 ? null : all[ Mathf.Clamp( selectedDestinationIndex, 0, all.Count - 1 ) ];
            }
        }

        public void SelectDestination( int index ) {
            selectedDestinationIndex = Mathf.Clamp( index, 0, Mathf.Max( 0, Destinations.Count - 1 ) );
        }

        // Falls back to the old hardcoded destination if a ship instance was never given a destinations
        // list (keeps existing prefabs working without a data migration).
        public override string DestinationMapType => SelectedDestination?.mapTypeName ?? "Forest";
        public override bool NewMapOnEachEntry => true;
        public override ExpeditionTarget? DestinationTrip => SelectedDestination?.trip;
        public float InteractionDistance => 1f;

        public override void InteractionStart() {
            DisplayShipMenu();
        }

        private void DisplayShipMenu() {
            shipService.OnShipInteraction?.Invoke( this );
        }

        public override Vector3 GetEntryPos(BlockModel playerNode, Map.Map currentMap) {
            return playerNode.WorldPosition - new Vector3( 1.4f, 0, 0 );
        }
    }
}
