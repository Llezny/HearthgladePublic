using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.Ship {
    
    [System.Serializable]
    public class ShipStat{
        public string name;
        public string description;
        public Slider slider;
        public short statLevel;
        public short maxLevel;
        public List<Requirement> upgradeCost;
        public Button upgradeButton;
        public void LevelUp(){
            slider.DOValue(++statLevel,1f);
        }
    }

}