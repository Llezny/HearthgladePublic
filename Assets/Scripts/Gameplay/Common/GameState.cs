using System;

namespace Hearthglade.Gameplay.Common
{
    [ Serializable ]
    public class GameState {
        event Action onStateEnter;
        event Action onStateExit;
        public string StateName { get; set; }
        public int StateIndex { get; set; }
        public void AddAtEnter( Action func ) {
            onStateEnter += func;
        }
        public void AddAtExit( Action func ) {
            onStateExit += func;
        }
        public void EnterState() {
            UnityEngine.Debug.Log(" enter state " + StateName );
            onStateEnter?.Invoke();
        }
        public void ExitState() {
            UnityEngine.Debug.Log(" exit state " + StateName );
            onStateExit?.Invoke();
        }
    }
}
