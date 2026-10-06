using Hearthglade.Core.Entities;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities
{
    /// <summary>An entity a biome spawns, and how many of it may exist on the map at once.</summary>
    [ System.Serializable ]
    public class EntitySpawnData {

        public Entity prefab;

        [ Min( 1 ) ]
        public int maxAlive = 3;

        [ Tooltip( "Seconds between two spawns of this entity." ) ]
        public float cooldownSeconds = 5f;

        [ Tooltip( "Added to the middle of the cell the entity appears on." ) ]
        public Vector3 spawnOffset = Vector3.zero;

        [ Tooltip( "Day only, night only or always. Entities are retired when their time is over." ) ]
        public SpawnTime time = SpawnTime.Always;
    }
}
