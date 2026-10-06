using Hearthglade.Gameplay.UI.HUD;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Debug.Commands {
        public class ClockCommands : IStartable {

        // Dependencies
        private readonly ClockManager clockManager;
        private readonly ConsoleCommandsService consoleCommandsService;
        //

        public void Start() {
            RegisterCommands();
        }

        public ClockCommands( ClockManager clockManager, ConsoleCommandsService consoleCommandsService ) {
            this.clockManager = clockManager;
            this.consoleCommandsService = consoleCommandsService;
        }

        private void RegisterCommands() {
            consoleCommandsService.AddCommand("skipday", SkipDay, "Skips half a day." );
        }
        public string SkipDay( string input = ""){
            clockManager.SwitchTimeOfTheDay();
            return "Day skipped\n";
        }
    }
}
