using System.Collections.Generic;
using Hearthglade.Core.Stats;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.UI.HUD;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Hearthglade.Gameplay.Player.Stats
{
    public class PlayerStatsModel {

        private readonly Dictionary<StatsMap, Stat> statsDictionary = new Dictionary<StatsMap, Stat>();
        private readonly Rigidbody playerRigidbody;
        private readonly ClockManager clockManager;
        // Other reasons than hunger and thirst for health to drain right now (the cold or the heat); may be null.
        private readonly System.Func<bool> otherHarm;


        public List<Pair<StatsMap, PlayerNeed>> PlayerNeeds;


        public PlayerStatsModel( Rigidbody playerRigidbody, GameEvents gameEvents, ClockManager clockManager, System.Func<bool> otherHarm = null ) {
            this.otherHarm = otherHarm;
            this.playerRigidbody = playerRigidbody;
            this.clockManager = clockManager;

            // Health is a pure Hearthglade.Core type with no knowledge of GameEvents - bridge its
            // own events to the ones the rest of the game actually listens to (e.g. DamageIndicator).
            var health = new Health( 100, 100, 100 );
            health.OnTakingDamage += gameEvents.OnPlayerTakingDamage;
            health.OnNotTakingDamage += gameEvents.OnPlayerNotTakingDamage;
            health.OnDeath += gameEvents.OnPlayerDeath;

            PlayerNeeds = new List<Pair<StatsMap, PlayerNeed>>() {
                new( StatsMap.health, health ),
                new( StatsMap.hunger, new PlayerNeed( 100, 100, 100 ) ),
                new( StatsMap.thirst, new PlayerNeed( 100, 100, 100 ) ),
            };

            foreach( var stat in PlayerNeeds ) {
                statsDictionary.Add( stat.Item1, stat.Item2 );
            }

        }

        public void Tick() {
            float increaseFactor = clockManager.GetTimeScale() * Time.deltaTime / 2f;
            var isHungry = statsDictionary[StatsMap.hunger].CurrentValue <= 0;
            var isThirsty = statsDictionary[StatsMap.thirst].CurrentValue <= 0;
            var isHarmed = otherHarm != null && otherHarm();

            // Health only cares that damage is being taken, not why: starvation, dehydration and the weather (ExposureService) all
            // feed the same flag.
            foreach( var stat in PlayerNeeds ) {
                stat.Item2.Update( increaseFactor, isThirsty || isHungry || isHarmed );
            }
        }

        public void AddStatValue( StatsMap statName, float value ) {
            statsDictionary[ statName ].CurrentValue += value;
        }

        public Stat GetStat( StatsMap statName ) {
            return statsDictionary[ statName ];
        }



        [System.Serializable]
        public class SaveData {
            public SaveData( Vector3 pos, float health, float hunger, float thirst ) {
                this.pos = pos;
                this.health = health;
                this.hunger = hunger;
                this.thirst = thirst;
            }
            public Vector3 pos;
            public float health;
            public float hunger;
            public float thirst;
        }

        public object CaptureState() {
            var data = new SaveData(
                playerRigidbody.position,
                GetStat( StatsMap.health ).CurrentValue,
                GetStat( StatsMap.hunger ).CurrentValue,
                GetStat( StatsMap.thirst ).CurrentValue
            );
            return data;
        }

        public void RestoreState( object state ) {
            var data = ((JObject)state).ToObject<SaveData>();
            playerRigidbody.position = data.pos;
            GetStat( StatsMap.health ).CurrentValue = data.health;
            GetStat( StatsMap.hunger ).CurrentValue = data.hunger;
            GetStat( StatsMap.thirst ).CurrentValue = data.thirst;
        }
    }
}
