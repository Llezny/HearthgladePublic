using Hearthglade.Core.Stats;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class PlayerNeedTests {

        [ Test ]
        public void Update_BeforeCheckIntervalElapsed_DoesNotChangeCurrentValue( ) {
            var need = new PlayerNeed( 100, 100, 100 );
            need.Update( increaseFactor: 0.1f );
            Assert.AreEqual( 100, need.CurrentValue );
        }

        [ Test ]
        public void Update_AfterCheckIntervalElapsed_DecreasesCurrentValue( ) {
            var need = new PlayerNeed( 100, 100, 100 );
            need.Update( increaseFactor: 100 ); // accumulate past the internal check interval
            var beforeThresholdCrossed = need.CurrentValue;
            need.Update( increaseFactor: 0 ); // this call sees the accumulated counter and decreases
            Assert.Less( need.CurrentValue, beforeThresholdCrossed );
        }
    }
}
