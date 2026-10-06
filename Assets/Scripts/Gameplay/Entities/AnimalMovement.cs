using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>How a wandering animal moves. Speeds are world units per second, distances are world units (a map cell is 0.3675).</summary>
    [Serializable]
    public class AnimalMovement {

        public float walkSpeed = 0.2f;
        public float runSpeed = 0.5f;

        [Tooltip("How far from where it stands the animal picks the next place to walk to.")]
        public float wanderRadius = 2f;

        [Tooltip("How far it runs when frightened.")]
        public float fleeDistance = 2f;

        [Tooltip("The animal runs off when the player comes closer than this. 0 = never scared by the player.")]
        public float alertDistance = 0f;

        public float minRestSeconds = 1f;
        public float maxRestSeconds = 10f;

        [Tooltip("Degrees per second.")]
        public float turnSpeed = 600f;

        public float RollRest() {
            return Random.Range( minRestSeconds, maxRestSeconds );
        }
    }
}
