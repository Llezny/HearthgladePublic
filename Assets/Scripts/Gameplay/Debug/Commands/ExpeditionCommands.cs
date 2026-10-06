using System;
using System.Text;
using Hearthglade.Core.Expedition;
using Hearthglade.Gameplay.Expeditions;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Debug.Commands {
    public class ExpeditionCommands : IStartable {

        // Dependencies
        private readonly ExpeditionService expeditionService;
        private readonly ConsoleCommandsService consoleCommandsService;
        //

        public ExpeditionCommands( ExpeditionService expeditionService, ConsoleCommandsService consoleCommandsService ) {
            this.expeditionService = expeditionService;
            this.consoleCommandsService = consoleCommandsService;
        }

        public void Start() {
            consoleCommandsService.AddCommand( "depth", Depth, "depth [north|east|south|west depth] - shows how far each direction was sailed, or sets the deepest completed depth of one" );
        }

        private string Depth( string input = "" ) {
            var parts = input.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
            if( parts.Length == 2 && Enum.TryParse<Direction>( parts[ 0 ], true, out var chosen ) && int.TryParse( parts[ 1 ], out int depth ) ) {
                expeditionService.SetMaxDepth( chosen, depth );
            }
            var text = new StringBuilder();
            foreach( Direction direction in Enum.GetValues( typeof( Direction ) ) ) {
                text.Append( $"{direction}: depth {expeditionService.Progress.MaxDepthReached( direction )} done, next {expeditionService.Progress.NextDepth( direction )}\n" );
            }
            return text.ToString();
        }
    }
}
