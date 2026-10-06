using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class ContractTests {

        private static readonly ItemDefinition Pear = TestItems.Make( "Pear", baseValue: 10 );
        private static readonly ItemDefinition Honey = TestItems.Make( "Honey", baseValue: 40 );
        private static readonly ItemDefinition Sapling = TestItems.Make( "Sapling", baseValue: 22 );
        private static readonly ItemDefinition Stone = TestItems.Make( "Stone", baseValue: 6 );
        private static readonly ItemDefinition Iron = TestItems.Make( "Iron", baseValue: 30 );
        private static readonly ItemDefinition Junk = TestItems.Make( "Junk", baseValue: 0 );

        private sealed class Catalog : IItemCatalog {
            private readonly Dictionary<ItemId, ItemDefinition> items = new[] { Pear, Honey, Sapling, Stone, Iron, Junk }.ToDictionary( i => i.Id );
            public IReadOnlyCollection<ItemDefinition> All => items.Values;
            public bool TryGet( ItemId id, out ItemDefinition item ) => items.TryGetValue( id, out item );
            public ItemDefinition Get( ItemId id ) => items[ id ];
        }

        private static readonly Catalog Items = new Catalog();

        // Sells pears and honey (honey from level 1), saplings only from level 1; wants stone, iron and junk (which cannot be traded).
        private static PortProfile Profile() {
            return new PortProfile( "Mosshollow", 2,
                new[] { new PortGood( Pear.Id, 8, 0.7f ), new PortGood( Honey.Id, 4, 1f, 1 ), new PortGood( Sapling.Id, 2, 1f, 1 ) },
                new Dictionary<ItemId, float> { { Stone.Id, 1.6f }, { Iron.Id, 1.5f }, { Junk.Id, 2f } } );
        }

        [Test]
        public void Create_AsksForWhatThePortWantsAndCanBeTraded() {
            var profile = Profile();
            for( int serial = 0; serial < 30; serial++ ) {
                var contract = ContractGenerator.Create( profile, Items, 0, serial, 1234, 2 );
                Assert.IsNotNull( contract );
                Assert.That( contract.Item, Is.EqualTo( Stone.Id ).Or.EqualTo( Iron.Id ), $"serial {serial}" );
                Assert.AreNotEqual( Junk.Id, contract.Item );
            }
        }

        [Test]
        public void Create_IsDeterministicAndDiffersWithTheSerial() {
            var profile = Profile();
            var a = ContractGenerator.Create( profile, Items, 1, 7, 99, 3 );
            var b = ContractGenerator.Create( profile, Items, 1, 7, 99, 3 );
            Assert.AreEqual( a.Item, b.Item );
            Assert.AreEqual( a.Count, b.Count );
            Assert.AreEqual( a.RewardItem, b.RewardItem );
            Assert.AreEqual( a.RewardCount, b.RewardCount );
            Assert.AreEqual( a.ExpiresAt, b.ExpiresAt );

            var differing = Enumerable.Range( 0, 20 ).Select( serial => ContractGenerator.Create( profile, Items, 1, serial, 99, 3 ) )
                .Select( c => ( c.Item, c.Count, c.RewardItem, c.RewardCount ) ).Distinct().Count();
            Assert.Greater( differing, 3 );
        }

        [Test]
        public void Create_TheRewardIsWorthMoreThanWhatIsAsked() {
            var profile = Profile();
            for( int serial = 0; serial < 40; serial++ ) {
                var contract = ContractGenerator.Create( profile, Items, 2, serial, 5, 0 );
                double asked = contract.Count * Items.Get( contract.Item ).BaseValue;
                double reward = contract.RewardCount * Items.Get( contract.RewardItem ).BaseValue;
                // Rounding to whole pieces moves it a little, never below the asked value.
                Assert.GreaterOrEqual( reward, asked * 0.9, $"serial {serial}: {contract.Count} x {contract.Item} for {contract.RewardCount} x {contract.RewardItem}" );
                Assert.Greater( contract.RelationPoints, 0 );
            }
        }

        [Test]
        public void Create_SignatureGoodsAreOnlyAPrizeOnceTheRelationUnlockedThem() {
            var profile = Profile();
            for( int serial = 0; serial < 40; serial++ ) {
                var contract = ContractGenerator.Create( profile, Items, 0, serial, 5, 0 );
                Assert.AreEqual( Pear.Id, contract.RewardItem, $"serial {serial}" );
            }
            var rewards = Enumerable.Range( 0, 60 ).Select( serial => ContractGenerator.Create( profile, Items, 1, serial, 5, 0 ).RewardItem ).Distinct().ToList();
            Assert.IsTrue( rewards.Any( r => r == Honey.Id || r == Sapling.Id ), "a signature good is sometimes the prize" );
        }

        [Test]
        public void Create_NothingToAskForMeansNoContract() {
            var profile = new PortProfile( "Quiet", 0, new[] { new PortGood( Pear.Id, 5 ) } );
            Assert.IsNull( ContractGenerator.Create( profile, Items, 0, 0, 1, 0 ) );
        }

        [Test]
        public void Board_PlacesGrowWithTheRelationLevel() {
            Assert.AreEqual( 1, ContractBoard.PlacesAt( 0 ) );
            Assert.AreEqual( 2, ContractBoard.PlacesAt( 1 ) );
            Assert.AreEqual( 3, ContractBoard.PlacesAt( 2 ) );
            Assert.AreEqual( ContractBoard.MaxActive, ContractBoard.PlacesAt( 9 ) );
        }

        [Test]
        public void Board_RefreshFillsThePlacesAndNeverAsksForTheSameItemTwiceWhenThereIsAChoice() {
            var board = new ContractBoard();
            Assert.AreEqual( 2, board.Refresh( Profile(), Items, 1, 1, 0 ) );
            Assert.AreEqual( 2, board.Active.Count );
            Assert.AreNotEqual( board.Active[ 0 ].Item, board.Active[ 1 ].Item );
            Assert.AreEqual( 0, board.Refresh( Profile(), Items, 1, 1, 0 ), "nothing is free" );
        }

        [Test]
        public void Board_OrdersLapseAndAreReplaced() {
            var board = new ContractBoard();
            board.Refresh( Profile(), Items, 0, 1, 0 );
            var first = board.Active[ 0 ];
            Assert.IsFalse( first.IsExpired( first.ExpiresAt - 1 ) );
            Assert.IsTrue( first.IsExpired( first.ExpiresAt ) );

            Assert.AreEqual( 1, board.Refresh( Profile(), Items, 0, 1, first.ExpiresAt ) );
            Assert.AreEqual( 1, board.Active.Count );
            Assert.AreNotEqual( first.Serial, board.Active[ 0 ].Serial );
        }

        [Test]
        public void Board_CompletedOrderLeavesAndANewOneComesOnRefresh() {
            var board = new ContractBoard();
            board.Refresh( Profile(), Items, 0, 1, 0 );
            var done = board.Active[ 0 ];
            Assert.IsTrue( board.Remove( done ) );
            Assert.AreEqual( 0, board.Active.Count );
            board.Refresh( Profile(), Items, 0, 1, 0 );
            Assert.AreNotEqual( done.Serial, board.Active[ 0 ].Serial );
        }

        [Test]
        public void Board_SaveRoundTripsAndAnOldSaveIsAnEmptyBoard() {
            var board = new ContractBoard();
            board.Refresh( Profile(), Items, 1, 1, 4 );
            var loaded = ContractBoard.FromData( board.ToData() );
            Assert.AreEqual( board.NextSerial, loaded.NextSerial );
            Assert.AreEqual( board.Active.Count, loaded.Active.Count );
            for( int i = 0; i < board.Active.Count; i++ ) {
                Assert.AreEqual( board.Active[ i ].Item, loaded.Active[ i ].Item );
                Assert.AreEqual( board.Active[ i ].Count, loaded.Active[ i ].Count );
                Assert.AreEqual( board.Active[ i ].RewardItem, loaded.Active[ i ].RewardItem );
                Assert.AreEqual( board.Active[ i ].ExpiresAt, loaded.Active[ i ].ExpiresAt );
            }
            Assert.AreEqual( 0, ContractBoard.FromData( null ).Active.Count );
        }

        [Test]
        public void PortState_KeepsItsBoardInTheSave() {
            var state = new PortState();
            state.TryDiscover( Profile(), 2 );
            state.Contracts.Refresh( Profile(), Items, 0, 1, 2 );
            var loaded = PortState.FromData( state.ToData() );
            Assert.AreEqual( 1, loaded.Contracts.Active.Count );
            Assert.AreEqual( 0, PortState.FromData( new PortStateData { Discovered = true } ).Contracts.Active.Count );
        }
    }
}
