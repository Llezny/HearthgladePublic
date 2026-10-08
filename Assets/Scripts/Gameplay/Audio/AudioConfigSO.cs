using UnityEngine;

namespace Hearthglade.Gameplay.Audio {

    /// <summary>The settings of the audio services that live for the whole life of the app (see AppScope).</summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "ScriptableObjects/Audio/AudioConfig")]
    public class AudioConfigSO : ScriptableObject {

        [Tooltip("Plays from the start of the app, and again whenever the player is back in the main menu.")]
        public MusicPlaylistSO MenuMusic;

        [Tooltip("When one playlist replaces another (a new map, the menu), the old one fades out over this long and the new one fades in at least that long.")]
        [Min(0f)]
        public float PlaylistSwitchFadeSeconds = 2f;

        [Tooltip("How many sound effects can play at once. When they are all busy the oldest one is cut.")]
        [Min(1)]
        public int SfxVoices = 12;

        [Range(0f, 1f)]
        public float MusicVolume = 1f;

        [Range(0f, 1f)]
        public float SfxVolume = 1f;
    }
}
