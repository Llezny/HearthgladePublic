using System.Collections.Generic;
using Hearthglade.Core.Farming;
using Hearthglade.Gameplay.Common;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    public struct MapModel {
        public Vector3 entryPosAtPreviousMap;
        public bool hasEntryAtPreviousMap;
        public string mapTypeName;
        public int mapId;

        // Seed the map was generated with (0 in saves that predate it); used for blocks generated later.
        public int seed;

        // WorldGenVersion the map was generated with (0 in saves that predate it).
        public int generatorVersion;

        // The expedition this island was sailed to; depth 0 = none (also what a save that predates expeditions holds).
        public int expeditionDirection;
        public int expeditionDepth;
        public Dictionary<SerializableVector2Int, BlockModel> blockModels;

        // Crops of the map's garden plots (null in saves that predate farming).
        public FarmSnapshot farm;

        public MapModel( Map map ) {
            this.mapTypeName = map.MapType.mapName;
            this.entryPosAtPreviousMap = map.EntryPosAtPreviousMap;
            this.hasEntryAtPreviousMap = map.HasEntryAtPreviousMap;
            this.blockModels = new();
            this.mapId = map.MapId;
            this.seed = map.Seed;
            this.generatorVersion = map.GeneratorVersion;
            this.expeditionDirection = map.ExpeditionTrip.HasValue ? ( int ) map.ExpeditionTrip.Value.Direction : 0;
            this.expeditionDepth = map.ExpeditionTrip?.Depth ?? 0;
            this.farm = map.Farm?.ToSnapshot();

            foreach( var block in map.Models ) {
                this.blockModels.Add( block.Key, block.Value );
            }
        }
    }
}