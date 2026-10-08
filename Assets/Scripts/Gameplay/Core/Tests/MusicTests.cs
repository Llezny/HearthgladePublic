using System;
using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Audio;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {

    public class MusicTests {

        private static List<int> Take( PlaylistSequencer sequencer, int n ) {
            return Enumerable.Range( 0, n ).Select( _ => sequencer.Next( ) ).ToList( );
        }

        [ Test ]
        public void InOrder_WalksTheTracksRoundAndRound( ) {
            var sequencer = new PlaylistSequencer( 3, shuffle: false );

            CollectionAssert.AreEqual( new[] { 0, 1, 2, 0, 1, 2, 0 }, Take( sequencer, 7 ) );
        }

        [ Test ]
        public void Shuffled_PlaysEveryTrackOncePerRound( ) {
            var sequencer = new PlaylistSequencer( 5, shuffle: true, new Random( 7 ) );

            for ( int round = 0; round < 20; round++ ) {
                CollectionAssert.AreEquivalent( new[] { 0, 1, 2, 3, 4 }, Take( sequencer, 5 ) );
            }
        }

        [ Test ]
        public void Shuffled_NeverRepeatsATrackBackToBack( ) {
            for ( int seed = 0; seed < 50; seed++ ) {
                var sequencer = new PlaylistSequencer( 3, shuffle: true, new Random( seed ) );
                var played = Take( sequencer, 60 );

                for ( int i = 1; i < played.Count; i++ ) {
                    Assert.AreNotEqual( played[ i - 1 ], played[ i ], $"seed {seed}, position {i}" );
                }
            }
        }

        [ TestCase( false ) ]
        [ TestCase( true ) ]
        public void OneTrackPlaylist_RepeatsThatTrack( bool shuffle ) {
            var sequencer = new PlaylistSequencer( 1, shuffle );

            CollectionAssert.AreEqual( new[] { 0, 0, 0 }, Take( sequencer, 3 ) );
        }

        [ Test ]
        public void EmptyPlaylist_IsRejected( ) {
            Assert.Throws<ArgumentOutOfRangeException>( ( ) => new PlaylistSequencer( 0, shuffle: false ) );
        }

        [ Test ]
        public void Envelope_RisesOverTheFadeInAndFallsOverTheFadeOut( ) {
            Assert.AreEqual( 0f, MusicEnvelope.Evaluate( 0f, 60f, 4f, 4f ), 1e-5f );
            Assert.AreEqual( 0.5f, MusicEnvelope.Evaluate( 2f, 60f, 4f, 4f ), 1e-5f );
            Assert.AreEqual( 1f, MusicEnvelope.Evaluate( 30f, 60f, 4f, 4f ), 1e-5f );
            Assert.AreEqual( 0.5f, MusicEnvelope.Evaluate( 58f, 60f, 4f, 4f ), 1e-5f );
            Assert.AreEqual( 0f, MusicEnvelope.Evaluate( 60f, 60f, 4f, 4f ), 1e-5f );
        }

        [ Test ]
        public void Envelope_WithoutFades_IsFullVolumeFromStartToEnd( ) {
            Assert.AreEqual( 1f, MusicEnvelope.Evaluate( 0f, 60f, 0f, 0f ), 1e-5f );
            Assert.AreEqual( 1f, MusicEnvelope.Evaluate( 60f, 60f, 0f, 0f ), 1e-5f );
        }

        [ Test ]
        public void Envelope_CanFadeOnlyOneEnd( ) {
            Assert.AreEqual( 1f, MusicEnvelope.Evaluate( 0f, 60f, 0f, 4f ), 1e-5f );
            Assert.AreEqual( 0f, MusicEnvelope.Evaluate( 60f, 60f, 0f, 4f ), 1e-5f );
            Assert.AreEqual( 0f, MusicEnvelope.Evaluate( 0f, 60f, 4f, 0f ), 1e-5f );
            Assert.AreEqual( 1f, MusicEnvelope.Evaluate( 60f, 60f, 4f, 0f ), 1e-5f );
        }

        [ Test ]
        public void ClampFade_KeepsTheTwoFadesFromOverlapping( ) {
            Assert.AreEqual( 5f, MusicEnvelope.ClampFade( 8f, 10f ), 1e-5f );
            Assert.AreEqual( 3f, MusicEnvelope.ClampFade( 3f, 10f ), 1e-5f );
            Assert.AreEqual( 0f, MusicEnvelope.ClampFade( -1f, 10f ), 1e-5f );
        }
    }
}
