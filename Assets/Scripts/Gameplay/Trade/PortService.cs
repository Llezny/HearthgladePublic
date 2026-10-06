using System.Collections.Generic;
using System.Linq;
using Hearthglade.Core.Trade;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Trade
{
    /// <summary>One port of the game: its data, its fixed character and what has happened to it so far.</summary>
    public sealed class PortEntry
    {
        public PortSO Asset { get; }
        public PortProfile Profile { get; }
        public PortState State { get; set; }

        public PortEntry(PortSO asset, PortProfile profile, PortState state)
        {
            Asset = asset;
            Profile = profile;
            State = state;
        }
    }

    [System.Serializable]
    public class PortServiceSaveData
    {
        public int CompletedExpeditions;
        public Dictionary<string, PortStateData> Ports = new Dictionary<string, PortStateData>();
    }

    /// <summary>
    /// Counts the completed expeditions, discovers the ports that unlock at them and refills their stock.
    /// A registered entry point so it exists from the start of the game: a saveable that nothing resolved
    /// would be missing from the save and its progress lost.
    /// </summary>
    public sealed class PortService : IStartable, ISaveable, System.IDisposable
    {
        private readonly SaveManager saveManager;
        private readonly MessagePopup messagePopup;
        private readonly MapManager mapManager;
        private readonly ItemCatalog catalog;
        private readonly List<PortEntry> ports = new List<PortEntry>();

        public int CompletedExpeditions { get; private set; }

        public IReadOnlyList<PortEntry> Ports => ports;

        public IEnumerable<PortEntry> DiscoveredPorts => ports.Where(port => port.State.Discovered);

        [Inject]
        public PortService(SaveManager saveManager, MessagePopup messagePopup, MapManager mapManager, ItemCatalog catalog)
        {
            this.saveManager = saveManager;
            this.messagePopup = messagePopup;
            this.catalog = catalog;
            this.mapManager = mapManager;
            mapManager.MapGenerated += RememberPortMap;
            foreach (var asset in ResourceLoader.LoadAll<PortSO>(ResourceLoader.PORTS_PATH).OrderBy(port => port.unlockAfterExpeditions).ThenBy(port => port.name))
            {
                ports.Add(new PortEntry(asset, asset.ToProfile(), new PortState()));
            }
            saveManager.RegisterISavable(this);
            if (saveManager.TryGetState<PortService>(out var saved))
            {
                RestoreState(saved);
            }
        }

        public void Dispose()
        {
            mapManager.MapGenerated -= RememberPortMap;
        }

        // The island of a port is generated once; its map id is saved so that the ship can sail back to it.
        private void RememberPortMap(Map.Map map)
        {
            foreach (var port in ports)
            {
                if (port.Asset.mapName == map.MapType.mapName)
                {
                    port.State.MapId = map.MapId;
                }
            }
        }

        public void Start()
        {
            // A loaded game may already be past a threshold the data of which was added after the save.
            DiscoverAndRestock(announce: false);
        }

        public PortEntry Find(string portId)
        {
            return ports.FirstOrDefault(port => port.Asset.Id == portId);
        }

        public PortEntry FindByMap(int mapId)
        {
            return mapId == MapManager.NotGeneratedMapId ? null : ports.FirstOrDefault(port => port.State.MapId == mapId);
        }

        /// <summary>One more expedition is over: new ports may be discovered, the stock of the known ones refilled.</summary>
        public void CompleteExpedition()
        {
            SetCompletedExpeditions(CompletedExpeditions + 1);
        }

        /// <summary>For the console: jumps to a count of completed expeditions.</summary>
        public void SetCompletedExpeditions(int count)
        {
            CompletedExpeditions = System.Math.Max(0, count);
            DiscoverAndRestock(announce: true);
        }

        private void DiscoverAndRestock(bool announce)
        {
            foreach (var port in ports)
            {
                if (port.State.TryDiscover(port.Profile, CompletedExpeditions) && announce)
                {
                    var text = $"Discovered the port {port.Asset.DisplayName}";
                    messagePopup.AddTextMessageToQueue(ref text);
                }
                port.State.Restock(port.Profile, CompletedExpeditions);
                RefreshContracts(port);
            }
        }

        /// <summary>Drops the orders of a port that lapsed and fills its free places (more of them at a higher relation level).</summary>
        public void RefreshContracts(PortEntry port)
        {
            if (!port.State.Discovered)
            {
                return;
            }
            int level = port.Profile.LevelFor(port.State.RelationPoints);
            port.State.Contracts.Refresh(port.Profile, catalog, level, saveManager.GameSeed, CompletedExpeditions);
        }

        public object CaptureState()
        {
            var data = new PortServiceSaveData { CompletedExpeditions = CompletedExpeditions };
            foreach (var port in ports)
            {
                data.Ports[port.Asset.Id] = port.State.ToData();
            }
            return data;
        }

        public void RestoreState(object state)
        {
            var data = state is JToken token
                ? token.ToObject<PortServiceSaveData>()
                : JsonConvert.DeserializeObject<PortServiceSaveData>(state.ToString());
            if (data == null)
            {
                return;
            }
            CompletedExpeditions = System.Math.Max(0, data.CompletedExpeditions);
            foreach (var port in ports)
            {
                if (data.Ports != null && data.Ports.TryGetValue(port.Asset.Id, out var portData))
                {
                    port.State = PortState.FromData(portData);
                }
            }
        }
    }
}
