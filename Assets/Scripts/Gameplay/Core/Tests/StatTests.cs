using Hearthglade.Core.Stats;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class StatTests {

        [ Test ]
        public void Constructor_SetsBaseValueMaxValueCurrentValue( ) {
            var stat = new Stat( 10, 200, 75 );
            Assert.AreEqual( 10, stat.BaseValue );
            Assert.AreEqual( 200, stat.MaxValue );
            Assert.AreEqual( 75, stat.CurrentValue );
        }

        [ Test ]
        public void CurrentValue_SetAboveMax_ClampsToMax( ) {
            var stat = new Stat( 0, 100, 50 );
            stat.CurrentValue = 150;
            Assert.AreEqual( 100, stat.CurrentValue );
        }

        [ Test ]
        public void CurrentValue_SetBelowZero_ClampsToZero( ) {
            var stat = new Stat( 0, 100, 50 );
            stat.CurrentValue = -10;
            Assert.AreEqual( 0, stat.CurrentValue );
        }

        [ Test ]
        public void CurrentValue_SetToZero_FiresOnCurrentValueEqualZero( ) {
            var stat = new Stat( 0, 100, 50 );
            var fired = false;
            stat.OnCurrentValueEqualZero += ( ) => fired = true;
            stat.CurrentValue = 0;
            Assert.IsTrue( fired );
        }

        [ Test ]
        public void CurrentValue_SetBelowZero_FiresOnCurrentValueEqualZero( ) {
            var stat = new Stat( 0, 100, 50 );
            var fired = false;
            stat.OnCurrentValueEqualZero += ( ) => fired = true;
            stat.CurrentValue = -10;
            Assert.IsTrue( fired );
        }

        [ Test ]
        public void CurrentValue_SetAboveZero_DoesNotFireOnCurrentValueEqualZero( ) {
            var stat = new Stat( 0, 100, 50 );
            var fired = false;
            stat.OnCurrentValueEqualZero += ( ) => fired = true;
            stat.CurrentValue = 10;
            Assert.IsFalse( fired );
        }

        [ Test ]
        public void CurrentValue_Set_FiresOnChangedWithClampedValue( ) {
            var stat = new Stat( 0, 100, 50 );
            float? received = null;
            stat.OnChanged += v => received = v;
            stat.CurrentValue = 150;
            Assert.AreEqual( 100f, received );
        }
    }
}
