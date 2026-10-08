using System;
using Hearthglade.Core.Items;

namespace Hearthglade.Core.Stats
{
    public enum Discomfort
    {
        None = 0,
        Cold = 1,
        Heat = 2,
    }

    // How the weather treats the player right now: too cold or too hot, and by how much more than clothing, fire and a roof can make up for.
    public readonly struct ExposureState
    {
        public Discomfort Kind { get; }
        // What is left of the cold or the heat after everything that helps, in °C; 0 = comfortable.
        public float Excess { get; }

        public ExposureState( Discomfort kind, float excess ) {
            Kind = excess > 0f ? kind : Discomfort.None;
            Excess = Math.Max( 0f, excess );
        }

        // Only a clear excess hurts, so the border of the comfortable band is not a cliff.
        public bool IsHarmful => Excess > Exposure.HarmThreshold;

        public static ExposureState Comfortable => default;
    }

    // The rules of cold and heat (docs/EQUIPMENT_PLAN.md, phase 6), in °C. Each map spreads the climate scale of its cells (-1..1) over its
    // own range of temperatures (MapSO), the night takes some off, and the player is comfortable within a band. Outside it, a roof takes the
    // edge off (both ways: shelter and shade), a burning fire nearby cancels the cold, and each point of clothing protection covers
    // ProtectionCover degrees of what is left. Whatever is still left is the excess; a harmful one drains health.
    public static class Exposure
    {
        // The range of a map that sets none.
        public const float DefaultMinTemperature = -10f;
        public const float DefaultMaxTemperature = 30f;

        public const float ComfortMin = 0f;
        public const float ComfortMax = 24f;
        public const float NightShift = -4f;
        // Caves keep the same cool, comfortable air day and night.
        public const float UndergroundTemperature = 12f;
        // The full Traveller set (cold 1.1) just covers the coldest cell of the home map at night (-14 °C: 14 below the band).
        public const float ProtectionCover = 12f;
        public const float IndoorsCover = 6f;
        public const float HarmThreshold = 1f;

        // A cell's climate (-1..1) in °C on a map whose climate spans minTemperature..maxTemperature.
        public static float Celsius( float climateTemperature, float minTemperature, float maxTemperature ) {
            float t = ( Math.Max( -1f, Math.Min( 1f, climateTemperature ) ) + 1f ) / 2f;
            return minTemperature + ( maxTemperature - minTemperature ) * t;
        }

        public static float AmbientTemperature( float celsius, bool isNight, bool underground ) {
            if( underground ) {
                return UndergroundTemperature;
            }
            return celsius + ( isNight ? NightShift : 0f );
        }

        public static ExposureState Assess( float temperature, Protection protection, bool nearFire, bool indoors ) {
            float cold = Math.Max( 0f, ComfortMin - temperature );
            float heat = Math.Max( 0f, temperature - ComfortMax );
            if( nearFire ) {
                cold = 0f;
            }
            if( indoors ) {
                cold = Math.Max( 0f, cold - IndoorsCover );
                heat = Math.Max( 0f, heat - IndoorsCover );
            }
            cold = Math.Max( 0f, cold - protection.Cold * ProtectionCover );
            heat = Math.Max( 0f, heat - protection.Heat * ProtectionCover );
            return cold >= heat ? new ExposureState( Discomfort.Cold, cold ) : new ExposureState( Discomfort.Heat, heat );
        }
    }
}
