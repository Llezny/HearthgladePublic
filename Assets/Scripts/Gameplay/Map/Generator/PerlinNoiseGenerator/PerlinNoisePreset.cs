using UnityEngine;

namespace Hearthglade {
    [CreateAssetMenu(fileName = "NewNoisePreset", menuName = "ScriptableObjects/Map/PerlinNoise/NoisePreset")]
    public class PerlinNoisePreset : ScriptableObject {

        [Tooltip("Map cells per noise lattice unit of the broadest octave: the size of the biggest features. Larger is smoother.")]
        [Min(1f)]
        public float Scale = 30f;

        [Min(1)]
        public int Octaves = 1;

        [Tooltip("Amplitude multiplier per octave. Below 1 the finer octaves add detail on top of the broad shape.")]
        [Range(0f, 1f)]
        public float Persistance = 0.5f;

        [Tooltip("Frequency multiplier per octave. Above 1 every octave is finer than the previous one.")]
        [Min(1f)]
        public float Lacunarity = 2f;
    }
}
