using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Core.Trade;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class BarterTests {

        private static readonly ItemDefinition Pear = TestItems.Make( "Pear", baseValue: 10 );
        private static readonly ItemDefinition Honey = TestItems.Make( "Honey", baseValue: 40 );
        private static readonly ItemDefinition Stone = TestItems.Make( "Stone", baseValue: 6 );
        private static readonly ItemDefinition Iron = TestItems.Make( "Iron", baseValue: 30 );
        private static readonly ItemDefinition Junk = TestItems.Make( "Junk", baseValue: 0 );

        private static ItemStack Of( ItemDefinition item, int count ) => ItemStack.Of( item, count );

        // Offers pears (cheap) and honey (signature, level 1), wants stone double.
        private static PortProfile Profile( float saturation = 0.95f ) {
            return new PortProfile(
                "Mosshollow", 2,
                new[] { new PortGood( Pear.Id, 10, 0.7f ), new PortGood( Honey.Id, 3, 1f, minRelationLevel: 1 ) },
                new Dictionary<ItemId, float> { { Stone.Id, 2f } },
                saturationPerUnit: saturation );
        }

        private static PortState Discovered( PortProfile profile ) {
            var state = new PortState();
            Assert.IsTrue( state.TryDiscover( profile, 2 ) );
            return state;
        }

        [ Test ]
        public void Port_IsDiscoveredAtItsThreshold_OnceOnly( ) {
            var profile = Profile();
            var state = new PortState();
            Assert.IsFalse( state.TryDiscover( profile, 1 ) );
            Assert.IsFalse( state.Discovered );
            Assert.IsTrue( state.TryDiscover( profile, 2 ) );
            Assert.IsFalse( state.TryDiscover( profile, 3 ), "already discovered" );
            Assert.AreEqual( 10, state.StockOf( Pear.Id ) );
            Assert.AreEqual( 3, state.StockOf( Honey.Id ) );
        }

        [ Test ]
        public void SellValue_UsesBasePriceDemandAndMargin( ) {
            var profile = Profile( saturation: 1f );
            var state = Discovered( profile );
            double neutral = BarterCalculator.SellValue( profile, state, Iron, 2 );
            Assert.AreEqual( 30 * 2 / 1.25, neutral, 1e-6, "base value less the level 0 margin" );
            double wanted = BarterCalculator.SellValue( profile, state, Stone, 1 );
            Assert.AreEqual( 6 * 2 / 1.25, wanted, 1e-6, "a wanted good is worth double" );
            Assert.AreEqual( 0.0, BarterCalculator.SellValue( profile, state, Junk, 5 ), "no base value, no trade" );
        }

        [ Test ]
        public void SellValue_FallsWithEveryUnitSold_AndMatchesTheSumOfSingleUnits( ) {
            var profile = Profile( saturation: 0.9f );
            var state = Discovered( profile );
            double decay = profile.SaturationPerUnit; // the float as stored, not the double 0.9
            double expected = 0;
            for( int i = 0; i < 5; i++ ) {
                expected += 30 / 1.25 * System.Math.Pow( decay, i );
            }
            Assert.AreEqual( expected, BarterCalculator.SellValue( profile, state, Iron, 5 ), 1e-6 );

            state.AddSold( Iron.Id, 3 );
            Assert.AreEqual( 30 / 1.25 * System.Math.Pow( decay, 3 ), BarterCalculator.SellValue( profile, state, Iron, 1 ), 1e-6, "units already sold count" );
        }

        [ Test ]
        public void Barter_AcceptsWhenTheOfferIsWorthEnough( ) {
            var profile = Profile( saturation: 1f );
            var state = Discovered( profile );
            // 4 stone = 4 * 12 / 1.25 = 38.4; 5 pears cost 5 * 10 * 0.7 * 1.25 = 43.75
            var tooLittle = BarterCalculator.Quote( profile, state, new[] { Of( Stone, 4 ) }, new[] { Of( Pear, 5 ) } );
            Assert.AreEqual( TradeProblem.NotEnough, tooLittle.Problem );
            var enough = BarterCalculator.Quote( profile, state, new[] { Of( Stone, 5 ) }, new[] { Of( Pear, 5 ) } );
            Assert.IsTrue( enough.IsAcceptable );
            Assert.AreEqual( 48.0, enough.GiveValue, 1e-6 );
            Assert.AreEqual( 43.75, enough.TakeValue, 1e-6 );
        }

        [ Test ]
        public void Barter_ExplainsWhyItIsRefused( ) {
            var profile = Profile();
            var state = Discovered( profile );
            Assert.AreEqual( TradeProblem.NothingOffered, BarterCalculator.Quote( profile, state, new ItemStack[ 0 ], new[] { Of( Pear, 1 ) } ).Problem );
            Assert.AreEqual( TradeProblem.NothingRequested, BarterCalculator.Quote( profile, state, new[] { Of( Stone, 1 ) }, new ItemStack[ 0 ] ).Problem );
            Assert.AreEqual( TradeProblem.NotTradable, BarterCalculator.Quote( profile, state, new[] { Of( Junk, 1 ) }, new[] { Of( Pear, 1 ) } ).Problem );
            var notSold = BarterCalculator.Quote( profile, state, new[] { Of( Stone, 9 ) }, new[] { Of( Iron, 1 ) } );
            Assert.AreEqual( TradeProblem.NotSold, notSold.Problem );
            Assert.AreEqual( Iron.Id, notSold.ProblemItem );
            Assert.AreEqual( TradeProblem.OutOfStock, BarterCalculator.Quote( profile, state, new[] { Of( Iron, 9 ) }, new[] { Of( Pear, 11 ) } ).Problem );
            Assert.AreEqual( TradeProblem.Locked, BarterCalculator.Quote( profile, state, new[] { Of( Iron, 9 ) }, new[] { Of( Honey, 1 ) } ).Problem, "level 0 cannot buy the signature good" );
        }

        [ Test ]
        public void StockLimit_CountsEveryLineOfTheSameItem( ) {
            var profile = Profile();
            var state = Discovered( profile );
            var quote = BarterCalculator.Quote( profile, state, new[] { Of( Iron, 9 ) }, new[] { Of( Pear, 6 ), Of( Pear, 6 ) } );
            Assert.AreEqual( TradeProblem.OutOfStock, quote.Problem );
        }

        [ Test ]
        public void Execute_ChangesStockSaturationAndRelation_OnlyWhenAccepted( ) {
            var profile = Profile();
            var state = Discovered( profile );
            var refused = BarterCalculator.Execute( profile, state, new[] { Of( Stone, 1 ) }, new[] { Of( Pear, 10 ) } );
            Assert.IsFalse( refused.IsAcceptable );
            Assert.AreEqual( 10, state.StockOf( Pear.Id ) );
            Assert.AreEqual( 0.0, state.RelationPoints );
            Assert.AreEqual( 0f, state.SoldUnitsOf( Stone.Id ) );

            var done = BarterCalculator.Execute( profile, state, new[] { Of( Iron, 4 ) }, new[] { Of( Pear, 3 ) } );
            Assert.IsTrue( done.IsAcceptable );
            Assert.AreEqual( 7, state.StockOf( Pear.Id ) );
            Assert.AreEqual( 4f, state.SoldUnitsOf( Iron.Id ) );
            Assert.AreEqual( done.GiveValue, state.RelationPoints, 1e-9 );
        }

        [ Test ]
        public void Relation_RaisesTheLevel_LowersTheMargin_AndUnlocksSignatureGoods( ) {
            var profile = Profile( saturation: 1f );
            var state = Discovered( profile );
            Assert.AreEqual( 0, profile.LevelFor( state.RelationPoints ) );
            double before = BarterCalculator.BuyCost( profile, state, Pear, 1 );
            state.AddRelation( 150 );
            Assert.AreEqual( 1, profile.LevelFor( state.RelationPoints ) );
            Assert.Less( BarterCalculator.BuyCost( profile, state, Pear, 1 ), before, "a better relation, a smaller margin" );
            Assert.IsTrue( BarterCalculator.Quote( profile, state, new[] { Of( Iron, 5 ) }, new[] { Of( Honey, 1 ) } ).IsAcceptable );
            state.AddRelation( 10000 );
            Assert.AreEqual( 2, profile.LevelFor( state.RelationPoints ), "the highest level" );
            Assert.AreEqual( 0.10f, profile.MarginAt( 2 ) );
            Assert.AreEqual( 0.10f, profile.MarginAt( 9 ), "the last margin counts for every higher level" );
        }

        [ Test ]
        public void SellingAndBuyingBackInOnePort_NeverPays( ) {
            foreach( float saturation in new[] { 1f, 0.95f, 0.8f } ) {
                var profile = Profile( saturation );
                foreach( double relation in new[] { 0.0, 150.0, 5000.0 } ) {
                    foreach( var item in new[] { Pear, Stone, Iron, Honey } ) {
                        for( int count = 1; count <= 6; count++ ) {
                            var state = Discovered( profile );
                            state.AddRelation( relation );
                            double got = BarterCalculator.SellValue( profile, state, item, count );
                            double cost = BarterCalculator.BuyCost( profile, state, item, count );
                            Assert.Less( got, cost, $"{item} x{count} at relation {relation}, saturation {saturation}" );
                        }
                    }
                }
            }
        }

        [ Test ]
        public void AnyChainOfSwaps_InOnePort_LosesValue( ) {
            var profile = Profile( saturation: 1f );
            var items = new[] { Pear, Stone, Iron };
            foreach( var a in items ) {
                foreach( var b in items.Where( i => i != a ) ) {
                    var state = Discovered( profile );
                    // one unit of a buys this many b; swapping back must give less than one a
                    double bPerA = BarterCalculator.SellValue( profile, state, a, 1 ) / BarterCalculator.BuyCost( profile, state, b, 1 );
                    double aPerB = BarterCalculator.SellValue( profile, state, b, 1 ) / BarterCalculator.BuyCost( profile, state, a, 1 );
                    Assert.Less( bPerA * aPerB, 1.0, $"{a} -> {b} -> {a}" );
                }
            }
        }

        [ Test ]
        public void SellingBackAnOfferedGood_RestoresItsStock( ) {
            var profile = Profile();
            var state = Discovered( profile );
            BarterCalculator.Execute( profile, state, new[] { Of( Iron, 4 ) }, new[] { Of( Pear, 4 ) } );
            Assert.AreEqual( 6, state.StockOf( Pear.Id ) );
            BarterCalculator.Execute( profile, state, new[] { Of( Pear, 2 ) }, new[] { Of( Pear, 1 ) } );
            Assert.AreEqual( 7, state.StockOf( Pear.Id ), "2 sold back, 1 bought" );
        }

        [ Test ]
        public void Restock_RefillsOnItsIntervalOnly_AndTheSaturationEases( ) {
            var profile = Profile();
            var state = Discovered( profile ); // discovered at 2 expeditions, refilled every 3
            BarterCalculator.Execute( profile, state, new[] { Of( Iron, 8 ) }, new[] { Of( Pear, 5 ) } );
            state.Restock( profile, 4 );
            Assert.AreEqual( 5, state.StockOf( Pear.Id ), "too early" );
            Assert.AreEqual( 8f, state.SoldUnitsOf( Iron.Id ) );
            state.Restock( profile, 5 );
            Assert.AreEqual( 10, state.StockOf( Pear.Id ) );
            Assert.AreEqual( 3f, state.SoldUnitsOf( Iron.Id ), 1e-6, "five units forgotten" );
            state.Restock( profile, 8 );
            Assert.AreEqual( 0f, state.SoldUnitsOf( Iron.Id ), "all forgotten" );
            state.Restock( profile, 8 );
            Assert.AreEqual( 8, state.LastRestock );
        }

        [ Test ]
        public void Restock_SkippedIntervalsCountOnce_AndANotDiscoveredPortStaysEmpty( ) {
            var profile = Profile();
            var state = Discovered( profile );
            state.AddStock( Pear.Id, -10 );
            state.Restock( profile, 20 );
            Assert.AreEqual( 10, state.StockOf( Pear.Id ) );
            Assert.AreEqual( 20, state.LastRestock, "caught up to the last whole interval" );

            var untouched = new PortState();
            untouched.Restock( profile, 50 );
            Assert.AreEqual( 0, untouched.StockOf( Pear.Id ) );
        }

        [ Test ]
        public void State_SurvivesASaveAndLoad( ) {
            var profile = Profile();
            var state = Discovered( profile );
            state.MapId = 7;
            BarterCalculator.Execute( profile, state, new[] { Of( Iron, 4 ) }, new[] { Of( Pear, 3 ) } );
            state.Restock( profile, 3 );

            var loaded = PortState.FromData( state.ToData() );
            Assert.IsTrue( loaded.Discovered );
            Assert.AreEqual( 7, loaded.MapId );
            Assert.AreEqual( state.RelationPoints, loaded.RelationPoints, 1e-9 );
            Assert.AreEqual( state.LastRestock, loaded.LastRestock );
            Assert.AreEqual( state.StockOf( Pear.Id ), loaded.StockOf( Pear.Id ) );
            Assert.AreEqual( state.SoldUnitsOf( Iron.Id ), loaded.SoldUnitsOf( Iron.Id ), 1e-6 );
        }

        [ Test ]
        public void MissingOrBrokenSaveData_GivesAFreshPort( ) {
            Assert.IsFalse( PortState.FromData( null ).Discovered );
            var data = new PortStateData { Relation = -5, Stock = null, Sold = new List<PortSoldEntry> { new PortSoldEntry { Item = "", Units = 3 } } };
            var state = PortState.FromData( data );
            Assert.AreEqual( 0.0, state.RelationPoints );
            Assert.AreEqual( 0f, state.SoldUnitsOf( new ItemId( "" ) ) );
        }
    }
}

