using System;

namespace Hearthglade.Core.Expedition {

    /// <summary>
    /// How far a direction and depth push the climate of an expedition island (equalised units, see
    /// <c>WorldShapeSettings.TemperatureOffset</c>): north is colder, south warmer and drier, east wetter, west drier.
    /// </summary>
    public static class ExpeditionClimate {

        /// <summary>Climate shift per depth (3 deep = 0.6, which turns about a fifth of the map into a different biome).</summary>
        public const double StepPerDepth = 0.2;

        public static void Offsets( ExpeditionTarget target, out double temperature, out double humidity ) {
            double push = Math.Max( 0, target.Depth ) * StepPerDepth;
            temperature = 0;
            humidity = 0;
            switch( target.Direction ) {
                case Direction.North:
                    temperature = -push;
                    break;
                case Direction.South:
                    temperature = push;
                    humidity = -push * 0.5;
                    break;
                case Direction.East:
                    humidity = push;
                    break;
                case Direction.West:
                    humidity = -push;
                    break;
            }
            temperature = Math.Max( -1, Math.Min( 1, temperature ) );
            humidity = Math.Max( -1, Math.Min( 1, humidity ) );
        }
    }
}
