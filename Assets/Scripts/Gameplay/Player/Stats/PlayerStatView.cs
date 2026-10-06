using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.UI.HUD;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Player.Stats
{
    
    public class PlayerStatView : HUD {

        private PlayerStatBarFactory playerStatBarFactory;
        [SerializeField] List<StatsMap> statsToSpawn;

        [ Inject ]
        public void Construct(PlayerStatBarFactory playerStatBarFactory) {
            this.playerStatBarFactory = playerStatBarFactory;
        }

        private void Start( ) {
            foreach(var stat in statsToSpawn) {
                if(!playerStatBarFactory.TryGet(stat, out var view)) {
                    UnityEngine.Debug.LogError($"Failed to spawn stat bar. Type: {stat}");
                }
                view.transform.SetParent(content.transform, false);
            }
        }
    } 
}
