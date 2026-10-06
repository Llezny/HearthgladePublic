using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hearthglade
{
    [CreateAssetMenu(fileName = "NewSound", menuName = "ScriptableObjects/SoundSO")]
    public class AudioSO : ScriptableObject {

        public AudioClip Clip;

        public AudioType AudioType;
        
        [Range(0f, 1f)]
        public float Volume = .75f;
        
        [Range(.1f, 3f)]
        public float Pitch = 1f;
    }

    public enum AudioType {
        Effect,
        Music
    }
}
