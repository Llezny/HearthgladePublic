using System.Collections.Generic;
using Hearthglade.Core.Entities;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Lean.Pool;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities
{
    /// <summary>
    /// Spawns the entities of the biome the player is walking through (each biome lists its own), keeps the ones
    /// near the player running and returns the far ones to the pool. Called once per game tick.
    /// </summary>
    public class EntityManager {

        private readonly Map.Map targetMap;
        private readonly PlayerController playerController;
        private readonly Dictionary<EntitySpawnData, EntityGroup> groups = new();
        private readonly List<EntityGroup> spawnCandidates = new();
        private readonly List<EntitySpawnData> candidateData = new();

        public EntityManager( Map.Map targetMap, PlayerController playerController ) {
            this.targetMap = targetMap;
            this.playerController = playerController;
        }

        public void Tick( bool isDay ) {
            RetireOutOfSeason( isDay );
            HandleSpawnedEntities();
            SpawnEntityIfCan( isDay );
        }

        // Butterflies do not outlast the day, fireflies the night: their time is over, so they leave (each in its own way).
        private void RetireOutOfSeason( bool isDay ) {
            foreach( var group in groups.Values ) {
                if( SpawnTimeRules.Allows( group.Data.time, isDay ) ) {
                    continue;
                }
                for( int i = group.Alive.Count - 1; i >= 0; i-- ) {
                    var entity = group.Alive[ i ];
                    Release( group, entity );
                    entity.Retire();
                }
            }
        }

        private void HandleSpawnedEntities() {
            var playerPosition = playerController.transform.position;
            float activateDistance = targetMap.MapType.entityActivateDistance;
            float despawnDistance = targetMap.MapType.entityDespawnDistance;

            foreach( var group in groups.Values ) {
                var alive = group.Alive;
                for( int i = alive.Count - 1; i >= 0; i-- ) {
                    var entity = alive[ i ];
                    float distance = Vector3.Distance( playerPosition, entity.transform.position );
                    switch( EntityLod.Decide( distance, entity.gameObject.activeSelf, activateDistance, despawnDistance ) ) {
                        case EntityLodAction.Activate:
                            entity.gameObject.SetActive( true );
                            break;
                        case EntityLodAction.Deactivate:
                            entity.gameObject.SetActive( false );
                            break;
                        case EntityLodAction.Despawn:
                            Release( group, entity );
                            LeanPool.Despawn( entity.gameObject );
                            break;
                    }
                }
            }
        }

        // One entity per tick at most: somewhere off screen, of a kind that lives in the biome of that spot.
        private void SpawnEntityIfCan( bool isDay ) {
            foreach( var group in groups.Values ) {
                group.Quota.Advance( MapManager.MapTickTime );
            }
            if( !targetMap.TryFindEntitySpawn( out var position, out var biome ) ) {
                return;
            }

            spawnCandidates.Clear();
            candidateData.Clear();
            foreach( var data in biome.Entities ) {
                if( data.prefab == null || !SpawnTimeRules.Allows( data.time, isDay ) ) {
                    continue;
                }
                var group = GroupOf( data );
                if( group.Quota.CanSpawn( group.Alive.Count ) ) {
                    spawnCandidates.Add( group );
                    candidateData.Add( data );
                }
            }
            if( spawnCandidates.Count == 0 ) {
                return;
            }

            int chosen = Random.Range( 0, spawnCandidates.Count );
            Spawn( spawnCandidates[ chosen ], candidateData[ chosen ], position );
        }

        private void Spawn( EntityGroup group, EntitySpawnData data, Vector3 position ) {
            var entity = LeanPool.Spawn(
                data.prefab,
                position + data.spawnOffset,
                Quaternion.Euler( 0f, Random.Range( 0f, 360f ), 0f ),
                targetMap.transform );

            entity.Construct( targetMap, playerController.transform );
            // Far from the player until they come close enough (see HandleSpawnedEntities).
            entity.gameObject.SetActive( false );
            entity.Died += OnEntityDied;

            group.Alive.Add( entity );
            group.Quota.Spawned();
            UnityEngine.Debug.Log( $"[ Spawned ] {data.prefab.name} at {position}" );
        }

        private EntityGroup GroupOf( EntitySpawnData data ) {
            if( !groups.TryGetValue( data, out var group ) ) {
                group = new EntityGroup( data );
                groups.Add( data, group );
            }
            return group;
        }

        private void OnEntityDied( Entity entity ) {
            foreach( var group in groups.Values ) {
                if( group.Alive.Contains( entity ) ) {
                    Release( group, entity );
                    return;
                }
            }
        }

        private void Release( EntityGroup group, Entity entity ) {
            entity.Died -= OnEntityDied;
            group.Alive.Remove( entity );
        }
    }
}