namespace Hearthglade.Core.Tests {

    public class TradeBasketTests {

        private static readonly ItemDefinition Pear = TestItems.Make( "Pear", maxStack: 5, baseValue: 10 );
        private static readonly ItemDefinition Iron = TestItems.Make( "Iron", maxStack: 5, baseValue: 30 );

        [ NUnit.Framework.Test ]
        public void Basket_CountsUnits_RespectsTheLimit_AndForgetsEmptiedItems( ) {
            var basket = new Trade.TradeBasket();
            NUnit.Framework.Assert.IsTrue( basket.IsEmpty );
            NUnit.Framework.Assert.AreEqual( 3, basket.Add( Pear, 3, limit: 4 ) );
            NUnit.Framework.Assert.AreEqual( 1, basket.Add( Pear, 3, limit: 4 ), "only room for one more" );
            NUnit.Framework.Assert.AreEqual( 0, basket.Add( Pear, 1, limit: 4 ) );
            NUnit.Framework.Assert.AreEqual( 4, basket.CountOf( Pear.Id ) );
            basket.Add( Iron );
            NUnit.Framework.Assert.AreEqual( 2, basket.Items.Count );
            NUnit.Framework.Assert.AreEqual( 4, basket.Remove( Pear.Id, 9 ) );
            NUnit.Framework.Assert.AreEqual( 1, basket.Items.Count, "an item leaves with its last unit" );
            NUnit.Framework.Assert.AreEqual( 0, basket.Remove( Pear.Id ) );
            basket.Clear();
            NUnit.Framework.Assert.IsTrue( basket.IsEmpty );
        }

