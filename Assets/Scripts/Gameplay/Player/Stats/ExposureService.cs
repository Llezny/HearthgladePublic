using System;
using Hearthglade.Core.Stats;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Player.Stats
{
    /// <summary>
    /// How cold or hot the player is (docs/EQUIPMENT_PLAN.md, phase 6). Once a second it reads the climate of the player's cell in the °C range
    /// of the map (<see cref="MapSO.minTemperature"/>), the time of day, the worn clothing, a burning fire nearby and whether the player is
    /// indoors, and lets <see cref="Exposure"/> judge them. The temperature always shows on the HUD; a harmful state drains health
    /// (<see cref="PlayerStatsModel"/>) and makes the player murmur once when it begins.
    /// </summary>
    public sealed class ExposureService : IStartable, ITickable
    {
        private const float CheckIntervalSeconds = 1f;
        // How close a burning fireplace has to be to warm the player: about three tiles.
        private const float FireRadius = 3f * MapGenerator.TILE_X_OFFSET;

        private readonly PlayerController player;
        private readonly MapManager maps;
        private readonly ClockManager clock;
        private readonly EquipmentService equipment;
        private readonly CookingService cooking;
        private readonly MurmurService murmur;
        private readonly CanvasService canvases;

        private ExposureIndicator indicator;
        private float timer;

        public ExposureState State { get; private set; } = ExposureState.Comfortable;
        // The air around the player in °C (the place, the time of day, a cave); null when there is no map to read it from.
        public float? Temperature { get; private set; }
        public event Action<ExposureState> Changed;

        public ExposureService( PlayerController player, MapManager maps, ClockManager clock, EquipmentService equipment, CookingService cooking,
                                MurmurService murmur, CanvasService canvases ) {
            this.player = player;
            this.maps = maps;
            this.clock = clock;
            this.equipment = equipment;
            this.cooking = cooking;
            this.murmur = murmur;
            this.canvases = canvases;
        }

        public void Start() {
            indicator = ExposureIndicator.Create( canvases.HudCanvas.transform );
        }

        public void Tick() {
            timer -= Time.deltaTime;
            if( timer > 0f ) {
                return;
            }
            timer = CheckIntervalSeconds;
            Assess();
        }

        private void Assess() {
            if( player == null || !maps.MapsDictionary.TryGetValue( maps.CurrentMapId, out var map ) || map == null ) {
                Temperature = null;
                Set( ExposureState.Comfortable );
                return;
            }
            var position = player.transform.position;
            var climate = map.ClimateAt( MapHelper.WorldPositionToBlockIndex( position ) );
            var mapType = map.MapType;
            float celsius = mapType != null
                ? Exposure.Celsius( climate.Temperature, mapType.minTemperature, mapType.maxTemperature )
                : Exposure.Celsius( climate.Temperature, Exposure.DefaultMinTemperature, Exposure.DefaultMaxTemperature );
            bool underground = mapType != null && mapType.isUnderground;
            float temperature = Exposure.AmbientTemperature( celsius, !clock.IsDay, underground );
            Temperature = temperature;
            Set( Exposure.Assess( temperature, equipment.Model.Protection, cooking.IsFireBurningNear( position, FireRadius ), player.IsIndoors ) );
        }

        private void Set( ExposureState state ) {
            var previous = State;
            State = state;
            if( indicator != null ) {
                indicator.Show( Temperature, state );
            }
            if( state.Kind == previous.Kind && state.IsHarmful == previous.IsHarmful ) {
                return;
            }
            if( state.IsHarmful && !previous.IsHarmful ) {
                murmur.Show( state.Kind == Discomfort.Cold ? "I'm freezing. I need warmer clothes or a fire." : "This heat is too much. I need some shade." );
            }
            Changed?.Invoke( state );
        }
    }
}
