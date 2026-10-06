using System;

namespace Hearthglade.Core.Expedition {

    /// <summary>The saved form of <see cref="ExpeditionProgress"/>: the deepest completed depth per direction, in enum order.</summary>
    [Serializable]
    public class ExpeditionProgressData {
        public int[] MaxDepth = new int[ ExpeditionProgress.DirectionCount ];
    }

    /// <summary>
    /// How far the player has sailed in each direction. A depth can be sailed once the one before it was completed in that
    /// direction, so the first trip of every direction is depth 1 and each finished trip opens the next depth, up to <see cref="DepthCap"/>.
    /// </summary>
    public sealed class ExpeditionProgress {
        public const int DirectionCount = 4;
        public const int DepthCap = 10;

        private readonly int[] maxDepth = new int[ DirectionCount ];

        public int MaxDepthReached( Direction direction ) => maxDepth[ ( int ) direction ];

        /// <summary>The deepest depth that may be sailed in the direction right now.</summary>
        public int NextDepth( Direction direction ) => Math.Min( DepthCap, MaxDepthReached( direction ) + 1 );

        public bool CanSail( ExpeditionTarget target ) => target.Depth >= 1 && target.Depth <= NextDepth( target.Direction );

        /// <returns>True when the target was a new record for its direction (it opened a deeper trip).</returns>
        public bool Complete( ExpeditionTarget target ) {
            if( !CanSail( target ) || target.Depth <= maxDepth[ ( int ) target.Direction ] ) {
                return false;
            }
            maxDepth[ ( int ) target.Direction ] = target.Depth;
            return true;
        }

        public ExpeditionProgressData ToData() {
            return new ExpeditionProgressData { MaxDepth = ( int[] ) maxDepth.Clone() };
        }

        /// <summary>Loads a save; missing, short or out of range values (an old save) are treated as nothing sailed.</summary>
        public void Load( ExpeditionProgressData data ) {
            Array.Clear( maxDepth, 0, maxDepth.Length );
            if( data?.MaxDepth == null ) {
                return;
            }
            for( int i = 0; i < DirectionCount && i < data.MaxDepth.Length; i++ ) {
                maxDepth[ i ] = Math.Max( 0, Math.Min( DepthCap, data.MaxDepth[ i ] ) );
            }
        }
    }
}
