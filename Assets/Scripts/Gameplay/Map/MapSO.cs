using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    [CreateAssetMenu(fileName = "Map", menuName = "ScriptableObjects/Map/MapInfo")]
    public class MapSO : ScriptableObject {

        [ Header( "Base info:" ) ]

        public string mapName;
        public bool isUnderground;
        public bool IsHome;
        [Tooltip("The map stays (and is saved) when the player leaves it, like Home: a port, not a one-off island.")]
        public bool IsPersistent;
        public bool KeepsAfterLeaving => IsHome || IsPersistent;
        public bool IsEndless => SizeInChunks < 0;
        
        [Tooltip( "Map NxN size in chunks, less than zero means endless map " )]
        public int SizeInChunks;

        [ Header( "Perlin noise config:" ) ]
        public PerlinNoiseMapConfig perlinNoiseConfig;

        [ Header( "Ambient colors:" ) ]
        public GradientColor dayLight;
        public GradientColor nightLight;

        [ Header( "Objects on map:" ) ]
        public GameObject earthBlock;
        public GameObject backgroundPrefab;
        public GameObject entryObject;

        [ Header( "Entities (what spawns is listed by each biome):" ) ]
        [ Tooltip( "Entities closer to the player than this (world units) are running." ) ]
        public float entityActivateDistance = 4f;

        [ Tooltip( "Entities further from the player than this (world units) go back to the pool. Between the two they stand frozen." ) ]
        public float entityDespawnDistance = 7f;

    }
}
