using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>
    /// A set of songs that play one after another while the player is somewhere (a map, the main menu): which
    /// map plays which playlist is set on the MapSO. The songs are Addressables - only the song that plays
    /// (and the one that follows it) is in memory.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlaylist", menuName = "ScriptableObjects/Audio/MusicPlaylist")]
    public class MusicPlaylistSO : ScriptableObject {

        public List<MusicTrack> Tracks = new( );

        [Range(0f, 1f)]
        public float Volume = 1f;

        [Tooltip("Plays every song once in a random order, then reshuffles. Otherwise the songs play in the order of the list.")]
        public bool Shuffle = true;

        [Header("Fades")]
        [Tooltip("Every song starts quietly and rises to full volume.")]
        public bool FadeIn = true;
        [Min(0f)] public float FadeInSeconds = 4f;

        [Tooltip("Every song gets quieter towards its end, instead of stopping at full volume.")]
        public bool FadeOut = true;
        [Min(0f)] public float FadeOutSeconds = 4f;

        [Header("Gaps")]
        [Tooltip("Silence between one song and the next, a random time between Min and Max.")]
        public bool GapBetweenTracks = true;
        [Min(0f)] public float MinGapSeconds = 3f;
        [Min(0f)] public float MaxGapSeconds = 8f;

        public float RollGapSeconds( ) {
            if ( !GapBetweenTracks ) {
                return 0f;
            }
            float min = Mathf.Max( 0f, MinGapSeconds );
            return UnityEngine.Random.Range( min, Mathf.Max( min, MaxGapSeconds ) );
        }
    }

    [Serializable]
    public class MusicTrack {
        public AssetReferenceT<AudioClip> Clip;

        [Tooltip("Trim for a song that is louder or quieter than the others.")]
        [Range(0f, 1f)]
        public float Volume = 1f;
    }
}
