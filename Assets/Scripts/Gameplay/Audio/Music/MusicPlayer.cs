using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Audio;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>
    /// Plays the playlists. Songs are loaded through Addressables just before they play (the next one is loaded
    /// while the current one plays, so there is no hitch between them) and released when they end.
    /// A playlist runs as one async loop; replacing the playlist cancels that loop, and the song it was playing
    /// fades out on its own while the new playlist starts.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour, IMusicPlayer {

        // A song that ends sooner than this after it started did not really play (no audio device, a broken clip).
        private const float MinPlayedSeconds = 1f;
        private const float RetryAfterBrokenSongSeconds = 5f;

        private AudioConfigSO config;
        private readonly Stack<AudioSource> freeSources = new( );
        private PlaylistRun run;
        private float volume = 1f;
        private bool quitting;
        private CancellationToken destroyToken;

        public MusicPlaylistSO CurrentPlaylist { get; private set; }

        public float Volume {
            get => volume;
            set => volume = Mathf.Clamp01( value );
        }

        [ Inject ]
        public void Construct( AudioConfigSO config ) {
            this.config = config;
            volume = config.MusicVolume;
        }

        public void Play( MusicPlaylistSO playlist ) {
            if ( quitting || playlist == null || playlist == CurrentPlaylist ) {
                return;
            }
            bool followsOtherMusic = CurrentPlaylist != null;
            StopCurrentRun( config.PlaylistSwitchFadeSeconds );
            CurrentPlaylist = playlist;
            run = new PlaylistRun( );
            RunPlaylistAsync( run, playlist, followsOtherMusic ).Forget( );
        }

        public void Stop( float fadeSeconds = -1f ) {
            StopCurrentRun( fadeSeconds < 0f ? config.PlaylistSwitchFadeSeconds : fadeSeconds );
            CurrentPlaylist = null;
        }

        private void Awake( ) {
            // Unity requires this token to be read once before the object is destroyed.
            destroyToken = destroyCancellationToken;
        }

        private void OnApplicationQuit( ) {
            quitting = true;
        }

        private void OnDestroy( ) {
            quitting = true;
            StopCurrentRun( 0f );
        }

        private void StopCurrentRun( float fadeOutSeconds ) {
            if ( run == null ) {
                return;
            }
            run.HandOverFadeSeconds = fadeOutSeconds;
            run.Cancellation.Cancel( );
            run.Cancellation.Dispose( );
            run = null;
        }

        private async UniTaskVoid RunPlaylistAsync( PlaylistRun thisRun, MusicPlaylistSO playlist, bool followsOtherMusic ) {
            var ct = thisRun.Cancellation.Token;
            var tracks = playlist.Tracks.Where( t => t?.Clip != null && t.Clip.RuntimeKeyIsValid( ) ).ToList( );
            if ( tracks.Count == 0 ) {
                UnityEngine.Debug.LogWarning( $"Music playlist '{playlist.name}' has no songs to play.", playlist );
                return;
            }

            var sequencer = new PlaylistSequencer( tracks.Count, playlist.Shuffle );
            Voice loading = null;
            Voice playing = null;
            int brokenInARow = 0;
            bool first = true;
            try {
                loading = new Voice( tracks[ sequencer.Next( ) ] );
                while ( true ) {
                    await loading.WaitUntilLoadedAsync( ct );
                    playing = loading;
                    loading = null;

                    if ( !playing.IsLoaded ) {
                        UnityEngine.Debug.LogError( $"Music playlist '{playlist.name}': a song failed to load.", playlist );
                        Release( playing );
                        playing = null;
                        if ( ++brokenInARow >= tracks.Count ) {
                            return;
                        }
                        loading = new Voice( tracks[ sequencer.Next( ) ] );
                        continue;
                    }

                    brokenInARow = 0;

                    // Fading in from silence also when the playlist itself does not fade: music that
                    // replaces other music is never cut in.
                    float introFade = playlist.FadeIn ? playlist.FadeInSeconds : 0f;
                    if ( first && followsOtherMusic ) {
                        introFade = Mathf.Max( introFade, config.PlaylistSwitchFadeSeconds );
                    }
                    first = false;

                    // Loading the next song while this one plays.
                    loading = new Voice( tracks[ sequencer.Next( ) ] );

                    float startedAt = Time.unscaledTime;
                    await PlayAsync( playing, playlist, introFade, ct );
                    Release( playing );
                    playing = null;

                    if ( Time.unscaledTime - startedAt < MinPlayedSeconds ) {
                        await UniTask.Delay( TimeSpan.FromSeconds( RetryAfterBrokenSongSeconds ), ignoreTimeScale: true, cancellationToken: ct );
                    }
                    float gap = playlist.RollGapSeconds( );
                    if ( gap > 0f ) {
                        await UniTask.Delay( TimeSpan.FromSeconds( gap ), ignoreTimeScale: true, cancellationToken: ct );
                    }
                }
            }
            catch ( OperationCanceledException ) {
                if ( playing != null ) {
                    FadeOutAndReleaseAsync( playing, thisRun.HandOverFadeSeconds ).Forget( );
                    playing = null;
                }
            }
            finally {
                Release( loading );
                Release( playing );
            }
        }

        private async UniTask PlayAsync( Voice voice, MusicPlaylistSO playlist, float introFade, CancellationToken ct ) {
            var clip = voice.Handle.Result;
            var source = RentSource( );
            voice.Source = source;
            voice.TrackVolume = playlist.Volume * voice.Track.Volume;

            float length = clip.length;
            float fadeIn = MusicEnvelope.ClampFade( introFade, length );
            float fadeOut = MusicEnvelope.ClampFade( playlist.FadeOut ? playlist.FadeOutSeconds : 0f, length );

            voice.Envelope = MusicEnvelope.Evaluate( 0f, length, fadeIn, fadeOut );
            source.clip = clip;
            source.volume = voice.CurrentVolume( volume );
            source.Play( );

            while ( source.isPlaying ) {
                voice.Envelope = MusicEnvelope.Evaluate( source.time, length, fadeIn, fadeOut );
                source.volume = voice.CurrentVolume( volume );
                await UniTask.Yield( PlayerLoopTiming.Update, ct );
            }
        }

        private async UniTaskVoid FadeOutAndReleaseAsync( Voice voice, float seconds ) {
            try {
                // Being destroyed (leaving play mode, quitting): there is nothing left to fade.
                if ( quitting || seconds <= 0f ) {
                    return;
                }
                float startEnvelope = voice.Envelope;
                float elapsed = 0f;
                var destroyed = destroyToken;
                while ( elapsed < seconds && voice.Source != null && voice.Source.isPlaying ) {
                    elapsed += Time.unscaledDeltaTime;
                    voice.Envelope = startEnvelope * ( 1f - Mathf.SmoothStep( 0f, 1f, elapsed / seconds ) );
                    voice.Source.volume = voice.CurrentVolume( volume );
                    await UniTask.Yield( PlayerLoopTiming.Update, destroyed );
                }
            }
            catch ( OperationCanceledException ) {
                // The player is destroyed: nothing more to fade.
            }
            finally {
                Release( voice );
            }
        }

        private AudioSource RentSource( ) {
            while ( freeSources.Count > 0 ) {
                var source = freeSources.Pop( );
                if ( source != null ) {
                    return source;
                }
            }
            var host = new GameObject( "MusicSource" );
            host.transform.SetParent( transform, false );
            var created = host.AddComponent<AudioSource>( );
            created.playOnAwake = false;
            created.loop = false;
            created.spatialBlend = 0f;
            created.priority = 0;
            return created;
        }

        private void Release( Voice voice ) {
            if ( voice == null ) {
                return;
            }
            if ( voice.Source != null ) {
                voice.Source.Stop( );
                voice.Source.clip = null;
                freeSources.Push( voice.Source );
                voice.Source = null;
            }
            if ( voice.Handle.IsValid( ) ) {
                Addressables.Release( voice.Handle );
            }
        }

        private sealed class PlaylistRun {
            public readonly CancellationTokenSource Cancellation = new( );
            public float HandOverFadeSeconds;
        }

        // One song: its Addressables handle (the clip is in memory while the handle is alive) and, once it
        // plays, the AudioSource it plays on.
        private sealed class Voice {
            public readonly MusicTrack Track;
            public readonly AsyncOperationHandle<AudioClip> Handle;
            public AudioSource Source;
            public float TrackVolume = 1f;
            public float Envelope = 1f;

            public bool IsLoaded => Handle.Status == AsyncOperationStatus.Succeeded && Handle.Result != null;

            public Voice( MusicTrack track ) {
                Track = track;
                Handle = Addressables.LoadAssetAsync<AudioClip>( track.Clip.RuntimeKey );
            }

            public float CurrentVolume( float musicVolume ) => Mathf.Clamp01( TrackVolume * Envelope * musicVolume );

            public async UniTask WaitUntilLoadedAsync( CancellationToken ct ) {
                if ( Handle.IsDone ) {
                    return;
                }
                var done = new UniTaskCompletionSource<bool>( );
                Handle.Completed += _ => done.TrySetResult( true );
                await done.Task.AttachExternalCancellation( ct );
            }
        }
    }
}
