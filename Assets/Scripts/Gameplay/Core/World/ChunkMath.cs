using System;

namespace Hearthglade.Core.World {

    public readonly struct ChunkCoord : IEquatable<ChunkCoord> {
        public readonly int X;
        public readonly int Y;

        public ChunkCoord( int x, int y ) {
            X = x;
            Y = y;
        }

        public bool Equals( ChunkCoord other ) => X == other.X && Y == other.Y;
        public override bool Equals( object obj ) => obj is ChunkCoord other && Equals( other );
        public override int GetHashCode() => unchecked( X * 397 ^ Y );
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==( ChunkCoord a, ChunkCoord b ) => a.Equals( b );
        public static bool operator !=( ChunkCoord a, ChunkCoord b ) => !a.Equals( b );
    }

    /// <summary>Integer-only grid/chunk arithmetic, so a block can never disagree with its chunk because of float rounding.</summary>
    public static class ChunkMath {

        /// <summary>Division that rounds towards negative infinity (unlike C#'s /, which truncates towards zero).</summary>
        public static int FloorDiv( int value, int divisor ) {
            int quotient = value / divisor;
            bool inexact = value % divisor != 0;
            bool negativeResult = ( value < 0 ) != ( divisor < 0 );
            return inexact && negativeResult ? quotient - 1 : quotient;
        }

        public static ChunkCoord ChunkOf( int gridX, int gridY, int chunkSize ) {
            return new ChunkCoord( FloorDiv( gridX, chunkSize ), FloorDiv( gridY, chunkSize ) );
        }

        /// <summary>A negative size means an endless map: every chunk is inside it.</summary>
        public static bool IsInMap( ChunkCoord chunk, int mapSizeInChunks ) {
            if( mapSizeInChunks < 0 ) {
                return true;
            }
            return chunk.X >= 0 && chunk.X < mapSizeInChunks && chunk.Y >= 0 && chunk.Y < mapSizeInChunks;
        }

        /// <summary>Chebyshev distance: 1 for all eight neighbours.</summary>
        public static int Distance( ChunkCoord a, ChunkCoord b ) {
            return Math.Max( Math.Abs( a.X - b.X ), Math.Abs( a.Y - b.Y ) );
        }
    }

    /// <summary>Conversion between block grid indices and world coordinates for the square tile layout.</summary>
    public static class GridMath {

        /// <summary>Block cell g covers world [g - 0.5, g + 0.5) tiles, centred on GridToWorld(g).</summary>
        public static int WorldToGrid( float world, float tileSize ) {
            return ( int ) Math.Floor( world / tileSize + 0.5 );
        }

        public static float GridToWorld( int grid, float tileSize ) {
            return grid * tileSize;
        }
    }
}
