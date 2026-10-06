using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.Debug {
    // Command input + live log viewer, built into the gesture/button-triggered DebugMenuGrid panel
    // (see DebugConsoleBuilder). Replaces the old always-on CommandConsoleView OnGUI overlay.
    public class DebugConsoleView : MonoBehaviour {
        [ SerializeField ] private TMP_InputField commandInput;
        [ SerializeField ] private Button executeButton;
        [ SerializeField ] private TextMeshProUGUI outputText;
        [ SerializeField ] private ScrollRect outputScroll;

        private ConsoleCommandsService consoleCommandsService;
        private DebugLogService debugLogService;

        [ Inject ]
        public void Construct( ConsoleCommandsService consoleCommandsService, DebugLogService debugLogService ) {
            this.consoleCommandsService = consoleCommandsService;
            this.debugLogService = debugLogService;
        }

        private void Start() {
            executeButton.onClick.AddListener( ExecuteCommand );
            commandInput.onSubmit.AddListener( _ => ExecuteCommand() );
        }

        private void OnEnable() {
            if( debugLogService == null ) {
                return;
            }
            debugLogService.LogUpdated += RefreshOutput;
            RefreshOutput();
        }

        private void OnDisable() {
            if( debugLogService == null ) {
                return;
            }
            debugLogService.LogUpdated -= RefreshOutput;
        }

        private void ExecuteCommand() {
            var command = commandInput.text.Trim();
            if( string.IsNullOrEmpty( command ) ) {
                return;
            }
            var result = consoleCommandsService.ExecuteCommand( command );
            UnityEngine.Debug.Log( $"[Console] > {command}\n{result}" );
            commandInput.text = "";
            commandInput.ActivateInputField();
        }

        private void RefreshOutput() {
            outputText.text = debugLogService.GetLog();
            Canvas.ForceUpdateCanvases();
            outputScroll.verticalNormalizedPosition = 0f;
        }
    }
}
