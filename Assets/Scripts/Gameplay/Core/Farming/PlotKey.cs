using System;

namespace Hearthglade.Core.Farming {

    // Anchor cell of a plot. Stable across launches, unlike scene-object hashes.
    public readonly struct PlotKey : IEquatable<PlotKey>, IComparable<PlotKey> {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public PlotKey( int x, int y, int z ) {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals( PlotKey other ) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals( object obj ) => obj is PlotKey other && Equals( other );

        public override int GetHashCode() => HashCode.Combine( X, Y, Z );

        public int CompareTo( PlotKey other ) {
            int c = X.CompareTo( other.X );
            if( c != 0 ) {
                return c;
            }
            c = Y.CompareTo( other.Y );
            return c != 0 ? c : Z.CompareTo( other.Z );
        }

        public override string ToString() => $"({X}, {Y}, {Z})";

        public static bool operator ==( PlotKey left, PlotKey right ) => left.Equals( right );

        public static bool operator !=( PlotKey left, PlotKey right ) => !left.Equals( right );
    }
}
