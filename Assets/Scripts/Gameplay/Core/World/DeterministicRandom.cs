using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Small SplitMix64-based generator. Deterministic for a given seed regardless of runtime
    /// (System.Random(seed) is not guaranteed to be stable between .NET versions), and independent of the
    /// global UnityEngine.Random state that the rest of the game consumes between frames.
    /// </summary>
    public sealed class DeterministicRandom {

        private ulong state;

        public DeterministicRandom( int seed ) {
            unchecked {
                state = ( ulong ) ( uint ) seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL;
            }
        }

        public uint NextUInt() {
            unchecked {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = ( z ^ ( z >> 30 ) ) * 0xBF58476D1CE4E5B9UL;
                z = ( z ^ ( z >> 27 ) ) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return ( uint ) ( z >> 32 );
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() {
            return ( NextUInt() >> 8 ) * ( 1f / 16777216f );
        }

        /// <summary>Uniform in [min, max).</summary>
        public float Range( float min, float max ) {
            return min + ( max - min ) * NextFloat();
        }

        /// <summary>Uniform in [0, maxExclusive).</summary>
        public int NextInt( int maxExclusive ) {
            if( maxExclusive <= 0 ) {
                throw new ArgumentOutOfRangeException( nameof( maxExclusive ) );
            }
            return ( int ) ( ( ( ulong ) NextUInt() * ( uint ) maxExclusive ) >> 32 );
        }

        public void Shuffle<T>( T[] array ) {
            for( int n = array.Length; n > 1; ) {
                int k = NextInt( n-- );
                ( array[ n ], array[ k ] ) = ( array[ k ], array[ n ] );
            }
        }
    }
}
