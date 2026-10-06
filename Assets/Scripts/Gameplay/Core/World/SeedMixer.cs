namespace Hearthglade.Core.World {

    /// <summary>
    /// Stable integer hashing used to derive independent seeds (per map, per noise layer, per block)
    /// from the single game seed. Everything here is pure arithmetic, so the results are identical on
    /// every platform and every run - unlike string.GetHashCode() or UnityEngine.Random.
    /// </summary>
    public static class SeedMixer {

        public static uint Mix( uint a, uint b ) {
            unchecked {
                uint h = a ^ 0x9E3779B9u;
                h = ( h ^ b ) * 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        public static int Derive( int seed, int channel ) {
            return unchecked( ( int ) Mix( ( uint ) seed, ( uint ) channel * 0x9E3779B9u + 0x7F4A7C15u ) );
        }

        public static int Derive( int seed, int a, int b ) {
            return unchecked( ( int ) Mix( Mix( ( uint ) seed, ( uint ) a ), ( uint ) b ) );
        }

        // FNV-1a over UTF-16 code units.
        public static int HashString( string value ) {
            unchecked {
                uint h = 2166136261u;
                if( value != null ) {
                    for( int i = 0; i < value.Length; i++ ) {
                        h = ( h ^ value[ i ] ) * 16777619u;
                    }
                }
                return ( int ) h;
            }
        }
    }
}
