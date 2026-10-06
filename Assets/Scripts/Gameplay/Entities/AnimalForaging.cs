using System;
using Hearthglade.Core.Entities;
using Hearthglade.Core.Farming;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>What a wandering animal eats from the garden. The default diet is empty: it leaves crops alone.</summary>
    [Serializable]
    public class AnimalForaging {

        [Tooltip("The kinds of ripe produce it goes for. None = never raids the garden.")]
        public ForageKind diet = ForageKind.None;

        [Tooltip("How far from where it stands it notices ripe crops, in world units (a map cell is 0.3675).")]
        public float radius = 2f;

        [Tooltip("Seconds it spends eating one piece.")]
        public float eatSeconds = 3f;

        [Range( 0f, 1f ), Tooltip("Chance that, when a rest runs out, it goes for food instead of just wandering.")]
        public float forageChance = 0.5f;

        [Tooltip("Seconds it leaves a plot alone after a meal.")]
        public float cooldownSeconds = 60f;

        [Tooltip("Seconds it ignores a plot it could not reach (a fence in the way).")]
        public float blockedCooldownSeconds = 120f;

        public ForageSettings ToSettings() {
            return new ForageSettings( diet, radius / Map.MapGenerator.TILE_X_OFFSET, eatSeconds, forageChance, cooldownSeconds, blockedCooldownSeconds );
        }
    }
}
