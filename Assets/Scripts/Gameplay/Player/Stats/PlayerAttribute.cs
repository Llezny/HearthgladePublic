using System.Collections.Generic;
using Hearthglade.Core.Stats;

namespace Hearthglade.Gameplay.Player.Stats
{
    [System.Serializable]
    public class PlayerAttribute : Stat {
        
        private HashSet<AttributeModifier> modifiers = new HashSet<AttributeModifier>();

        public PlayerAttribute( float baseValue = 0, float maxValue = 1, float currentValue = 0 ) {
            this.baseValue = baseValue;
            this.maxValue = maxValue;
            this.currentValue = currentValue;
        }
        
        public PlayerAttribute() {}

        public override float CurrentValue {
            get { 
                var result = currentValue;
                foreach( var modifier in modifiers ){
                    result += modifier.Multiplier;
                }
                return result; 
            }
            set => currentValue = value;
        }

        public void AddModifier( AttributeModifier modifier ) {
            modifiers.Add( modifier );
        }

        public void RemoveModifier( AttributeModifier modifier ) {
            modifiers.Remove( modifier );
        }
    }
}
