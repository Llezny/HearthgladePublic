namespace Hearthglade.Core.Farming {

    // Temperature and humidity on the world scale -1..1.
    public readonly struct Climate {
        public float Temperature { get; }
        public float Humidity { get; }

        // One byte per axis in saves: 0 = unknown, 1..255 = -1..1.
        public static byte Encode( float value ) => ( byte ) ( ( int ) System.Math.Round( ( System.Math.Max( -1f, System.Math.Min( 1f, value ) ) + 1f ) * 127f ) + 1 );

        public static float Decode( byte code ) => ( code - 1 ) / 127f - 1f;

        public Climate( float temperature, float humidity ) {
            Temperature = temperature;
            Humidity = humidity;
        }
    }

    // The part of the climate a crop grows at full speed in.
    public readonly struct ClimateRange {
        public float TemperatureFrom { get; }
        public float TemperatureTo { get; }
        public float HumidityFrom { get; }
        public float HumidityTo { get; }

        public ClimateRange( float temperatureFrom, float temperatureTo, float humidityFrom, float humidityTo ) {
            TemperatureFrom = temperatureFrom;
            TemperatureTo = temperatureTo;
            HumidityFrom = humidityFrom;
            HumidityTo = humidityTo;
        }

        public static ClimateRange Anywhere => new ClimateRange( -1f, 1f, -1f, 1f );
    }
}
