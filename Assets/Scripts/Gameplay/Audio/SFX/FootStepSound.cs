using UnityEngine;

namespace Hearthglade.Gameplay.SFX
{
    public class FootStepSound : MonoBehaviour
    {
        [SerializeField] AudioClip[] clips = null;
        AudioSource audioSource = null;
        void Awake(){
            audioSource = GetComponent<AudioSource>();
        }

        void Step(){
            var clip = GetRandomClip();
            audioSource.clip = clip;
            audioSource.Play();
        }

        AudioClip GetRandomClip(){
            return clips[UnityEngine.Random.Range(0,clips.Length)];
        }
    }
}
