using System;

namespace Hearthglade.Core.Farming {

    // Pure growth functions: stage and fruit are derived from (definition, state, now), nothing ticks.
    // Every duration goes through Scaled, so a crop in a poor climate (state.SpeedPercent < 100) takes proportionally longer.
    public static class CropGrowth {

        // `minutes` of normal growth at the speed of the state, rounded up.
        public static long Scaled( int minutes, CropState state ) {
            return ( minutes * 100L + state.SpeedPercent - 1 ) / state.SpeedPercent;
        }

        // Minutes from planting until the crop is mature.
        public static long MatureMinutes( CropDefinition def, CropState state ) {
            long total = 0;
            for( int i = 0; i < def.StageCount - 1; i++ ) {
                total += Scaled( def.StageMinutes( i ), state );
            }
            return total;
        }

        // Minutes a perennial needs for one more fruit.
        public static long RegrowMinutes( CropDefinition def, CropState state ) {
            return Math.Max( 1, Scaled( def.FruitRegrowMinutes, state ) );
        }

        public static CropState Plant( CropDefinition def, long now, int speedPercent = 100 ) {
            var state = new CropState( def.Id.Value, now, now, speedPercent );
            state.FruitClockStartMinute = now + MatureMinutes( def, state );
            return state;
        }

        // Stage index (the last one is mature) and progress 0..1 through it. Mature is always progress 1.
        public static (int stage, float progress) StageAt( CropDefinition def, CropState state, long now ) {
            long elapsed = Math.Max( 0, now - state.PlantedAtMinute );
            long start = 0;
            for( int i = 0; i < def.StageCount - 1; i++ ) {
                long duration = Scaled( def.StageMinutes( i ), state );
                if( elapsed < start + duration ) {
                    return ( i, ( elapsed - start ) / ( float ) duration );
                }
                start += duration;
            }
            return ( def.StageCount - 1, 1f );
        }

        public static bool IsMature( CropDefinition def, CropState state, long now ) {
            return now - state.PlantedAtMinute >= MatureMinutes( def, state );
        }

        // Produce that can be harvested or eaten right now.
        public static int ReadyYield( CropDefinition def, CropState state, long now ) {
            if( !IsMature( def, state, now ) ) {
                return 0;
            }
            if( def.Lifecycle == CropLifecycle.Annual ) {
                return def.MaxYield;
            }
            return ( int ) Math.Min( def.MaxYield, RawFruit( def, state, now ) );
        }

        // Game minutes until there is something to harvest: 0 when there already is.
        public static long MinutesUntilRipe( CropDefinition def, CropState state, long now ) {
            if( ReadyYield( def, state, now ) > 0 ) {
                return 0;
            }
            if( def.Lifecycle == CropLifecycle.Annual ) {
                return Math.Max( 0, state.PlantedAtMinute + MatureMinutes( def, state ) - now );
            }
            return Math.Max( 0, state.FruitClockStartMinute + ( RawFruit( def, state, now ) + 1 ) * RegrowMinutes( def, state ) - now );
        }

        // Removes `count` ripe fruit from a perennial, keeping the progress of the next one.
        public static void ConsumeFruit( CropDefinition def, CropState state, long now, int count ) {
            long raw = RawFruit( def, state, now );
            long regrow = RegrowMinutes( def, state );
            if( raw >= def.MaxYield ) {
                // Full: the clock was idle, so restart it instead of banking the surplus time.
                state.FruitClockStartMinute = now - ( long ) ( def.MaxYield - count ) * regrow;
            } else {
                state.FruitClockStartMinute += count * regrow;
            }
        }

        // Next moment the stage or ready yield changes, or null when nothing will change by itself.
        public static long? NextChangeAt( CropDefinition def, CropState state, long now ) {
            long elapsed = now - state.PlantedAtMinute;
            if( elapsed < MatureMinutes( def, state ) ) {
                long boundary = 0;
                for( int i = 0; i < def.StageCount - 1; i++ ) {
                    boundary += Scaled( def.StageMinutes( i ), state );
                    if( elapsed < boundary ) {
                        return state.PlantedAtMinute + boundary;
                    }
                }
            }
            if( def.Lifecycle == CropLifecycle.Annual ) {
                return null;
            }
            long raw = RawFruit( def, state, now );
            if( raw >= def.MaxYield ) {
                return null;
            }
            return state.FruitClockStartMinute + ( raw + 1 ) * RegrowMinutes( def, state );
        }

        // Fruit ripened since the clock start, not capped. A clock that runs backwards yields 0, never negative.
        private static long RawFruit( CropDefinition def, CropState state, long now ) {
            long elapsed = now - state.FruitClockStartMinute;
            return elapsed <= 0 ? 0 : elapsed / RegrowMinutes( def, state );
        }
    }
}
