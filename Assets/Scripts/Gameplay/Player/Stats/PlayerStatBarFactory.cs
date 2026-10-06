using System.Collections.Generic;
using Hearthglade.Gameplay.Common.Service;
using Lean.Pool;
using UnityEngine;

namespace Hearthglade.Gameplay.Player.Stats
{
    public class PlayerStatBarFactory {

        private StatBarsContainerSO statBarPrefabs;
        private PlayerStatsComponent playerStatsComponent;
        private CanvasService canvasService;

        private Transform targetCanvas;
        private Dictionary<StatsMap, PlayerStatBarView> cachedPlayerStatBars = new();

        PlayerStatBarFactory(StatBarsContainerSO statBarPrefabs, PlayerStatsComponent playerStatsComponent, CanvasService canvasService ) {
            this.statBarPrefabs = statBarPrefabs;
            this.playerStatsComponent = playerStatsComponent;
            this.canvasService = canvasService;
            targetCanvas = canvasService.HudCanvas.transform;
        }
                   
        public bool TryGet( StatsMap statType, out PlayerStatBarView view ) {
            if(cachedPlayerStatBars.TryGetValue(statType, out view)) {
                return true;
            }
            if(TrySpawnPlayerStatBarView(statType, out view)) {
                return true;
            }
            view = null;
            return false;
        }

        private bool TrySpawnPlayerStatBarView(StatsMap statType, out PlayerStatBarView view) {
            switch(statType) {
                case StatsMap.health:
                    view = LeanPool.Spawn(statBarPrefabs.HealthBarPrefab, targetCanvas);
                    break;
                case StatsMap.thirst:
                    view = LeanPool.Spawn(statBarPrefabs.ThirstBarPrefab, targetCanvas);
                    break;
                case StatsMap.hunger:
                    view = LeanPool.Spawn(statBarPrefabs.HungerBarPrefab, targetCanvas);
                    break;
                default:
                    view = null;
                    return false;
            }   
            view.Construct(playerStatsComponent.GetPlayerStat(statType));
            cachedPlayerStatBars.Add(statType, view);
            return true;
        }
    }
}
