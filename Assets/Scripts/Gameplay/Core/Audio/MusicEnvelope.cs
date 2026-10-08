namespace Hearthglade.Core.Audio {

    /// <summary>
    /// The loudness (0..1) of a track at a moment of its playback: rising over <c>fadeIn</c> seconds from its
    /// start and falling over <c>fadeOut</c> seconds towards its end. A fade of 0 seconds means no fade.
    /// The curve is a smoothstep, so a fade has no audible corner at either end.
    /// </summary>
    public static class MusicEnvelope {

        public static float Evaluate( float time, float length, float fadeIn, float fadeOut ) {
            float volume = 1f;
            if ( fadeIn > 0f ) {
                volume = System.Math.Min( volume, Smooth( time / fadeIn ) );
            }
            if ( fadeOut > 0f ) {
                volume = System.Math.Min( volume, Smooth( ( length - time ) / fadeOut ) );
            }
            return volume;
        }

        /// <summary>A fade can be at most half of the track, so the fade in and the fade out never overlap.</summary>
        public static float ClampFade( float fadeSeconds, float length ) {
            return System.Math.Max( 0f, System.Math.Min( fadeSeconds, length * 0.5f ) );
        }

        private static float Smooth( float x ) {
            x = x < 0f ? 0f : x > 1f ? 1f : x;
            return x * x * ( 3f - 2f * x );
        }
    }
}
