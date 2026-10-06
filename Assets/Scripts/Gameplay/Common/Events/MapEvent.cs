using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Events {
    public abstract class MapEvent : ScriptableObject {
        private readonly List<Action<Map.Map>> listeners = new( );

        public void Raise( Map.Map map ) {
            for(int i = listeners.Count -1; i >= 0; i--) {
                listeners[i].Invoke( map );
            }
        }

        public void RemoveAllListeners( ) {
            listeners.Clear(  );
        }

        public void RegisterListener(Action<Map.Map> listener)
        { listeners.Add(listener); }

        public void UnregisterListener(Action<Map.Map> listener)
        { listeners.Remove(listener); }
    }
}
