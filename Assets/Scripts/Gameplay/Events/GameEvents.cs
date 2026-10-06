using System;
using UnityEngine;

namespace Hearthglade.Gameplay.Events
{
    public class GameEvents : MonoBehaviour {

        public event Action onBuildModeEnable;
        public void OnBuildModeEnable(){
            onBuildModeEnable?.Invoke();
        }

        public event Action onBuildModeDisable;
        public void OnBuildModeDisable(){
            onBuildModeDisable?.Invoke();
        }

        public event Action onBuildBlockSwitch;
        public void OnBuildBlockSwitch(){
            onBuildBlockSwitch?.Invoke();
        }

        public event Action<string,string> onOverTooltip;
        public void OnOverTooltip(string title, string content = ""){
            onOverTooltip?.Invoke(title, content);
        }

        public event Action onOutTooltip;
        public void OnOutTooltip(){
            onOutTooltip?.Invoke();
        }

        public event Action onGamePause;
        public void GamePause(){
            onGamePause?.Invoke();
        }

        public event Action onGameResume;
        public void GameResume(){
            onGameResume?.Invoke();
        }

        public event Action onMenuShow;
        public void OnMenuShow(){
            onMenuShow?.Invoke();
        }
        public event Action onMenuHide;
        public void OnMenuHide(){
            onMenuHide?.Invoke();
        }

        public event Action onBlockReached;
        public void OnBlockReached(){
            onBlockReached?.Invoke();
        }

        public event Action onPlayerDeath;
        public void OnPlayerDeath(){
            onPlayerDeath?.Invoke();
        }

        // Fires every tick based on whether the player is currently taking damage (today that
        // only means starving/dehydrated - see Health.Update) - NOT based on the player's actual
        // current health value. Renamed from OnPlayerLowHealth/OnPlayerAboveHealthThreshold,
        // which were misleading: they fired every tick based on hunger/thirst state, so e.g.
        // eating food while at critically low (but not currently decreasing) health would
        // incorrectly clear a "low health" indicator driven by these events.
        public event Action onPlayerTakingDamage;
        public void OnPlayerTakingDamage(){
            onPlayerTakingDamage?.Invoke();
        }

        public event Action onPlayerNotTakingDamage;
        public void OnPlayerNotTakingDamage(){
            onPlayerNotTakingDamage?.Invoke();
        }


    }
}
