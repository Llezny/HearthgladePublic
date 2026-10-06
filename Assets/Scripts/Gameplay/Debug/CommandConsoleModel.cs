using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Hearthglade.Gameplay.Debug
{
    public class ConsoleCommandsService {
        Dictionary<string, Command> commands = new ( StringComparer.OrdinalIgnoreCase );

        public ConsoleCommandsService() {
            AddCommand( "help", GetCommandsList, "prints all commands" );
        }

        string GetCommandsList( string input = "" ) {
            var commandList = "Commands:\n";
            foreach( var command in commands ){
                commandList += $"  {command.Key} - {command.Value.help} \n";
            }
            return commandList;
        }

        public void AddCommand( string name, Func<string, string> func, string help = "" ) {
            if( commands.ContainsKey( name ) ) {
                UnityEngine.Debug.LogError( "There is already defined command :" + name );
            }
            else {
                commands.Add( name, new Command( name, help, func ) );
            }
        }

        // "name arg1 arg2": the first word picks the command, the rest is passed to it as the input.
        public string ExecuteCommand( string commandLine ) {
            commandLine = commandLine?.Trim();
            if( string.IsNullOrEmpty( commandLine ) ){
                return "";
            }
            var parts = commandLine.Split( new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries );
            if( !commands.TryGetValue( parts[ 0 ], out var command ) ) {
                return $"Unknown command: {parts[ 0 ]}\n";
            }
            return command.func.Invoke( parts.Length > 1 ? parts[ 1 ].Trim() : "" );
        }

        [Serializable]
        public class Command {
            public string name;
            public string help;
            public Func<string, string> func;
            public Command( string name, string help, Func<string, string> func ) {
                this.name = name;
                this.help = help;
                this.func = func;
            }
        }
    }
}
