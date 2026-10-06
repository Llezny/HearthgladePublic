using System.Linq;
using Hearthglade.Core.Expedition;
using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ShipTreeTests {

        private static ShipNodeTier Tier( int points, float magnitude, string ore = null, int count = 0 ) =>
            new ShipNodeTier( points, ore == null ? null : new[] { new ItemAmount( new ItemId( ore ), count ) }, magnitude );

        private static readonly ShipNode Provisions = new ShipNode( "provisions", ShipEffect.ExpeditionDiscount,
            new[] { Tier( 3, 0.15f, "Iron", 2 ), Tier( 5, 0.15f, "Iron", 4 ), Tier( 8, 0.15f, "Crystal", 2 ) } );
        private static readonly ShipNode Hold = new ShipNode( "hold", ShipEffect.BackpackRows, new[] { Tier( 4, 1f ), Tier( 7, 1f ) } );
        private static readonly ShipNode Eye = new ShipNode( "eye", ShipEffect.HarvestBonus, new[] { Tier( 3, 0.3f ), Tier( 6, 0.3f ), Tier( 9, 0.3f ) } );
        private static readonly ShipNode[] Nodes = { Provisions, Hold, Eye };

        [Test]
        public void NewState_HasNothingAndNoEffects() {
            var state = new ShipTreeState();
            Assert.AreEqual( 0, state.Points );
            Assert.AreEqual( 0, state.LevelOf( Provisions ) );
            Assert.AreEqual( 1f, state.CostShare( Nodes ), 1e-4 );
            Assert.AreEqual( 0, state.ExtraBackpackRows( Nodes ) );
            Assert.AreEqual( 0f, state.HarvestChance( Nodes ), 1e-4 );
        }

        [Test]
        public void TryBuy_NeedsThePointsAndSpendsThem() {
            var state = new ShipTreeState();
            state.Award( 2 );
            Assert.IsFalse( state.TryBuy( Provisions ) );
            Assert.AreEqual( 2, state.Points );

            state.Award( 2 );
            Assert.IsTrue( state.TryBuy( Provisions ) );
            Assert.AreEqual( 1, state.Points );
            Assert.AreEqual( 1, state.LevelOf( Provisions ) );
        }

        [Test]
        public void TiersAreBoughtInOrderUntilTheNodeIsBoughtOut() {
            var state = new ShipTreeState();
            state.Award( 100 );
            Assert.AreEqual( 3, state.NextTier( Provisions ).Points );
            state.TryBuy( Provisions );
            Assert.AreEqual( 5, state.NextTier( Provisions ).Points );
            state.TryBuy( Provisions );
            state.TryBuy( Provisions );
            Assert.IsTrue( state.IsMaxed( Provisions ) );
            Assert.IsNull( state.NextTier( Provisions ) );
            int before = state.Points;
            Assert.IsFalse( state.TryBuy( Provisions ) );
            Assert.AreEqual( before, state.Points );
        }

        [Test]
        public void Discount_AddsUpButNeverBelowTheFloor() {
            var state = new ShipTreeState();
            state.Award( 100 );
            state.TryBuy( Provisions );
            Assert.AreEqual( 0.85f, state.CostShare( Nodes ), 1e-4 );
            state.TryBuy( Provisions );
            state.TryBuy( Provisions );
            Assert.AreEqual( 0.55f, state.CostShare( Nodes ), 1e-4 );

            var generous = new[] { new ShipNode( "free", ShipEffect.ExpeditionDiscount, new[] { Tier( 1, 0.9f ) } ) };
            var other = new ShipTreeState();
            other.Award( 1 );
            other.TryBuy( generous[ 0 ] );
            Assert.AreEqual( ShipTreeState.MinCostShare, other.CostShare( generous ), 1e-4 );
        }

        [Test]
        public void BackpackRowsAndHarvestChanceFollowTheirNodes() {
            var state = new ShipTreeState();
            state.Award( 100 );
            state.TryBuy( Hold );
            state.TryBuy( Hold );
            state.TryBuy( Eye );
            state.TryBuy( Eye );
            state.TryBuy( Eye );
            Assert.AreEqual( 2, state.ExtraBackpackRows( Nodes ) );
            Assert.AreEqual( ShipTreeState.MaxHarvestChance, state.HarvestChance( Nodes ), 1e-4, "0.9 is capped" );
            Assert.AreEqual( 1f, new ShipTreeState().CostShare( Nodes ), 1e-4 );
        }

        [Test]
        public void Save_RoundTripsAndAnOldSaveMeansNothing() {
            var state = new ShipTreeState();
            state.Award( 20 );
            state.TryBuy( Provisions );
            state.TryBuy( Hold );

            var loaded = new ShipTreeState();
            loaded.Load( state.ToData() );
            Assert.AreEqual( state.Points, loaded.Points );
            Assert.AreEqual( 1, loaded.LevelOf( Provisions ) );
            Assert.AreEqual( 1, loaded.LevelOf( Hold ) );
            Assert.AreEqual( 0, loaded.LevelOf( Eye ) );

            loaded.Load( null );
            Assert.AreEqual( 0, loaded.Points );
            Assert.AreEqual( 0, loaded.LevelOf( Provisions ) );
        }

        [Test]
        public void ALevelBeyondTheTiersInASaveIsClamped() {
            var state = new ShipTreeState();
            var data = new ShipTreeData { Points = 1 };
            data.Levels[ "hold" ] = 9;
            state.Load( data );
            Assert.AreEqual( 2, state.LevelOf( Hold ) );
            Assert.IsTrue( state.IsMaxed( Hold ) );
        }

        [Test]
        public void Award_NeverGoesBelowZero() {
            var state = new ShipTreeState();
            state.Award( -5 );
            Assert.AreEqual( 0, state.Points );
        }
    }
}
