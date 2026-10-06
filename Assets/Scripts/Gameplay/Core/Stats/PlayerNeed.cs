namespace Hearthglade.Core.Stats
{
    public class PlayerNeed : Stat {

        public PlayerNeed( float baseValue, float maxValue, float currentValue ) : base( baseValue, maxValue, currentValue ) { }
        public PlayerNeed() {}

        private float checkInterval = 5f;
        private float checkCounter = 0;
        private float decreaseFactor = 2;

        public void Decrease( float value ) {

        }

        public virtual void Update( float increaseFactor = 1, bool isTakingDamage = false ) {
            if( checkCounter >= checkInterval ) {
                checkCounter = 0;
                CurrentValue -= decreaseFactor;
            }
            checkCounter += increaseFactor;
        }
    }
}
