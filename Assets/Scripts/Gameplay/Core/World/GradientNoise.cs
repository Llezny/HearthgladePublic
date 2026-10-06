using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// 2D gradient (Perlin) noise with a seeded permutation table. Output of <see cref="Noise"/> is in [-1, 1]
    /// and is exactly 0 on integer lattice points. No delegates or allocations per sample, so filling a
    /// whole map is cheap, and it has no UnityEngine dependency so it can run on any thread.
    /// </summary>
    public sealed class GradientNoise {

        private const int TableSize = 256;

        // The classic 12 edge gradients with the z component dropped.
        private static readonly double[] GradX = { 1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0 };
        private static readonly double[] GradY = { 1, 1, -1, -1, 0, 0, 0, 0, 1, -1, 1, -1 };

        private readonly int[] perm = new int[ TableSize * 2 ];

        public GradientNoise( int seed ) {
            var table = new int[ TableSize ];
            for( int i = 0; i < TableSize; i++ ) {
                table[ i ] = i;
            }
            new DeterministicRandom( seed ).Shuffle( table );
            for( int i = 0; i < TableSize; i++ ) {
                perm[ i ] = table[ i ];
                perm[ i + TableSize ] = table[ i ];
            }
        }

        public double Noise( double x, double y ) {
            double floorX = Math.Floor( x );
            double floorY = Math.Floor( y );
            int xi = ( int ) floorX & ( TableSize - 1 );
            int yi = ( int ) floorY & ( TableSize - 1 );
            double xf = x - floorX;
            double yf = y - floorY;

            int a = perm[ xi ];
            int b = perm[ xi + 1 ];
            int g00 = perm[ a + yi ];
            int g01 = perm[ a + yi + 1 ];
            int g10 = perm[ b + yi ];
            int g11 = perm[ b + yi + 1 ];

            double u = Fade( xf );
            double v = Fade( yf );
            double bottom = Lerp( Dot( g00, xf, yf ), Dot( g10, xf - 1, yf ), u );
            double top = Lerp( Dot( g01, xf, yf - 1 ), Dot( g11, xf - 1, yf - 1 ), u );
            return Lerp( bottom, top, v );
        }

        /// <summary>
        /// Sum of octaves, normalised by the total amplitude so the result stays in [-1, 1].
        /// Same formula the map presets were tuned for: frequency is multiplied by lacunarity and
        /// amplitude by persistence on every octave.
        /// </summary>
        public double Octaves( double x, double y, int octaves, double lacunarity, double persistence ) {
            double value = 0;
            double amplitude = 1;
            double frequency = 1;
            double totalAmplitude = 0;
            for( int i = 0; i < octaves; i++ ) {
                value += amplitude * Noise( x * frequency, y * frequency );
                totalAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return totalAmplitude == 0 ? 0 : value / totalAmplitude;
        }

        private static double Dot( int hash, double x, double y ) {
            int g = hash % 12;
            return GradX[ g ] * x + GradY[ g ] * y;
        }

        private static double Fade( double t ) {
            return t * t * t * ( t * ( t * 6 - 15 ) + 10 );
        }

        private static double Lerp( double a, double b, double t ) {
            return a + t * ( b - a );
        }
    }
}
