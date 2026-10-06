using System.Text;
using Hearthglade.Gameplay.Trade;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Debug.Commands {
    public class PortCommands : IStartable {

        // Dependencies
        private readonly PortService portService;
        private readonly ConsoleCommandsService consoleCommandsService;
        //

        public PortCommands( PortService portService, ConsoleCommandsService consoleCommandsService ) {
            this.portService = portService;
            this.consoleCommandsService = consoleCommandsService;
        }

        public void Start() {
            consoleCommandsService.AddCommand( "expeditions", Expeditions, "expeditions [count] - shows or sets the number of completed expeditions (ports are discovered at their thresholds)" );
            consoleCommandsService.AddCommand( "ports", ListPorts, "Lists the ports with their state" );
        }

        private string Expeditions( string input = "" ) {
            if( int.TryParse( input.Trim(), out int count ) ) {
                portService.SetCompletedExpeditions( count );
            }
            return $"Completed expeditions: {portService.CompletedExpeditions}\n";
        }

        private string ListPorts( string input = "" ) {
            var text = new StringBuilder();
            foreach( var port in portService.Ports ) {
                var state = port.State;
                text.Append( $"{port.Asset.DisplayName}: " );
                text.Append( state.Discovered
                    ? $"discovered, relation level {port.Profile.LevelFor( state.RelationPoints )} ({state.RelationPoints:F0} points), map {state.MapId}\n"
                    : $"not discovered (after {port.Profile.UnlockAfterExpeditions} expeditions)\n" );
            }
            return text.Length == 0 ? "There are no ports\n" : text.ToString();
        }
    }
}
