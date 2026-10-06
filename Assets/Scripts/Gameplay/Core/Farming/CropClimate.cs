using System;

namespace Hearthglade.Core.Farming {

    public enum ClimateIssue {
        None,
        TooCold,
        TooHot,
        TooDry,
        TooWet,
    }

    // How well a climate suits a crop. A wrong climate only slows growth down, it never kills or shrinks the harvest.
    public static class CropClimate {
        // Distance outside the good range at which a crop is as slow as it gets.
        public const float Margin = 0.3f;
        public const int MinSpeedPercent = 50;

        // 1 inside the range, falling linearly to 0 at Margin outside it.
        public static float AxisFit( float value, float from, float to ) {
            float distance = value < from ? from - value : value > to ? value - to : 0f;
            return Math.Max( 0f, 1f - distance / Margin );
        }

        public static float Fit( ClimateRange range, Climate climate ) {
            return AxisFit( climate.Temperature, range.TemperatureFrom, range.TemperatureTo )
                * AxisFit( climate.Humidity, range.HumidityFrom, range.HumidityTo );
        }

        public static int SpeedPercent( float fit ) {
            float clamped = Math.Min( 1f, Math.Max( 0f, fit ) );
            return ( int ) Math.Round( MinSpeedPercent + ( 100 - MinSpeedPercent ) * clamped );
        }

        public static int SpeedPercent( ClimateRange range, Climate climate ) => SpeedPercent( Fit( range, climate ) );

        // The axis that costs the most speed, None when the crop is at full speed.
        public static ClimateIssue Issue( ClimateRange range, Climate climate ) {
            float temperatureFit = AxisFit( climate.Temperature, range.TemperatureFrom, range.TemperatureTo );
            float humidityFit = AxisFit( climate.Humidity, range.HumidityFrom, range.HumidityTo );
            if( temperatureFit >= 1f && humidityFit >= 1f ) {
                return ClimateIssue.None;
            }
            if( temperatureFit <= humidityFit ) {
                return climate.Temperature < range.TemperatureFrom ? ClimateIssue.TooCold : ClimateIssue.TooHot;
            }
            return climate.Humidity < range.HumidityFrom ? ClimateIssue.TooDry : ClimateIssue.TooWet;
        }
    }
}
