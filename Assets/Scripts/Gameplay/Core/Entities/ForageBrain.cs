using System.Collections.Generic;
using Hearthglade.Core.Farming;
using Hearthglade.Core.World;

namespace Hearthglade.Core.Entities {

    /// <summary>What an animal will eat from the garden and how often it goes looking. Distances are in cells (0.3675 m).</summary>
    public readonly struct ForageSettings {
        public ForageKind Diet { get; }
        public float RadiusCells { get; }
        public float EatSeconds { get; }
        /// <summary>Chance that, when a rest runs out, the animal goes for food instead of wandering.</summary>
        public float ForageChance { get; }
        /// <summary>How long it leaves a plot alone after a meal.</summary>
        public float CooldownSeconds { get; }
        /// <summary>How long it leaves a plot alone after it could not get to it (a fence in the way).</summary>
        public float BlockedCooldownSeconds { get; }

        public ForageSettings( ForageKind diet, float radiusCells, float eatSeconds, float forageChance, float cooldownSeconds, float blockedCooldownSeconds ) {
            Diet = diet;
            RadiusCells = radiusCells;
            EatSeconds = eatSeconds;
            ForageChance = forageChance;
            CooldownSeconds = cooldownSeconds;
            BlockedCooldownSeconds = blockedCooldownSeconds;
        }
    }

    /// <summary>
    /// Decides when an animal goes for ripe produce in the garden. It extends a <see cref="WanderBrain"/> (which owns
    /// the state) with Seeking and Eating, and asks the <see cref="FarmModel"/> what there is to eat. There is no
    /// pathfinding: the animal walks straight at the plot and gives up when the way is blocked, which is exactly how a
    /// fence protects a field.
    /// </summary>
    public sealed class ForageBrain {

        private readonly WanderBrain brain;
        private readonly ForageSettings settings;
        private readonly DeterministicRandom random;
        private readonly Dictionary<PlotKey, float> cooldowns = new();
        private readonly List<PlotKey> expired = new();

        /// <summary>The plot the animal is heading for or eating at.</summary>
        public PlotKey? Target { get; private set; }

        public bool Enabled => settings.Diet != ForageKind.None;

        public ForageBrain( WanderBrain brain, ForageSettings settings, DeterministicRandom random ) {
            this.brain = brain;
            this.settings = settings;
            this.random = random;
        }

        /// <summary>Call when a rest has run out: with the forage chance, picks the nearest ripe plot and starts seeking it.</summary>
        public bool TryStartForaging( FarmModel farm, long now, float xCells, float zCells ) {
            if( !Enabled || farm == null || random.NextFloat() >= settings.ForageChance ) {
                return false;
            }
            if( !farm.TryFindForageTarget( xCells, zCells, settings.RadiusCells, settings.Diet, now, key => !cooldowns.ContainsKey( key ), out var target ) ) {
                return false;
            }
            Target = target;
            brain.StartSeeking();
            return true;
        }

        /// <summary>The animal stands at the plot: it starts eating.</summary>
        public void Arrived() {
            brain.StartEating( settings.EatSeconds );
        }

        /// <summary>Counts the meal down; true when it is time to take the food.</summary>
        public bool MealDone( float deltaSeconds ) {
            return brain.FinishedEating( deltaSeconds );
        }

        /// <summary>Takes one piece of ripe produce (nothing if it is gone by now) and goes back to resting.</summary>
        public int FinishMeal( FarmModel farm, long now, float restSeconds, string eater ) {
            int eaten = 0;
            if( Target.HasValue ) {
                eaten = farm.Eat( Target.Value, now, 1, eater );
                cooldowns[ Target.Value ] = settings.CooldownSeconds;
            }
            Target = null;
            brain.ReachedDestination( restSeconds );
            return eaten;
        }

        /// <summary>The way to the plot is blocked or the food is gone: forget it for a while and rest.</summary>
        public void GiveUp( float restSeconds ) {
            if( Target.HasValue ) {
                cooldowns[ Target.Value ] = settings.BlockedCooldownSeconds;
            }
            Target = null;
            brain.GiveUpWalking( restSeconds );
        }

        /// <summary>Something scared the animal off (its own flee logic runs the state): no cooldown, it may come back.</summary>
        public void Interrupt() {
            Target = null;
        }

        /// <summary>Lets the cooldowns run down.</summary>
        public void Update( float deltaSeconds ) {
            if( cooldowns.Count == 0 ) {
                return;
            }
            expired.Clear();
            foreach( var key in new List<PlotKey>( cooldowns.Keys ) ) {
                float left = cooldowns[ key ] - deltaSeconds;
                if( left <= 0f ) {
                    expired.Add( key );
                } else {
                    cooldowns[ key ] = left;
                }
            }
            foreach( var key in expired ) {
                cooldowns.Remove( key );
            }
        }

        public bool IsOnCooldown( PlotKey key ) => cooldowns.ContainsKey( key );
    }
}
