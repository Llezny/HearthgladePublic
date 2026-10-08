using UnityEngine;

namespace Hearthglade
{
    /// <summary>
    /// A sound effect: one or more clips, played by <see cref="Gameplay.Audio.ISfxPlayer"/>. With more than one
    /// clip a random one is played each time, and the volume and pitch can vary a little, so a sound heard
    /// again and again (a harvest, a footstep) does not wear out. Music is not an AudioSO - see MusicPlaylistSO.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSound", menuName = "ScriptableObjects/SoundSO")]
    public class AudioSO : ScriptableObject {

        public AudioClip Clip;

        [Tooltip("More clips of the same sound. Together with Clip, one of them is picked at random each time.")]
        public AudioClip[] MoreClips;

        [Range(0f, 1f)]
        public float Volume = .75f;

        [Tooltip("The volume is lowered by up to this fraction at random.")]
        [Range(0f, 1f)]
        public float VolumeVariance = 0f;

        [Range(.1f, 3f)]
        public float Pitch = 1f;

        [Tooltip("The pitch is raised or lowered by up to this fraction at random.")]
        [Range(0f, .5f)]
        public float PitchVariance = 0f;

        [Tooltip("The sound is not played again within this many seconds, so a burst of the same sound is not played as one very loud sound.")]
        [Min(0f)]
        public float MinInterval = .05f;

        public bool HasClip => Clip != null || ( MoreClips != null && MoreClips.Length > 0 );

        public AudioClip PickClip( ) {
            int extra = MoreClips?.Length ?? 0;
            if ( extra == 0 ) {
                return Clip;
            }
            int index = Random.Range( 0, extra + 1 );
            return index == 0 ? Clip : MoreClips[ index - 1 ];
        }

        public float RollVolume( ) {
            return Volume * ( 1f - Random.value * VolumeVariance );
        }

        public float RollPitch( ) {
            return Pitch * ( 1f + Random.Range( -PitchVariance, PitchVariance ) );
        }
    }
}
