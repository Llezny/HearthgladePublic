using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>
    /// Plays sound effects on a fixed set of AudioSources. Every sound gets a source of its own, so its pitch
    /// and volume are its own (they used to be shared by all sounds playing through the one source). When all
    /// the sources are busy the one that started first is cut, so a new sound is never lost.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour, ISfxPlayer {

        private AudioSource[] sources;
        private float[] startedAt;
        private readonly Dictionary<AudioSO, float> lastPlayedAt = new( );
        private float volume = 1f;

        public float Volume {
            get => volume;
            set => volume = Mathf.Clamp01( value );
        }

        [ Inject ]
        public void Construct( AudioConfigSO config ) {
            volume = config.SfxVolume;
            sources = new AudioSource[ config.SfxVoices ];
            startedAt = new float[ config.SfxVoices ];
            for ( int i = 0; i < sources.Length; i++ ) {
                var host = new GameObject( $"SfxSource{i}" );
                host.transform.SetParent( transform, false );
                var source = host.AddComponent<AudioSource>( );
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources[ i ] = source;
            }
        }

        public void Play( AudioSO sfx ) {
            if ( sfx == null || !sfx.HasClip ) {
                return;
            }
            float now = Time.unscaledTime;
            if ( lastPlayedAt.TryGetValue( sfx, out float last ) && now - last < sfx.MinInterval ) {
                return;
            }
            lastPlayedAt[ sfx ] = now;

            int index = PickSource( );
            var source = sources[ index ];
            source.clip = sfx.PickClip( );
            source.volume = Mathf.Clamp01( sfx.RollVolume( ) * volume );
            source.pitch = sfx.RollPitch( );
            source.Play( );
            startedAt[ index ] = now;
        }

        // A free source, or else the one that has been playing the longest.
        private int PickSource( ) {
            int oldest = 0;
            for ( int i = 0; i < sources.Length; i++ ) {
                if ( !sources[ i ].isPlaying ) {
                    return i;
                }
                if ( startedAt[ i ] < startedAt[ oldest ] ) {
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}
