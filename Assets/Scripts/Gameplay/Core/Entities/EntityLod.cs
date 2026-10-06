namespace Hearthglade.Core.Entities {

    public enum EntityLodAction { None, Activate, Deactivate, Despawn }

    /// <summary>
    /// How an entity is treated by its distance to the player: close ones run, the ones in between are frozen
    /// (inactive), the far ones are returned to the pool.
    /// </summary>
    public static class EntityLod {

        public static EntityLodAction Decide( float distance, bool isActive, float activateDistance, float despawnDistance ) {
            if( distance > despawnDistance ) {
                return EntityLodAction.Despawn;
            }
            if( distance < activateDistance ) {
                return isActive ? EntityLodAction.None : EntityLodAction.Activate;
            }
            return isActive ? EntityLodAction.Deactivate : EntityLodAction.None;
        }
    }
}
