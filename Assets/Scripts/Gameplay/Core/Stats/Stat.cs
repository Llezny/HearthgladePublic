using System;

namespace Hearthglade.Core.Stats
{
    public class Stat {

        public Stat( float baseValue, float maxValue, float currentValue ) {
            this.baseValue = baseValue;
            this.maxValue = maxValue;
            this.currentValue = currentValue;
        }
        public Stat() {}

        public event Action OnCurrentValueEqualZero;
        public event Action<float> OnChanged;

        public virtual float CurrentValue {
            get => currentValue;
            set {
                if ( value <= 0 ) {
                    OnCurrentValueEqualZero?.Invoke();
                }
                currentValue = value < 0 ? 0 : value > maxValue ? maxValue : value;
                OnChanged?.Invoke(currentValue);
            }
        }

        public float BaseValue { get => baseValue; private set => baseValue = value; }
        public float MaxValue { get => maxValue; private set => maxValue = value; }

        protected float baseValue;

        protected float currentValue;

        protected float maxValue;
    }
}
