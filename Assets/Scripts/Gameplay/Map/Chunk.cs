using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Common;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    public enum ChunkVisualState { NotBuilt, Building, Built }

    [Serializable]
    public class Chunk {

        public Vector2Int chunkId;
        public GameObject chunkGameObject;
        public List<SerializableVector2Int> blocksInChunk = new();

        // Terrain visuals (top surface, cliffs, grass) computed from the map's TerrainGrid. They are built
        // in the background, kept while the chunk is near the player and destroyed when it is far away.
        public ChunkVisualState visualState;
        public int visualVersion; // bumped whenever the terrain under the chunk changes
        [System.NonSerialized] public GameObject topVisual;
        [System.NonSerialized] public GameObject cliffVisual;
        [System.NonSerialized] public GameObject grassVisual;

        // Runtime content created while the chunk is near the player (blocks themselves are only data):
        // one object carrying the ground colliders, and the instances of blocks that are visible themselves.
        [System.NonSerialized] public GameObject collidersObject;
        [System.NonSerialized] public Dictionary<SerializableVector2Int, GameObject> blockVisuals = new();

        public SerializableVector2Int GetRandomBlockIndex() {
            return blocksInChunk[ UnityEngine.Random.Range( 0, blocksInChunk.Count ) ];
        }
    }
}
