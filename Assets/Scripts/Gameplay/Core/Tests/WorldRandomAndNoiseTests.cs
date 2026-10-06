using System;
using System.Linq;
using Hearthglade.Core.World;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class SeedMixerTests {

        [ Test ]
        public void HashString_MatchesFnv1aReferenceVectors( ) {
            // Published FNV-1a 32-bit test vectors.
            Assert.AreEqual( unchecked( ( int ) 0x811C9DC5u ), SeedMixer.HashString( "" ) );
            Assert.AreEqual( unchecked( ( int ) 0xE40C292Cu ), SeedMixer.HashString( "a" ) );
            Assert.AreEqual( unchecked( ( int ) 0xBF9CF968u ), SeedMixer.HashString( "foobar" ) );
        }

        [ Test ]
        public void HashString_Null_IsSameAsEmpty( ) {
            Assert.AreEqual( SeedMixer.HashString( "" ), SeedMixer.HashString( null ) );
        }

        [ Test ]
        public void Derive_IsDeterministic( ) {
            Assert.AreEqual( SeedMixer.Derive( 42, 1 ), SeedMixer.Derive( 42, 1 ) );
            Assert.AreEqual( SeedMixer.Derive( 42, 3, 4 ), SeedMixer.Derive( 42, 3, 4 ) );
        }

        [ Test ]
        public void Derive_DifferentChannelsOrSeeds_GiveDifferentResults( ) {
            var values = Enumerable.Range( 0, 50 ).Select( channel => SeedMixer.Derive( 7, channel ) ).ToArray( );
            Assert.AreEqual( values.Length, values.Distinct( ).Count( ) );
            Assert.AreNotEqual( SeedMixer.Derive( 1, 0 ), SeedMixer.Derive( 2, 0 ) );
            Assert.AreNotEqual( SeedMixer.Derive( 5, 1, 2 ), SeedMixer.Derive( 5, 2, 1 ) );
        }
    }

    public class DeterministicRandomTests {

        [ Test ]
        public void SameSeed_ProducesSameSequence( ) {
            var a = new DeterministicRandom( 123 );
            var b = new DeterministicRandom( 123 );
            for( int i = 0; i < 100; i++ ) {
                Assert.AreEqual( a.NextUInt( ), b.NextUInt( ) );
            }
        }

        [ Test ]
        public void DifferentSeeds_ProduceDifferentSequences( ) {
            var a = new DeterministicRandom( 1 );
            var b = new DeterministicRandom( 2 );
            var same = 0;
            for( int i = 0; i < 100; i++ ) {
                if( a.NextUInt( ) == b.NextUInt( ) ) {
                    same++;
                }
            }
            Assert.Less( same, 3 );
        }

        [ Test ]
        public void NextFloat_StaysInHalfOpenUnitRange( ) {
            var random = new DeterministicRandom( 9 );
            for( int i = 0; i < 100000; i++ ) {
                var value = random.NextFloat( );
                Assert.GreaterOrEqual( value, 0f );
                Assert.Less( value, 1f );
            }
        }

        [ Test ]
        public void NextInt_StaysInRange_AndCoversAllBuckets( ) {
            var random = new DeterministicRandom( 5 );
            var counts = new int[ 10 ];
            for( int i = 0; i < 10000; i++ ) {
                counts[ random.NextInt( 10 ) ]++;
            }
            foreach( var count in counts ) {
                Assert.Greater( count, 800 );
                Assert.Less( count, 1200 );
            }
        }

        [ Test ]
        public void NextInt_NonPositiveBound_Throws( ) {
            var random = new DeterministicRandom( 1 );
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => random.NextInt( 0 ) );
        }

        [ Test ]
        public void Range_StaysWithinBounds( ) {
            var random = new DeterministicRandom( 3 );
            for( int i = 0; i < 10000; i++ ) {
                var value = random.Range( -5f, 20f );
                Assert.GreaterOrEqual( value, -5f );
                Assert.Less( value, 20f );
            }
        }

        [ Test ]
        public void Shuffle_IsAPermutation_AndDeterministic( ) {
            var first = Enumerable.Range( 0, 256 ).ToArray( );
            var second = Enumerable.Range( 0, 256 ).ToArray( );
            new DeterministicRandom( 77 ).Shuffle( first );
            new DeterministicRandom( 77 ).Shuffle( second );

            CollectionAssert.AreEqual( first, second );
            CollectionAssert.AreEquivalent( Enumerable.Range( 0, 256 ).ToArray( ), first );
            CollectionAssert.AreNotEqual( Enumerable.Range( 0, 256 ).ToArray( ), first );
        }
    }

    public class GradientNoiseTests {

        [ Test ]
        public void Noise_IsZeroOnIntegerLatticePoints( ) {
            var noise = new GradientNoise( 1 );
            for( int x = -5; x < 5; x++ ) {
                for( int y = -5; y < 5; y++ ) {
                    Assert.AreEqual( 0.0, noise.Noise( x, y ), 1e-12 );
                }
            }
        }

        [ Test ]
        public void Noise_StaysInsideMinusOneToOne_AndActuallyVaries( ) {
            var noise = new GradientNoise( 2024 );
            double min = double.MaxValue, max = double.MinValue;
            for( int i = 0; i < 200; i++ ) {
                for( int j = 0; j < 200; j++ ) {
                    var value = noise.Noise( i * 0.137 - 10, j * 0.211 - 10 );
                    min = Math.Min( min, value );
                    max = Math.Max( max, value );
                }
            }
            Assert.GreaterOrEqual( min, -1.0 );
            Assert.LessOrEqual( max, 1.0 );
            Assert.Less( min, -0.4 );
            Assert.Greater( max, 0.4 );
        }

        [ Test ]
        public void Noise_SameSeed_SameValues_DifferentSeed_DifferentValues( ) {
            var a = new GradientNoise( 10 );
            var b = new GradientNoise( 10 );
            var c = new GradientNoise( 11 );
            var differing = 0;
            for( int i = 0; i < 100; i++ ) {
                double x = i * 0.31 + 0.17, y = i * 0.23 + 0.41;
                Assert.AreEqual( a.Noise( x, y ), b.Noise( x, y ) );
                if( Math.Abs( a.Noise( x, y ) - c.Noise( x, y ) ) > 1e-9 ) {
                    differing++;
                }
            }
            Assert.Greater( differing, 90 );
        }

        [ Test ]
        public void Noise_IsContinuous( ) {
            var noise = new GradientNoise( 5 );
            for( int i = 0; i < 1000; i++ ) {
                double x = i * 0.0173 - 8, y = i * 0.0291 - 3;
                Assert.AreEqual( noise.Noise( x, y ), noise.Noise( x + 1e-6, y + 1e-6 ), 1e-4 );
            }
        }

        [ Test ]
        public void Noise_HandlesNegativeCoordinates_WithoutException( ) {
            var noise = new GradientNoise( 5 );
            Assert.DoesNotThrow( ( ) => {
                for( int i = -300; i < 300; i++ ) {
                    noise.Noise( i * 0.7, -i * 1.3 );
                }
            } );
        }

        [ Test ]
        public void Octaves_AreNormalised_AndZeroOctavesGiveZero( ) {
            var noise = new GradientNoise( 8 );
            for( int i = 0; i < 500; i++ ) {
                var value = noise.Octaves( i * 0.37 + 0.1, i * 0.19 + 0.3, 5, 0.4, 50 );
                Assert.GreaterOrEqual( value, -1.0 );
                Assert.LessOrEqual( value, 1.0 );
            }
            Assert.AreEqual( 0.0, noise.Octaves( 3.3, 4.4, 0, 2, 0.5 ) );
        }

        [ Test ]
        public void Octaves_SingleOctave_EqualsNoise( ) {
            var noise = new GradientNoise( 8 );
            Assert.AreEqual( noise.Noise( 1.25, 2.75 ), noise.Octaves( 1.25, 2.75, 1, 2, 0.5 ), 1e-12 );
        }
    }
}
