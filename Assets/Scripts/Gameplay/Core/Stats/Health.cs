using System;

namespace Hearthglade.Core.Stats
{
    public class Health : PlayerNeed {
        private bool playerDied = false;

        public event Action OnTakingDamage;
        public event Action OnNotTakingDamage;
        public event Action OnDeath;

        public Health( float baseValue, float maxValue, float currentValue ) : base( baseValue, maxValue, currentValue ) { }

        public override void Update( float increaseFactor = 1, bool isTakingDamage = false ) {
            if( isTakingDamage ) {
                base.Update( increaseFactor );
                OnTakingDamage?.Invoke();
            }
            else {
                OnNotTakingDamage?.Invoke();
            }

            if( !playerDied && CurrentValue < 1 ) {
                playerDied = true;
                OnDeath?.Invoke();
            }
        }
    }
}
