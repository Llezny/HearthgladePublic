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


        public List<Pair<StatsMap, PlayerNeed>> PlayerNeeds;

        public List<Pair<StatsMap, PlayerAttribute>> staticStatsList = new List<Pair<StatsMap, PlayerAttribute>>() {
            new( StatsMap.miningSpeed, new PlayerAttribute( 0, 1, 0 ) ),
            new( StatsMap.cuttingTreesSpeed, new PlayerAttribute( 0, 1, 0 ) ),
        };

        public PlayerStatsModel( Rigidbody playerRigidbody, GameEvents gameEvents, ClockManager clockManager ) {
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
            foreach( var stat in staticStatsList ) {
                statsDictionary.Add( stat.Item1, stat.Item2 );
            }

        }

        public void Tick() {
            float increaseFactor = clockManager.GetTimeScale() * Time.deltaTime / 2f;
            var isHungry = statsDictionary[StatsMap.hunger].CurrentValue <= 0;
            var isThirsty = statsDictionary[StatsMap.thirst].CurrentValue <= 0;

            // Starvation/dehydration is currently the only source of damage, so it's what
            // Health.Update's isTakingDamage flag is derived from - but Health itself only cares
            // that damage is being taken, not why, so a future damage source (combat, fall damage,
            // etc.) can feed into the same flag without another rename.
            foreach( var stat in PlayerNeeds ) {
                stat.Item2.Update( increaseFactor, isThirsty || isHungry );
            }
        }

        public void AddStatValue( StatsMap statName, float value ) {
            statsDictionary[ statName ].CurrentValue += value;
        }

        public Stat GetStat( StatsMap statName ) {
            return statsDictionary[ statName ];
        }

        public void RemoveStatModifiers( List<AttributeModifier> modifiers ) {
            foreach( var modifier in modifiers ){
                if( statsDictionary[modifier.TargetStat] is PlayerAttribute attribute ) {
                    attribute.RemoveModifier( modifier );
                } else {
                    UnityEngine.Debug.LogWarning( $"Cannot remove modifier targeting {modifier.TargetStat}: it is not a PlayerAttribute." );
                }
            }
        }

        public void AddStatModifiers( List<AttributeModifier> modifiers ) {
            foreach( var modifier in modifiers ){
                if( statsDictionary[modifier.TargetStat] is PlayerAttribute attribute ) {
                    attribute.AddModifier( modifier );
                } else {
                    UnityEngine.Debug.LogWarning( $"Cannot add modifier targeting {modifier.TargetStat}: it is not a PlayerAttribute." );
                }
            }
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
