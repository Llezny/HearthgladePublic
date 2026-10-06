using System;
using System.ComponentModel;
using Hearthglade.Gameplay.Helpers;
using UnityEngine;

namespace Hearthglade.Gameplay.Common
{
    /// <summary>
    /// Serializable Vector2Int
    /// </summary>
    [TypeConverter(typeof(StringToSerializableVector2IntConverter))]
    [System.Serializable]
    public struct SerializableVector2Int : IEquatable<SerializableVector2Int>{
        public int x;
        public int y;

        public static SerializableVector2Int zero => new SerializableVector2Int( 0, 0 );

        public SerializableVector2Int(int rX, int rY) {
            x = rX;
            y = rY;
        }

        public override string ToString()
        {
            return String.Format("[{0}, {1}]", x, y);
        }

        public override bool Equals( object obj )
        {
            return this.Equals( (SerializableVector2Int)obj );
        }

        public bool Equals(SerializableVector2Int obj) {
            return this == obj;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public static implicit operator Vector2Int(SerializableVector2Int rValue)
        {
            return new Vector2Int(rValue.x, rValue.y);
        }

        public static implicit operator SerializableVector2Int(Vector2Int rValue)
        {
            return new SerializableVector2Int(rValue.x, rValue.y);
        }

        public static bool operator !=(SerializableVector2Int lhs, SerializableVector2Int rhs)
        {
            return lhs.x != rhs.x || lhs.y != rhs.y;
        }

        public static bool operator ==(SerializableVector2Int lhs, SerializableVector2Int rhs)
        {
            return lhs.x == rhs.x && lhs.y == rhs.y;
        }

        public static SerializableVector2Int operator +(SerializableVector2Int lhs, SerializableVector2Int rhs)
        {
            return new SerializableVector2Int(lhs.x + rhs.x, lhs.y + rhs.y);
        }

    }
}