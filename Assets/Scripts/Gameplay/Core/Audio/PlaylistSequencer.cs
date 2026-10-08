using System;

namespace Hearthglade.Core.Audio {

    /// <summary>
    /// Decides which track of a playlist plays next. In order it walks the tracks round and round; shuffled it
    /// plays every track once per round, in a fresh random order, and never starts a round with the track that
    /// ended the previous one (so a short playlist does not repeat itself back to back).
    /// </summary>
    public sealed class PlaylistSequencer {
        private readonly int count;
        private readonly bool shuffle;
        private readonly Random random;
        private readonly int[] bag;
        private int bagPosition;
        private int last = -1;

        public PlaylistSequencer( int trackCount, bool shuffle, Random random = null ) {
            if ( trackCount <= 0 ) {
                throw new ArgumentOutOfRangeException( nameof( trackCount ), "A playlist needs at least one track." );
            }
            this.count = trackCount;
            this.shuffle = shuffle;
            this.random = random ?? new Random( );
            this.bag = new int[ trackCount ];
            this.bagPosition = trackCount;
        }

        public int Next( ) {
            last = shuffle ? NextShuffled( ) : ( last + 1 ) % count;
            return last;
        }

        private int NextShuffled( ) {
            if ( bagPosition >= count ) {
                Refill( );
            }
            return bag[ bagPosition++ ];
        }

        private void Refill( ) {
            for ( int i = 0; i < count; i++ ) {
                bag[ i ] = i;
            }
            for ( int i = count - 1; i > 0; i-- ) {
                int j = random.Next( i + 1 );
                ( bag[ i ], bag[ j ] ) = ( bag[ j ], bag[ i ] );
            }
            if ( count > 1 && bag[ 0 ] == last ) {
                ( bag[ 0 ], bag[ count - 1 ] ) = ( bag[ count - 1 ], bag[ 0 ] );
            }
            bagPosition = 0;
        }
    }
}
