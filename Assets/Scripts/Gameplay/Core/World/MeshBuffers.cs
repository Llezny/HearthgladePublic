using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    public struct Float2 {
        public float X, Y;
        public Float2( float x, float y ) { X = x; Y = y; }
    }

    public struct Float3 {
        public float X, Y, Z;
        public Float3( float x, float y, float z ) { X = x; Y = y; Z = z; }

        public static readonly Float3 Up = new Float3( 0, 1, 0 );

        public static Float3 operator +( Float3 a, Float3 b ) => new Float3( a.X + b.X, a.Y + b.Y, a.Z + b.Z );
        public static Float3 operator -( Float3 a, Float3 b ) => new Float3( a.X - b.X, a.Y - b.Y, a.Z - b.Z );
        public static Float3 operator *( Float3 a, float s ) => new Float3( a.X * s, a.Y * s, a.Z * s );

        public Float3 Normalized() {
            float length = ( float ) Math.Sqrt( X * X + Y * Y + Z * Z );
            return length > 1e-5f ? new Float3( X / length, Y / length, Z / length ) : new Float3( 0, 0, 0 );
        }

        public static Float3 Cross( Float3 a, Float3 b ) =>
            new Float3( a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X );
    }

    public struct Rgba32 {
        public byte R, G, B, A;
        public Rgba32( byte r, byte g, byte b, byte a ) { R = r; G = g; B = b; A = a; }
    }

    /// <summary>Plain mesh data a chunk visual is built from; turned into a UnityEngine.Mesh on the main thread.</summary>
    public sealed class MeshBuffers {
        public readonly List<Float3> Vertices;
        public readonly List<Float3> Normals;
        public readonly List<Float2> Uvs;
        public readonly List<Rgba32> Colors;
        public readonly List<int> Triangles;

        public MeshBuffers( int vertexCapacity = 0, int indexCapacity = 0 ) {
            Vertices = new List<Float3>( vertexCapacity );
            Normals = new List<Float3>( vertexCapacity );
            Uvs = new List<Float2>( vertexCapacity );
            Colors = new List<Rgba32>();
            Triangles = new List<int>( indexCapacity );
        }

        public int VertexCount => Vertices.Count;
        public bool IsEmpty => Triangles.Count == 0;

        public int AddVertex( Float3 position, Float3 normal, Float2 uv ) {
            Vertices.Add( position );
            Normals.Add( normal );
            Uvs.Add( uv );
            return Vertices.Count - 1;
        }

        public void AddTriangle( int a, int b, int c ) {
            Triangles.Add( a );
            Triangles.Add( b );
            Triangles.Add( c );
        }
    }
}
