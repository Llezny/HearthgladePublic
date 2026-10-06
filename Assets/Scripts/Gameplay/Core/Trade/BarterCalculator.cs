using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Trade {

    public enum TradeProblem {
        None,
        NothingOffered,
        NothingRequested,
        NotTradable,
        NotSold,
        OutOfStock,
        Locked,
        NotEnough
    }

    public readonly struct TradeQuote {
        /// <summary>What the port pays for the player's goods.</summary>
        public double GiveValue { get; }

        /// <summary>What the port asks for the goods the player takes.</summary>
        public double TakeValue { get; }

        public TradeProblem Problem { get; }

        /// <summary>The item that causes the problem, when there is one.</summary>
        public ItemId ProblemItem { get; }

        public TradeQuote( double giveValue, double takeValue, TradeProblem problem, ItemId problemItem = default ) {
            GiveValue = giveValue;
            TakeValue = takeValue;
            Problem = problem;
            ProblemItem = problemItem;
        }

        public bool IsAcceptable => Problem == TradeProblem.None;
    }

    /// <summary>Prices a barter and checks that it may happen. Pure functions of a profile, a state and the two offers.</summary>
    public static class BarterCalculator {

        private const double Epsilon = 1e-9;

        /// <summary>What the port pays for <paramref name="count"/> units of an item, each sold unit worth less than the one before.</summary>
        public static double SellValue( PortProfile profile, PortState state, ItemDefinition item, int count ) {
            if( item == null || !item.IsTradable || count <= 0 ) {
                return 0.0;
            }
            double price = item.BaseValue * profile.PriceMultiplierOf( item.Id ) / ( 1.0 + profile.MarginAt( profile.LevelFor( state.RelationPoints ) ) );
            double decay = profile.SaturationPerUnit;
            double first = price * Math.Pow( decay, state.SoldUnitsOf( item.Id ) );
            return decay >= 1.0 - 1e-9 ? first * count : first * ( 1.0 - Math.Pow( decay, count ) ) / ( 1.0 - decay );
        }

        /// <summary>What the port asks for <paramref name="count"/> units of an item.</summary>
        public static double BuyCost( PortProfile profile, PortState state, ItemDefinition item, int count ) {
            if( item == null || !item.IsTradable || count <= 0 ) {
                return 0.0;
            }
            return item.BaseValue * profile.PriceMultiplierOf( item.Id ) * ( 1.0 + profile.MarginAt( profile.LevelFor( state.RelationPoints ) ) ) * count;
        }

        public static TradeQuote Quote( PortProfile profile, PortState state, IReadOnlyList<ItemStack> give, IReadOnlyList<ItemStack> take ) {
            int level = profile.LevelFor( state.RelationPoints );
            double giveValue = 0.0, takeValue = 0.0;
            bool anyGive = false, anyTake = false;

            foreach( var stack in give ?? new ItemStack[ 0 ] ) {
                if( stack.IsEmpty ) {
                    continue;
                }
                if( !stack.Definition.IsTradable ) {
                    return new TradeQuote( 0, 0, TradeProblem.NotTradable, stack.Id );
                }
                anyGive = true;
                giveValue += SellValue( profile, state, stack.Definition, stack.Count );
            }

            var requested = new Dictionary<ItemId, int>();
            foreach( var stack in take ?? new ItemStack[ 0 ] ) {
                if( stack.IsEmpty ) {
                    continue;
                }
                anyTake = true;
                if( !profile.TryGetOffer( stack.Id, out var good ) ) {
                    return new TradeQuote( giveValue, 0, TradeProblem.NotSold, stack.Id );
                }
                if( level < good.MinRelationLevel ) {
                    return new TradeQuote( giveValue, 0, TradeProblem.Locked, stack.Id );
                }
                requested.TryGetValue( stack.Id, out int already );
                requested[ stack.Id ] = already + stack.Count;
                if( requested[ stack.Id ] > state.StockOf( stack.Id ) ) {
                    return new TradeQuote( giveValue, 0, TradeProblem.OutOfStock, stack.Id );
                }
                takeValue += BuyCost( profile, state, stack.Definition, stack.Count );
            }

            if( !anyGive ) {
                return new TradeQuote( 0, takeValue, TradeProblem.NothingOffered );
            }
            if( !anyTake ) {
                return new TradeQuote( giveValue, 0, TradeProblem.NothingRequested );
            }
            return new TradeQuote( giveValue, takeValue, giveValue + Epsilon >= takeValue ? TradeProblem.None : TradeProblem.NotEnough );
        }

        /// <summary>
        /// Carries out an acceptable barter on the port's state (stock, saturation, relation); the player's items are the caller's job.
        /// Returns the quote; nothing changes when it is not acceptable.
        /// </summary>
        public static TradeQuote Execute( PortProfile profile, PortState state, IReadOnlyList<ItemStack> give, IReadOnlyList<ItemStack> take ) {
            var quote = Quote( profile, state, give, take );
            if( !quote.IsAcceptable ) {
                return quote;
            }
            foreach( var stack in take ) {
                if( !stack.IsEmpty ) {
                    state.AddStock( stack.Id, -stack.Count );
                }
            }
            foreach( var stack in give ) {
                if( stack.IsEmpty ) {
                    continue;
                }
                state.AddSold( stack.Id, stack.Count );
                if( profile.TryGetOffer( stack.Id, out _ ) ) {
                    state.AddStock( stack.Id, stack.Count );
                }
            }
            state.AddRelation( quote.GiveValue );
            return quote;
        }
    }
}
