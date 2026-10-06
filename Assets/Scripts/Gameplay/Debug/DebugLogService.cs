using System;
using System.Text;
using UnityEngine;

namespace Hearthglade.Gameplay.Debug {
    // Captures Unity's log stream into a capped buffer so the debug console can show it without
    // its own always-on OnGUI overlay. Subscribes as soon as it is constructed (forced early by
    // DebugConsoleView's injection), so it captures from session start, not just while open.
    public class DebugLogService : IDisposable {
        private const int MaxChars = 8000;

        private readonly StringBuilder log = new();

        public event Action LogUpdated;

        public DebugLogService() {
            Application.logMessageReceived += OnLogMessage;
        }

        public void Dispose() {
            Application.logMessageReceived -= OnLogMessage;
        }

        public string GetLog() {
            return log.ToString();
        }

        private void OnLogMessage( string message, string stackTrace, LogType type ) {
            log.Append( '[' ).Append( type ).Append( "] " ).Append( message ).Append( '\n' );
            if( log.Length > MaxChars ) {
                log.Remove( 0, log.Length - MaxChars );
            }
            LogUpdated?.Invoke();
        }
    }
}
