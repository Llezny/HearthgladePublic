using System;

namespace Hearthglade.Core.Items
{
    // Stable identity of an item (the asset name). Null and "" are the same empty id.
    public readonly struct ItemId : IEquatable<ItemId>
    {
        public string Value { get; }

        public ItemId( string value ) {
            Value = value;
        }

        public bool IsEmpty => string.IsNullOrEmpty( Value );

        public bool Equals( ItemId other ) => string.Equals( Value ?? "", other.Value ?? "", StringComparison.Ordinal );

        public override bool Equals( object obj ) => obj is ItemId other && Equals( other );

        public override int GetHashCode() => ( Value ?? "" ).GetHashCode();

        public override string ToString() => Value ?? "";

        public static bool operator ==( ItemId left, ItemId right ) => left.Equals( right );

        public static bool operator !=( ItemId left, ItemId right ) => !left.Equals( right );
    }
}
