using System.Collections.Generic;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hearthglade.Gameplay.Map
{
    [System.Serializable]
    public class BlockModel {

        // Associated prefab
        public string prefabName;

        // Index on map, [0,0] is South-West corner, [1, 1] is North-East
        public int gridX, gridY;

        // The noise values a block was generated from (height, temperature, humidity) are not kept, only the climate below:
        // nothing else reads them after generation and they were a fifth of every save. Saves that still have them load fine.
        public bool isWall;

        // Cosmetic ground patch (0 = none, otherwise the terrain biome id of the patch + 1). Only written to a save when set.
        [ Newtonsoft.Json.JsonProperty( DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore ) ]
        public byte patch;

        // Climate the block was generated in (temperature, humidity), see Climate.Encode; 0 = unknown, e.g. blocks from older saves.
        [ Newtonsoft.Json.JsonProperty( DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore ) ]
        public byte climateT, climateH;
        public Vector3 WorldPosition;
        public Dictionary<int, SceneObjectModel> blockObjects = new Dictionary<int, SceneObjectModel>();

        /// <summary>
        /// Takes an object out of the block. Found by reference, not by its key: a loaded save carries the keys of
        /// the launch that wrote it (older saves: random ones), so a key computed now may not match.
        /// </summary>
        public bool RemoveSceneObject( SceneObjectModel sceneObject ) {
            if( blockObjects == null ) {
                return false;
            }
            foreach( var pair in blockObjects ) {
                if( ReferenceEquals( pair.Value, sceneObject ) ) {
                    return blockObjects.Remove( pair.Key );
                }
            }
            return false;
        }

        /// <summary>Gives every object the key it has now: run after loading, so keys of old saves are valid again.</summary>
        public void RebuildObjectKeys( ) {
            var objects = new Dictionary<int, SceneObjectModel>();
            if( blockObjects != null ) {
                foreach( var sceneObject in blockObjects.Values ) {
                    if( sceneObject != null ) {
                        objects.TryAdd( sceneObject.GetHashCode(), sceneObject );
                    }
                }
            }
            blockObjects = objects;
        }

        public BlockModel( int gridX, int gridY, bool isWall, string prefabName, byte patch = 0, byte climateT = 0, byte climateH = 0 ) {
            this.prefabName = prefabName;
            this.WorldPosition = MapHelper.GridToWorldPosition( new Vector2( gridX, gridY ) );
            this.gridX = gridX;
            this.gridY = gridY;
            this.isWall = isWall;
            this.patch = patch;
            this.climateT = climateT;
            this.climateH = climateH;
        }
    }
}