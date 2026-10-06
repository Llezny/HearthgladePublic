using System.Collections.Generic;
using Hearthglade.Core.Entities;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>The living entities spawned from one <see cref="EntitySpawnData"/> and its spawn limits.</summary>
    public class EntityGroup {
        public readonly EntitySpawnData Data;
        public readonly SpawnQuota Quota;
        public readonly List<Entity> Alive = new();

        public EntityGroup( EntitySpawnData data ) {
            Data = data;
            Quota = new SpawnQuota( data.maxAlive, data.cooldownSeconds );
        }
    }
}
