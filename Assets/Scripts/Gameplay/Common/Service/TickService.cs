using System;
using UnityEngine;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service {
    public class TickService : ITickable {
        private Action tickListeners;
        private const float TickRateInSeconds = GameConfig.SECONDS_PER_TICK;
        private float timeToTick;

        public void Tick() {
            if ( timeToTick > 0 ) {
                timeToTick -= Time.deltaTime;
                return;
            }
            timeToTick = TickRateInSeconds;
            tickListeners?.Invoke();
        }

        public void RegisterListener( Action listener ) {
            tickListeners += listener;
        } 

        public void UnregisterListener( Action listener ) {
            tickListeners -= listener;
        }

        ~TickService() {
            tickListeners = null;
        }

    }
}