using System;
using System.Collections.Generic;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Trade;
using VContainer;

namespace Hearthglade.Gameplay.Common.Service {
    public class ShipService {

        public Action<MapEntryBase> OnShipInteraction;
        public Action OnShipUse;
        public Action OnReturnHome;

        public const string ExpeditionMapName = "Expedition";

        private static readonly Dictionary<Direction, string> ClimateHints = new() {
            { Direction.North, "Colder waters: tundra and taiga spread with every depth." },
            { Direction.East, "Wetter shores: swamps spread with every depth." },
            { Direction.South, "Warmer and drier lands: meadows spread with every depth." },
            { Direction.West, "Drier lands: meadows and open ground spread with every depth." },
        };

        private readonly MapManager mapManager;
        private readonly PortService portService;
        private readonly ExpeditionService expeditionService;
        private readonly ShipTreeService shipTree;
        private readonly Dictionary<string, ShipDestination> portDestinations = new();
        private readonly Dictionary<Direction, ShipDestination> expeditionDestinations = new();
        private readonly Dictionary<Direction, int> chosenDepth = new();
        private ExpeditionCostCurve costCurve;
        private MapEntryBase lastInteractedShip;
        private ExpeditionTarget? returningTrip;

        [ Inject ]
        public ShipService( MapManager mapManager, PortService portService, ExpeditionService expeditionService, ShipTreeService shipTree ) {
            this.mapManager = mapManager;
            this.portService = portService;
            this.expeditionService = expeditionService;
            this.shipTree = shipTree;
            this.OnShipInteraction += UpdateLastInteractedShip;
            this.OnShipUse += Travel;
            this.OnReturnHome += ReturnHome;
        }

        // Ships sail out from Home only; on an island or in a port the only way is back.
        public bool CanExplore => mapManager.CurrentMap.MapType.IsHome;

        /// <summary>
        /// The destinations a ship offers: an expedition per direction (to the depth chosen for it) plus every port the
        /// player has discovered. The list authored on the ship (the old Forest and Winter islands) is no longer used.
        /// </summary>
        public IReadOnlyList<ShipDestination> GetDestinations( Ship ship ) {
            var destinations = new List<ShipDestination>();
            if( !CanExplore ) {
                return destinations;
            }
            foreach( Direction direction in Enum.GetValues( typeof( Direction ) ) ) {
                destinations.Add( ExpeditionDestination( direction ) );
            }
            foreach( var port in portService.DiscoveredPorts ) {
                destinations.Add( PortDestination( port ) );
            }
            return destinations;
        }

        /// <summary>The depth the next trip in the direction sails to: the deepest allowed one until the player picks another.</summary>
        public int ChosenDepth( Direction direction ) {
            int deepest = expeditionService.Progress.NextDepth( direction );
            return chosenDepth.TryGetValue( direction, out int depth ) ? Math.Min( depth, deepest ) : deepest;
        }

        /// <summary>Steps the depth of a direction down, and back to the deepest allowed one after depth 1.</summary>
        public void CycleDepth( ShipDestination destination ) {
            if( destination?.trip is not { } trip ) {
                return;
            }
            int current = ChosenDepth( trip.Direction );
            chosenDepth[ trip.Direction ] = current > 1 ? current - 1 : expeditionService.Progress.NextDepth( trip.Direction );
        }

        // One object per direction, so that a selection stays valid while the list is rebuilt.
        private ShipDestination ExpeditionDestination( Direction direction ) {
            if( !expeditionDestinations.TryGetValue( direction, out var destination ) ) {
                destination = new ShipDestination { mapTypeName = ExpeditionMapName };
                expeditionDestinations.Add( direction, destination );
            }
            int depth = ChosenDepth( direction );
            bool isNewDepth = depth > expeditionService.Progress.MaxDepthReached( direction );
            destination.trip = new ExpeditionTarget( direction, depth );
            destination.displayName = $"{direction}, depth {depth}" + ( isNewDepth ? " (new)" : "" );
            destination.description = ClimateHints[ direction ] + "\nTap the destination again to change the depth.";
            destination.cost = Discounted( CostCurve?.CostFor( depth ) ) ?? new List<Hearthglade.Gameplay.UI.Menu.Crafting.Requirement>();
            destination.foodPoints = UnityEngine.Mathf.Ceil( ( CostCurve?.FoodFor( depth ) ?? 0f ) * shipTree.CostShare );
            return destination;
        }

