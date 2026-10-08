using System;
using System.Collections.Generic;
using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // Adapter between the Core FarmModel of the current map and the game: gives it the clock, takes seeds from and
    // hands produce to the inventory, and keeps the loaded plots' views up to date. Holds no crop state itself.
    public sealed class FarmingService : ITickable {

        private readonly MapManager mapManager;
        private readonly IWorldClock clock;
        private readonly CropCatalog crops;
        private readonly InventoryService inventory;
        private readonly ClockManager clockManager;
        private readonly PlayerToolService tools;
        private readonly ISfxPlayer sfx;
        private readonly MessagePopup messagePopup;
        private readonly DeterministicRandom random = new( ( int ) DateTime.UtcNow.Ticks );
        private readonly List<Plot> plots = new();
        private FarmModel watchedFarm;

        [ Inject ]
        public FarmingService( MapManager mapManager, IWorldClock clock, CropCatalog crops, InventoryService inventory,
            ClockManager clockManager, PlayerToolService tools, ISfxPlayer sfx, MessagePopup messagePopup ) {
            this.mapManager = mapManager;
            this.clock = clock;
            this.crops = crops;
            this.inventory = inventory;
            this.clockManager = clockManager;
            this.tools = tools;
            this.sfx = sfx;
            this.messagePopup = messagePopup;
        }

        public CropCatalog Crops => crops;
        public long Now => clock.NowMinutes;

        // The farm of the map the player is on; null until a map is loaded.
        public FarmModel Farm =>
            mapManager.MapsDictionary.TryGetValue( mapManager.CurrentMapId, out var map ) ? map.Farm : null;

        public void Register( Plot plot ) {
            if( !plots.Contains( plot ) ) {
                plots.Add( plot );
            }
        }

        public void Unregister( Plot plot ) {
            plots.Remove( plot );
        }

        // Growth is a function of the clock, so views only need a nudge at the moment their stage or fruit changes.
        public void Tick() {
            WatchCurrentFarm();
            if( plots.Count == 0 ) {
                return;
            }
            long now = Now;
            for( int i = plots.Count - 1; i >= 0; i-- ) {
                if( plots[ i ].NextRefreshAt <= now ) {
                    plots[ i ].Refresh();
                }
            }
        }

        // The farm changes with the map the player is on; tell the player when an animal raids it.
        private void WatchCurrentFarm() {
            var farm = Farm;
            if( farm == watchedFarm ) {
                return;
            }
            if( watchedFarm != null ) {
                watchedFarm.CropEaten -= OnCropEaten;
            }
            watchedFarm = farm;
            if( farm != null ) {
                farm.CropEaten += OnCropEaten;
            }
        }

        private void OnCropEaten( PlotKey key, string cropId, int count, string eater ) {
            if( string.IsNullOrEmpty( eater ) ) {
                return;
            }
            string what = crops.TryGetAsset( new ItemId( cropId ), out var crop ) && crop.produce != null ? crop.produce.itemName : "crops";
            string text = $"A {eater.ToLowerInvariant()} ate your {what}!";
            messagePopup.AddTextMessageToQueue( ref text );
        }

        // Game minutes as the player feels them: the clock adds DEFAULT_CLOCK_SPEED * DEFAULT_TIMESCALE of them per real second.
        public static string FormatRealTime( long gameMinutes ) {
            float seconds = gameMinutes / ( GameConfig.DEFAULT_CLOCK_SPEED * GameConfig.DEFAULT_TIMESCALE );
            if( seconds < 60f ) {
                return $"{Mathf.Max( 1, Mathf.CeilToInt( seconds ) )} s";
            }
            return $"{Mathf.CeilToInt( seconds / 60f )} min";
        }

        // Climate of the cell under a plot (the cell the plot was anchored at).
        public Climate ClimateAt( PlotKey key ) {
            return mapManager.MapsDictionary.TryGetValue( mapManager.CurrentMapId, out var map )
                ? map.ClimateAt( new SerializableVector2Int( key.X, key.Z ) )
                : new Climate( 0f, 0f );
        }

        // How fast `crop` would grow on this plot, in percent of normal, and what holds it back.
        public (int speedPercent, ClimateIssue issue) ClimateFor( PlotKey key, CropSO crop ) {
            var range = new ClimateRange( crop.temperatureFrom, crop.temperatureTo, crop.humidityFrom, crop.humidityTo );
            var climate = ClimateAt( key );
            return ( CropClimate.SpeedPercent( range, climate ), CropClimate.Issue( range, climate ) );
        }

        // "good here" or "slow here: too dry" for the seed picker.
        public string PlantingNote( PlotKey key, CropSO crop ) {
            var ( speed, issue ) = ClimateFor( key, crop );
            return issue == ClimateIssue.None ? "good here" : $"slow here ({speed}%): {IssueText( issue )}";
        }

        public static string IssueText( ClimateIssue issue ) => issue switch {
            ClimateIssue.TooCold => "too cold",
            ClimateIssue.TooHot => "too hot",
            ClimateIssue.TooDry => "too dry",
            ClimateIssue.TooWet => "too wet",
            _ => "",
        };

        public float GatheringSeconds( CropSO crop ) => tools.GatherSeconds( crop != null ? crop.harvest : null );

        public void BeginGathering( CropSO crop ) {
            tools.Begin( crop != null ? crop.harvest : null );
            clockManager.SetTimeScale( 2 );
        }

        // `harvested`: the work was done, so the tool in use wears down; otherwise it was cancelled.
        public void EndGathering( bool harvested = false ) {
            clockManager.ResetTimeScale();
            if( harvested ) {
                tools.Complete();
            }
            else {
                tools.Cancel();
            }
        }

        public int OwnedSeeds( CropSO crop ) => crop.seed == null ? 0 : inventory.Count( crop.seed.Id );

        // Plants one seed from the inventory; nothing is taken unless the crop really goes into the ground.
        public PlantResult Plant( PlotKey key, CropSO crop ) {
            var farm = Farm;
            if( farm == null || crop.seed == null || OwnedSeeds( crop ) < 1 ) {
                return PlantResult.UnknownCrop;
            }
            var result = farm.TryPlant( key, crop.Id, Now, ClimateAt( key ) );
            if( result == PlantResult.Planted ) {
                inventory.RemoveItem( crop.seed.Id, 1 );
            }
            return result;
        }

        public bool CanHarvest( PlotKey key, CropSO crop ) {
            var farm = Farm;
            if( farm == null || crop == null || crop.produce == null ) {
                return false;
            }
            int ready = farm.Describe( key, Now ).ReadyYield;
            return ready > 0 && inventory.CanFit( crop.produce, ready );
        }

        // Takes the ripe produce into the inventory (plus a seed, sometimes). Returns false when nothing was ripe.
        public bool Harvest( PlotKey key, CropSO crop ) {
            var farm = Farm;
            if( farm == null || crop == null || !CanHarvest( key, crop ) ) {
                return false;
            }
            var result = farm.TryHarvest( key, Now, random );
            if( !result.Success ) {
                return false;
            }
            inventory.AddItem( crop.produce, result.Quantity );
            if( result.SeedQuantity > 0 && crop.seed != null ) {
                inventory.AddItem( crop.seed, result.SeedQuantity );
            }
            if( crop.harvest != null ) {
                sfx.Play( crop.harvest.OnPickup );
            }
            return true;
        }
    }
}
