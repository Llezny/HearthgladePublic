using Hearthglade.Core.Stats;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class HealthTests {

        [ Test ]
        public void Update_WhenTakingDamage_FiresOnTakingDamage( ) {
            var health = new Health( 100, 100, 100 );
            var fired = false;
            health.OnTakingDamage += ( ) => fired = true;
            health.Update( increaseFactor: 1, isTakingDamage: true );
            Assert.IsTrue( fired );
        }

        [ Test ]
        public void Update_WhenNotTakingDamage_FiresOnNotTakingDamage( ) {
            var health = new Health( 100, 100, 100 );
            var fired = false;
            health.OnNotTakingDamage += ( ) => fired = true;
            health.Update( increaseFactor: 1, isTakingDamage: false );
            Assert.IsTrue( fired );
        }

        [ Test ]
        public void Update_WhenTakingDamage_DoesNotFireOnNotTakingDamage( ) {
            var health = new Health( 100, 100, 100 );
            var fired = false;
            health.OnNotTakingDamage += ( ) => fired = true;
            health.Update( increaseFactor: 1, isTakingDamage: true );
            Assert.IsFalse( fired );
        }

        [ Test ]
        public void Update_WhenNotTakingDamage_DoesNotFireOnTakingDamage( ) {
            var health = new Health( 100, 100, 100 );
            var fired = false;
            health.OnTakingDamage += ( ) => fired = true;
            health.Update( increaseFactor: 1, isTakingDamage: false );
            Assert.IsFalse( fired );
        }

        [ Test ]
        public void Update_WhenCurrentValueBelowOne_FiresOnDeath( ) {
            var health = new Health( 100, 100, 0.5f );
            var died = false;
            health.OnDeath += ( ) => died = true;
            health.Update( increaseFactor: 1, isTakingDamage: false );
            Assert.IsTrue( died );
        }

        [ Test ]
        public void Update_OnDeath_OnlyFiresOnce( ) {
            var health = new Health( 100, 100, 0.5f );
            var deathCount = 0;
            health.OnDeath += ( ) => deathCount++;
            health.Update( increaseFactor: 1, isTakingDamage: false );
            health.Update( increaseFactor: 1, isTakingDamage: false );
            Assert.AreEqual( 1, deathCount );
        }

        [ Test ]
        public void Update_WhenNotTakingDamage_CurrentValueUnchanged( ) {
            var health = new Health( 100, 100, 100 );
            health.Update( increaseFactor: 1, isTakingDamage: false );
            Assert.AreEqual( 100, health.CurrentValue );
        }

        [ Test ]
        public void Update_WhenTakingDamageLongEnough_DecreasesCurrentValue( ) {
            var health = new Health( 100, 100, 100 );
            health.Update( increaseFactor: 100, isTakingDamage: true ); // accumulate past the internal check interval
            var beforeThresholdCrossed = health.CurrentValue;
            health.Update( increaseFactor: 0, isTakingDamage: true ); // this call sees the accumulated counter and decreases
            Assert.Less( health.CurrentValue, beforeThresholdCrossed );
        }
    }
}
