using System;

namespace Hearthglade.Core.Expedition {

    public enum Direction { North = 0, East = 1, South = 2, West = 3 }

    /// <summary>Where an expedition sails: a direction from home and how far (depth 1 = the first island that way).</summary>
    public readonly struct ExpeditionTarget : IEquatable<ExpeditionTarget> {
        public readonly Direction Direction;
        public readonly int Depth;

        public ExpeditionTarget( Direction direction, int depth ) {
            Direction = direction;
            Depth = depth;
        }

        public bool Equals( ExpeditionTarget other ) => Direction == other.Direction && Depth == other.Depth;

        public override bool Equals( object obj ) => obj is ExpeditionTarget other && Equals( other );

        public override int GetHashCode() => ( ( int ) Direction * 397 ) ^ Depth;

        public override string ToString() => $"{Direction} {Depth}";
    }
}
