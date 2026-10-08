using System;
using Hearthglade.Core.Entities;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>
    /// Moves a harmless animal: it rests, wanders to a nearby free spot and runs away from the player when they
    /// come too close. What it wants is decided by the Core <see cref="WanderBrain"/> and <see cref="ForageBrain"/>
    /// (which also sends it to ripe crops of the garden, if its diet allows), this class only walks the body over
    /// free ground and reports state changes (for the animations).
    /// </summary>
    public class PassiveEntityController {

        private const float ArrivalDistance = 0.05f;
        private const int DestinationTries = 10;

        private readonly Transform body;
        private readonly Map.Map map;
        private readonly Transform threat;
        private readonly AnimalMovement movement;
        private readonly WanderBrain brain;
        private readonly ForageBrain forage;
        private readonly string eaterName;
        private Vector3 destination;

        public event Action<WanderState> StateChanged;
        public WanderState State => brain.State;

        public PassiveEntityController( Transform body, Map.Map map, Transform threat, AnimalMovement movement, AnimalForaging foraging, string eaterName ) {
            this.body = body;
            this.map = map;
            this.threat = threat;
            this.movement = movement;
            this.eaterName = eaterName;
            this.brain = new WanderBrain( movement.RollRest() );
            this.forage = new ForageBrain( brain, foraging.ToSettings(), new DeterministicRandom( ( int ) DateTime.UtcNow.Ticks ^ body.GetHashCode() ) );
        }

        public void Update( float deltaTime ) {
            forage.Update( deltaTime );
            if( State != WanderState.Running && IsThreatened() ) {
                FleeFrom( threat.position );
            }
            switch( State ) {
                case WanderState.Idle:
                    if( brain.WantsToWalk( deltaTime ) && !TryForage() ) {
                        Wander();
                    }
                    return;
                case WanderState.Eating:
                    Eat( deltaTime );
                    return;
                default:
                    Walk( deltaTime );
                    return;
            }
        }

        public void GetAttacked( PlayerController attacker ) {
            FleeFrom( attacker.transform.position );
        }

        // Hit by something at `danger` (a spear thrust): run away from it.
        public void GetAttacked( Vector3 danger ) {
            FleeFrom( danger );
        }

        private bool IsThreatened() {
            if( threat == null ) {
                return false;
            }
            var offset = threat.position - body.position;
            offset.y = 0f;
            return WanderBrain.IsThreatened( offset.magnitude, movement.alertDistance );
        }

        private void Wander() {
            if( !TryPickDestination( body.position, movement.wanderRadius, out destination ) ) {
                brain.GiveUpWalking( movement.RollRest() );
                return;
            }
            brain.StartWalking();
            StateChanged?.Invoke( State );
        }

        // Ripe produce of the garden within reach of its diet: walk straight to the plot.
        private bool TryForage() {
            var farm = map.Farm;
            if( !forage.Enabled || farm == null || map.WorldClock == null ) {
                return false;
            }
            var position = body.position;
            if( !forage.TryStartForaging( farm, map.WorldClock.NowMinutes, position.x / MapGenerator.TILE_X_OFFSET, position.z / MapGenerator.TILE_Z_OFFSET ) ) {
                return false;
            }
            var cell = forage.Target.Value;
            destination = MapHelper.GridToWorldPosition( new Vector2( cell.X, cell.Z ) );
            destination.y = position.y;
            StateChanged?.Invoke( State );
            return true;
        }

        private void Eat( float deltaTime ) {
            var farm = map.Farm;
            long now = map.WorldClock != null ? map.WorldClock.NowMinutes : 0;
            // The food may be gone by now: the player picked it, or another animal got it first.
            if( farm == null || !forage.Target.HasValue || farm.Describe( forage.Target.Value, now ).ReadyYield <= 0 ) {
                forage.GiveUp( movement.RollRest() );
                StateChanged?.Invoke( State );
                return;
            }
            if( forage.MealDone( deltaTime ) ) {
                forage.FinishMeal( farm, now, movement.RollRest(), eaterName );
                StateChanged?.Invoke( State );
            }
        }

        private void FleeFrom( Vector3 danger ) {
            var away = body.position - danger;
            away.y = 0f;
            if( away.sqrMagnitude < 0.0001f ) {
                away = body.forward;
            }
            away.Normalize();
            for( int i = 0; i < DestinationTries; i++ ) {
                // Straight away from the danger first, then wider and wider angles when that way is blocked.
                var direction = Quaternion.Euler( 0f, Random.Range( -1f, 1f ) * 20f * ( i + 1 ), 0f ) * away;
                var candidate = body.position + direction * movement.fleeDistance;
                if( map.IsFreeGround( candidate ) ) {
                    destination = candidate;
                    forage.Interrupt();
                    brain.StartRunning();
                    StateChanged?.Invoke( State );
                    return;
                }
            }
        }

        private bool TryPickDestination( Vector3 origin, float radius, out Vector3 result ) {
            for( int i = 0; i < DestinationTries; i++ ) {
                var offset = Random.insideUnitCircle * radius;
                result = origin + new Vector3( offset.x, 0f, offset.y );
                if( map.IsFreeGround( result ) ) {
                    return true;
                }
            }
            result = origin;
            return false;
        }

        private void Walk( float deltaTime ) {
            var toDestination = destination - body.position;
            toDestination.y = 0f;
            float distance = toDestination.magnitude;
            if( distance < ArrivalDistance ) {
                Arrive();
                return;
            }

            float speed = State == WanderState.Running ? movement.runSpeed : movement.walkSpeed;
            var direction = toDestination / distance;
            var next = body.position + direction * Mathf.Min( speed * deltaTime, distance );
            if( !map.CanStep( body.position, next ) ) {
                // A fence, a wall or a closed gate: a food seeker gives that plot up for a while.
                if( State == WanderState.Seeking ) {
                    forage.GiveUp( movement.RollRest() );
                    StateChanged?.Invoke( State );
                    return;
                }
                Stop();
                return;
            }

            body.position = next;
            body.rotation = Quaternion.RotateTowards( body.rotation, Quaternion.LookRotation( direction ), movement.turnSpeed * deltaTime );
        }

        private void Arrive() {
            if( State == WanderState.Seeking ) {
                forage.Arrived();
                StateChanged?.Invoke( State );
                return;
            }
            Stop();
        }

        private void Stop() {
            brain.ReachedDestination( movement.RollRest() );
            StateChanged?.Invoke( State );
        }
    }
}