        // The ship tree takes a share off what an expedition costs; at least one piece of every item stays.
        private List<Hearthglade.Gameplay.UI.Menu.Crafting.Requirement> Discounted( List<Hearthglade.Gameplay.UI.Menu.Crafting.Requirement> cost ) {
            if( cost == null ) {
                return null;
            }
            float share = shipTree.CostShare;
            var result = new List<Hearthglade.Gameplay.UI.Menu.Crafting.Requirement>();
            foreach( var requirement in cost ) {
                result.Add( new Hearthglade.Gameplay.UI.Menu.Crafting.Requirement( requirement.requiredItemName, UnityEngine.Mathf.Max( 1, UnityEngine.Mathf.CeilToInt( requirement.requiredQuantity * share ) ) ) );
            }
            return result;
        }

        private ExpeditionCostCurve CostCurve {
            get {
                if( costCurve == null ) {
                    var curves = ResourceLoader.LoadAll<ExpeditionCostCurve>( ResourceLoader.EXPEDITIONS_PATH );
                    costCurve = curves.Length > 0 ? curves[ 0 ] : null;
                    if( costCurve == null ) {
                        UnityEngine.Debug.LogError( $"No ExpeditionCostCurve in Resources/{ResourceLoader.EXPEDITIONS_PATH}, expeditions are free" );
                    }
                }
                return costCurve;
            }
        }

        // One object per port, so that a selection stays valid while the list is rebuilt.
        private ShipDestination PortDestination( PortEntry port ) {
            if( !portDestinations.TryGetValue( port.Asset.Id, out var destination ) ) {
                destination = new ShipDestination { portId = port.Asset.Id };
                portDestinations.Add( port.Asset.Id, destination );
            }
            destination.displayName = port.Asset.DisplayName;
            destination.mapTypeName = port.Asset.mapName;
            destination.description = port.Asset.description;
            destination.cost = port.Asset.travelCost;
            destination.foodPoints = 0f;
            return destination;
        }

        private void UpdateLastInteractedShip( MapEntryBase interactedShip ) {
            this.lastInteractedShip = interactedShip;
        }

        private void Travel( ) {
            if( !CanExplore ) {
                return;
            }
            // The ship of Home keeps no destination of its own, so it is set every time: 0 generates a new island,
            // the id of the map of a port that was already visited brings the player back to it.
            var destination = ( lastInteractedShip as Ship )?.SelectedDestination;
            var port = destination != null && destination.IsPort ? portService.Find( destination.portId ) : null;
            bool portMapExists = port != null && mapManager.MapsDictionary.ContainsKey( port.State.MapId );
            lastInteractedShip.DestinationMapId = portMapExists ? port.State.MapId : MapManager.NotGeneratedMapId;
            mapManager.ChangeMap( lastInteractedShip );
        }

        private void ReturnHome( ) {
            // The expedition counts once the player is home again.
            if( !mapManager.CurrentMap.MapType.IsHome ) {
                returningTrip = mapManager.CurrentMap.ExpeditionTrip;
                mapManager.MapEntered -= CompleteExpeditionOnArrival; // never twice for one trip
                mapManager.MapEntered += CompleteExpeditionOnArrival;
            }
            mapManager.ChangeMapToHome( lastInteractedShip );
        }

        private void CompleteExpeditionOnArrival( Map.Map map ) {
            mapManager.MapEntered -= CompleteExpeditionOnArrival;
            bool record = returningTrip is { } trip && expeditionService.Complete( trip );
            returningTrip = null;
            portService.CompleteExpedition();
            shipTree.Award( ShipTreeState.PointsPerExpedition + ( record ? ShipTreeState.PointsPerRecord : 0 ), record ? "a new depth" : null );
        }
    }
}