        [ NUnit.Framework.Test ]
        public void Basket_ListsItsStacksInTheOrderAdded( ) {
            var basket = new Trade.TradeBasket();
            basket.Add( Iron, 2 );
            basket.Add( Pear, 3 );
            basket.Add( Iron, 1 );
            var stacks = basket.ToStacks();
            NUnit.Framework.Assert.AreEqual( 2, stacks.Count );
            NUnit.Framework.Assert.AreEqual( "Iron", stacks[ 0 ].Id.Value );
            NUnit.Framework.Assert.AreEqual( 3, stacks[ 0 ].Count );
            NUnit.Framework.Assert.AreEqual( 3, stacks[ 1 ].Count );
        }

        private static ItemContainer Backpack( int size, params ItemStack[] stacks ) {
            var container = new ItemContainer( size );
            foreach( var stack in stacks ) {
                container.Add( stack );
            }
            return container;
        }

        [ NUnit.Framework.Test ]
        public void Swap_NeedsTheGoods_AndRoomForWhatComes( ) {
            var backpack = Backpack( 2, ItemStack.Of( Iron, 5 ), ItemStack.Of( Pear, 5 ) ); // both slots full
            NUnit.Framework.Assert.IsFalse( Trade.InventorySwap.CanSwap( backpack, new[] { ItemStack.Of( Iron, 6 ) }, new ItemStack[ 0 ] ), "does not hold that much" );
            NUnit.Framework.Assert.IsFalse( Trade.InventorySwap.CanSwap( backpack, new[] { ItemStack.Of( Iron, 2 ) }, new[] { ItemStack.Of( Pear, 3 ) } ), "no room: the iron slot is only emptied in part" );
            NUnit.Framework.Assert.IsTrue( Trade.InventorySwap.CanSwap( backpack, new[] { ItemStack.Of( Iron, 5 ) }, new[] { ItemStack.Of( Pear, 3 ) } ), "the emptied slot takes the pears" );
        }

        [ NUnit.Framework.Test ]
        public void Swap_NeverTouchesTheRealBackpack( ) {
            var backpack = Backpack( 2, ItemStack.Of( Iron, 5 ) );
            Trade.InventorySwap.CanSwap( backpack, new[] { ItemStack.Of( Iron, 5 ) }, new[] { ItemStack.Of( Pear, 5 ) } );
            NUnit.Framework.Assert.AreEqual( 5, backpack.Count( Iron.Id ) );
            NUnit.Framework.Assert.AreEqual( 0, backpack.Count( Pear.Id ) );
        }
    }
}
