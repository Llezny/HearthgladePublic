using System;
using System.Collections.Generic;

namespace Hearthglade.Core.Items
{
    /// <summary>Units to take from one slot of a container.</summary>
    public readonly struct FoodTake
    {
        public int SlotIndex { get; }
        public int Count { get; }

        public FoodTake( int slotIndex, int count ) {
            SlotIndex = slotIndex;
            Count = count;
        }
    }

    /// <summary>What pays a food budget: the units to take and the hunger points they cover.</summary>
    public sealed class FoodPlan
    {
        public IReadOnlyList<FoodTake> Takes { get; }
        public float Needed { get; }
        public float Covered { get; }

        /// <summary>Points beyond the budget: the last unit rarely fits exactly.</summary>
        public float Waste => Math.Max( 0f, Covered - Needed );

        public FoodPlan( IReadOnlyList<FoodTake> takes, float needed, float covered ) {
            Takes = takes;
            Needed = needed;
            Covered = covered;
        }
    }

    /// <summary>
    /// Pays a cost in hunger points out of the food a container holds (docs/EXPLORATION_LOOP_PLAN.md, phase 2). Every stack counts
    /// by the hunger it restores, a cooked dish by its own cooked value. The plan wastes as little as possible and spends the cheapest food
    /// per point first (by <see cref="ItemDefinition.BaseValue"/>), so trade goods and rich dishes stay in the backpack.
    /// </summary>
    public static class FoodBudget
    {
        private const float Epsilon = 1e-4f;

        private struct Candidate
        {
            public int Slot;
            public float Points;
            public int Left;
            public float CostPerPoint;
        }

        /// <summary>All hunger points in the container.</summary>
        public static float Available( ItemContainer container ) {
            float total = 0f;
            foreach( var candidate in Candidates( container ) ) {
                total += candidate.Points * candidate.Left;
            }
            return total;
        }

        /// <returns>False when the container does not hold enough food; the plan is then null.</returns>
        public static bool TryPlan( ItemContainer container, float points, out FoodPlan plan ) {
            if( points <= Epsilon ) {
                plan = new FoodPlan( new List<FoodTake>(), Math.Max( 0f, points ), 0f );
                return true;
            }
            var candidates = Candidates( container );
            float have = 0f;
            foreach( var candidate in candidates ) {
                have += candidate.Points * candidate.Left;
            }
            if( have + Epsilon < points ) {
                plan = null;
                return false;
            }

            var taken = new Dictionary<int, int>();
            float need = points;
            while( need > Epsilon ) {
                // A unit that covers what is left: the one that wastes least, the cheapest on a tie.
                int best = -1;
                for( int i = 0; i < candidates.Count; i++ ) {
                    var c = candidates[ i ];
                    if( c.Left > 0 && c.Points + Epsilon >= need && ( best < 0 || IsBetterFinisher( c, candidates[ best ], need ) ) ) {
                        best = i;
                    }
                }
                if( best >= 0 ) {
                    Take( candidates, best, 1, taken );
                    need -= candidates[ best ].Points;
                    break;
                }

                // Nothing covers it alone: spend the cheapest food per point, as many units as still fit under the need.
                for( int i = 0; i < candidates.Count; i++ ) {
                    var c = candidates[ i ];
                    if( c.Left > 0 && ( best < 0 || c.CostPerPoint < candidates[ best ].CostPerPoint - Epsilon
                        || ( Math.Abs( c.CostPerPoint - candidates[ best ].CostPerPoint ) <= Epsilon && c.Points > candidates[ best ].Points ) ) ) {
                        best = i;
                    }
                }
                var chosen = candidates[ best ];
                int units = Math.Max( 1, Math.Min( chosen.Left, ( int ) Math.Floor( ( need + Epsilon ) / chosen.Points ) ) );
                Take( candidates, best, units, taken );
                need -= units * chosen.Points;
            }

            var takes = new List<FoodTake>();
            foreach( var pair in taken ) {
                takes.Add( new FoodTake( pair.Key, pair.Value ) );
            }
            takes.Sort( ( a, b ) => a.SlotIndex.CompareTo( b.SlotIndex ) );
            plan = new FoodPlan( takes, points, points - need );
            return true;
        }

        /// <summary>Removes the planned units; the container must still hold what the plan was made from.</summary>
        public static void Apply( ItemContainer container, FoodPlan plan ) {
            foreach( var take in plan.Takes ) {
                container[ take.SlotIndex ].Remove( take.Count );
            }
        }

        private static bool IsBetterFinisher( Candidate a, Candidate b, float need ) {
            float wasteA = a.Points - need;
            float wasteB = b.Points - need;
            if( Math.Abs( wasteA - wasteB ) > Epsilon ) {
                return wasteA < wasteB;
            }
            return a.CostPerPoint < b.CostPerPoint;
        }

        private static void Take( List<Candidate> candidates, int index, int units, Dictionary<int, int> taken ) {
            var candidate = candidates[ index ];
            candidate.Left -= units;
            candidates[ index ] = candidate;
            taken[ candidate.Slot ] = ( taken.TryGetValue( candidate.Slot, out int already ) ? already : 0 ) + units;
        }

        private static List<Candidate> Candidates( ItemContainer container ) {
            var list = new List<Candidate>();
            for( int i = 0; i < container.Size; i++ ) {
                var stack = container[ i ].Stack;
                if( stack.IsEmpty ) {
                    continue;
                }
                float points = stack.EffectiveNutrition.Hunger;
                if( points <= 0f ) {
                    continue;
                }
                // An item with no trade value (0) still has a price, or it would always be spent first.
                list.Add( new Candidate { Slot = i, Points = points, Left = stack.Count, CostPerPoint = Math.Max( 1, stack.Definition.BaseValue ) / points } );
            }
            return list;
        }
    }
}
