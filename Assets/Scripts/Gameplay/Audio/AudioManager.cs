using CaptureTheFlag.Common;
using UnityEngine;

namespace Hearthglade.Gameplay.Audio
{
	public class AudioManager : PersistentSingleton<AudioManager> {
		
		[ SerializeField ] private AudioSO mainTheme;

		[ Header("Audio sources") ]
		[ SerializeField ] private AudioSource musicAudioSource;
		[ SerializeField ] private AudioSource effectsAudioSource;

		protected override void Awake( ) {
			UnityEngine.Debug.Log( "AudioManager Awake" );
			base.Awake( );
			Play( mainTheme );
		}

		public void Play( AudioSO audioSO ) {
			if ( audioSO is null ) {
				return;
			}
			switch ( audioSO.AudioType ) {
				case AudioType.Music:
					PlayMusic( audioSO );
					break;
				case AudioType.Effect:
					PlaySfx( audioSO );
					break;
				default:
					UnityEngine.Debug.Log( "Unknown audio type" );
					break;
			}
		}

		private void PlayMusic( AudioSO audioSO ) {
			musicAudioSource.Stop();
			musicAudioSource.clip = audioSO.Clip;
			musicAudioSource.Play();
		}

		private void PlaySfx( AudioSO audioSO ) {
			effectsAudioSource.PlayOneShot( audioSO.Clip );
		}

	}
}
