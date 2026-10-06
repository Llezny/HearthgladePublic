using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World {

    /// <summary>Where the parts of a placed site land, and the ground painted around it.</summary>
    public static class PoiLayout {

        /// <summary>Turns an offset (cells from the centre of the site) around the vertical axis by the rotation of the site.</summary>
        public static void Rotate( float rotationYDegrees, float x, float z, out float rotatedX, out float rotatedZ ) {
            float angle = rotationYDegrees * ( float ) ( Math.PI / 180.0 );
            float sin = ( float ) Math.Sin( angle ), cos = ( float ) Math.Cos( angle );
            rotatedX = x * cos + z * sin;
            rotatedZ = -x * sin + z * cos;
        }

        /// <summary>
        /// Writes the path segments of every site into <see cref="GeneratedTerrain.Patch"/>, which makes the ground look like the
        /// segment's biome. Only ground cells are painted; the cells keep their biome, block and resources.
        /// Run after the sites are placed and before the blocks are made from the terrain.
        /// </summary>
        public static int PaintPaths( GeneratedTerrain terrain, IReadOnlyList<PoiPlacement> sites, IReadOnlyList<PoiRule> rules, Func<int, int, bool> isGround ) {
            int painted = 0;
            foreach( var site in sites ) {
                var paths = rules[ site.Type ].Paths;
                if( paths == null ) {
                    continue;
                }
                foreach( var segment in paths ) {
                    Rotate( site.RotationY, segment.FromX, segment.FromZ, out float fromX, out float fromZ );
                    Rotate( site.RotationY, segment.ToX, segment.ToZ, out float toX, out float toZ );
                    fromX += site.CellX; fromZ += site.CellY;
                    toX += site.CellX; toZ += site.CellY;

                    float reach = segment.HalfWidth;
                    int minX = ( int ) Math.Floor( Math.Min( fromX, toX ) - reach ), maxX = ( int ) Math.Ceiling( Math.Max( fromX, toX ) + reach );
                    int minZ = ( int ) Math.Floor( Math.Min( fromZ, toZ ) - reach ), maxZ = ( int ) Math.Ceiling( Math.Max( fromZ, toZ ) + reach );
                    for( int z = Math.Max( 0, minZ ); z <= Math.Min( terrain.Size - 1, maxZ ); z++ ) {
                        for( int x = Math.Max( 0, minX ); x <= Math.Min( terrain.Size - 1, maxX ); x++ ) {
                            if( DistanceToSegment( x, z, fromX, fromZ, toX, toZ ) > reach || !isGround( x, z ) ) {
                                continue;
                            }
                            terrain.Patch[ terrain.Index( x, z ) ] = ( byte ) ( segment.Look + 1 );
                            painted++;
                        }
                    }
                }
            }
            return painted;
        }

        private static float DistanceToSegment( float px, float pz, float ax, float az, float bx, float bz ) {
            float dx = bx - ax, dz = bz - az;
            float lengthSquared = dx * dx + dz * dz;
            float t = lengthSquared <= 1e-6f ? 0f : Math.Max( 0f, Math.Min( 1f, ( ( px - ax ) * dx + ( pz - az ) * dz ) / lengthSquared ) );
            float cx = ax + t * dx - px, cz = az + t * dz - pz;
            return ( float ) Math.Sqrt( cx * cx + cz * cz );
        }
    }
}
