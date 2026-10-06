using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Service
{
    public class GameManager : MonoBehaviour {

        [ SerializeField ] private int currentStateIndex;
        [ SerializeField ] List<GameState> gameStates;
        public GameState CurrentState { 
            get { return gameStates[ currentStateIndex ]; }
            set { gameStates[ currentStateIndex ] = value; } 
        }

        private void Awake() {
            gameStates = new List<GameState>{
                new GameState { StateName = "Game" },
                new GameState { StateName = "Building" },
            };
            currentStateIndex = 0;
            for( int i = 0; i < gameStates.Count; i++ ){
                var state = GetState( i );
                state.StateIndex = i;
            }
        }

        public GameState GetState( string stateName ) { 
            foreach( var state in gameStates ){
                if( state.StateName == stateName ){
                    return state;
                }
            }
            return null;
        }

        public GameState GetState( int stateIndex ) { 
            if( stateIndex >= 0 && stateIndex < gameStates.Count ){
                return gameStates[ stateIndex ];
            }
            return null;
        }

        public void GoToNextState() {
            CurrentState.ExitState();
            currentStateIndex++;
            CurrentState.EnterState();
        }

        public void GoToState( GameState state ) {
            if( state.StateIndex == CurrentState.StateIndex ) {
                return;
            }
            CurrentState.ExitState();
            currentStateIndex = state.StateIndex;
            CurrentState.EnterState();
        }
        public void GoToDefaultState() {
            var state = GetState( "Game" );
            GoToState( state );
        }

        public void PrintAllStates() {
            foreach( var state in gameStates ) {
                UnityEngine.Debug.Log( "State:" + state.StateName + ", id: " + state.StateIndex );
            }
        }
    }
}
