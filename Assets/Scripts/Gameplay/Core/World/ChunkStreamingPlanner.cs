using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>
    /// Decides which chunks around the player should be loaded and what changed since the last update.
    /// Pure bookkeeping (no GameObjects, no async), so the rules that used to be spread over ChunkManager -
    /// map bounds, "walked back in before the hide ran", nearest-first ordering - can be unit tested.
    /// </summary>
    public sealed class ChunkStreamingPlanner {

        private readonly int mapSizeInChunks;
        private readonly int radius;
        private readonly HashSet<ChunkCoord> loaded = new HashSet<ChunkCoord>();
        private readonly List<ChunkCoord> toShow = new List<ChunkCoord>();
        private readonly List<ChunkCoord> toHide = new List<ChunkCoord>();

        public ChunkStreamingPlanner( int mapSizeInChunks, int radius = 1 ) {
            this.mapSizeInChunks = mapSizeInChunks;
            this.radius = radius;
        }

        public bool HasCenter { get; private set; }
        public ChunkCoord Center { get; private set; }

        /// <summary>Chunks that should currently be active.</summary>
        public HashSet<ChunkCoord> Loaded => loaded;

        /// <summary>Chunks that became active with the last update, nearest to the player first.</summary>
        public IReadOnlyList<ChunkCoord> ToShow => toShow;

        /// <summary>Chunks that became inactive with the last update.</summary>
        public IReadOnlyList<ChunkCoord> ToHide => toHide;

        /// <summary>Moves the window. Returns false (and reports nothing) when the player is still in the same chunk.</summary>
        public bool Update( ChunkCoord center ) {
            if( HasCenter && Center == center ) {
                return false;
            }
            Apply( center );
            return true;
        }

        /// <summary>Recomputes the window even for an unchanged centre (e.g. after teleporting to another map).</summary>
        public void Refresh( ChunkCoord center ) {
            Apply( center );
        }

        /// <summary>All chunks within <paramref name="ringRadius"/> of the centre that lie inside the map, nearest first.</summary>
        public void GetRing( ChunkCoord center, int ringRadius, List<ChunkCoord> result ) {
            result.Clear();
            for( int y = center.Y - ringRadius; y <= center.Y + ringRadius; y++ ) {
                for( int x = center.X - ringRadius; x <= center.X + ringRadius; x++ ) {
                    var chunk = new ChunkCoord( x, y );
                    if( ChunkMath.IsInMap( chunk, mapSizeInChunks ) ) {
                        result.Add( chunk );
                    }
                }
            }
            // List.Sort is unstable; the tie-breaker keeps the order deterministic.
            result.Sort( ( a, b ) => {
                int byDistance = ChunkMath.Distance( a, center ).CompareTo( ChunkMath.Distance( b, center ) );
                if( byDistance != 0 ) {
                    return byDistance;
                }
                int byY = a.Y.CompareTo( b.Y );
                return byY != 0 ? byY : a.X.CompareTo( b.X );
            } );
        }

        private void Apply( ChunkCoord center ) {
            var ring = new List<ChunkCoord>();
            GetRing( center, radius, ring );

            toShow.Clear();
            toHide.Clear();
            var next = new HashSet<ChunkCoord>( ring );
            foreach( var chunk in ring ) {
                if( !loaded.Contains( chunk ) ) {
                    toShow.Add( chunk );
                }
            }
            foreach( var chunk in loaded ) {
                if( !next.Contains( chunk ) ) {
                    toHide.Add( chunk );
                }
            }

            loaded.Clear();
            loaded.UnionWith( next );
            Center = center;
            HasCenter = true;
        }
    }
}
