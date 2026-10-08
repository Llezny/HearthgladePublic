using System;
using DG.Tweening;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities {

    /// <summary>What hunting an animal takes and gives (docs/EQUIPMENT_PLAN.md, phase 7); set on the prefab.</summary>
    [ Serializable ]
    public class AnimalHunt {
        [ Tooltip( "Can the player hunt it with a weapon? An animal that cannot be hunted is not interactive at all." ) ]
        public bool Huntable;
        [ Tooltip( "Health of one life: each hit takes the weapon's AttackPower off it (the stone spear hits for 6)." ), Min( 1f ) ]
        public float Health = 10f;
        public ItemSO Meat;
        [ Min( 0 ) ] public int MeatCount = 1;
        [ Tooltip( "What the player murmurs on tapping it while it cannot be hunted; empty stays silent." ) ]
        public string NotHuntableMessage;
    }

    /// <summary>
    /// A harmless animal that wanders around and runs away from the player. Subclasses only say how their
    /// animations are played; the prefab sets how it moves and whether it can be hunted. A hunt is one spear thrust per tap: the animal is hit
    /// at the end of the thrust and runs off, and the next tap chases it; at no health left it falls over and its meat goes into the backpack.
    /// It implements <see cref="IInteractable"/> again so that its hunting members, not <see cref="Entity"/>'s defaults, answer the player.
    /// </summary>
    public abstract class PassiveAnimal : Entity, IInteractable {

        // About the length of the spear plus an arm: the player stops this close to strike.
        private const float StrikeReach = 0.45f;
        // From the start of the thrust to the moment it lands (the spear clip strikes at half of its one second).
        private const float StrikeSeconds = 0.5f;
        private const float FallSeconds = 0.35f;
        private const float VanishSeconds = 0.3f;

        [ SerializeField ] private AnimalMovement movement = new();
        [ SerializeField ] private AnimalForaging foraging = new();
        [ SerializeField ] private AnimalHunt hunt = new();

        private PassiveEntityController controller;
        private IAnimalAnimator animalAnimator;
        private bool bound;
        private bool dying;
        private bool killedByPlayer;
        private Vector3 restScale;
        private Sequence deathTween;

        private HuntingService Hunting => targetMap != null ? targetMap.Hunting : null;

        protected abstract IAnimalAnimator CreateAnimator( );

        protected override float MaxHealth => hunt.Huntable ? hunt.Health : base.MaxHealth;

        private void Awake( ) {
            restScale = transform.localScale;
        }

        public override void Construct( Map.Map targetMap, Transform player ) {
            // A pooled animal comes back from its last death upright and full size.
            deathTween?.Kill( );
            dying = false;
            killedByPlayer = false;
            transform.localScale = restScale;
            base.Construct( targetMap, player );
            Unbind( );
            controller = new PassiveEntityController( transform, targetMap, player, movement, foraging, GetType().Name );
            // A pooled animal keeps its animator between lives.
            animalAnimator ??= CreateAnimator( );
            if( isActiveAndEnabled ) {
                Bind( );
            }
        }

        public override void GetAttacked( PlayerController attackSource ) {
            controller?.GetAttacked( attackSource );
            base.GetAttacked( attackSource );
        }

        // ---- hunting ----

        public float InteractionDistance => StrikeReach;
        public float InteractionDuration => StrikeSeconds;
        public InteractionTiming InteractionTiming => hunt.Huntable ? InteractionTiming.LoadingBar : InteractionTiming.Instant;
        public InteractionType InteractionType => hunt.Huntable ? InteractionType.Attack : InteractionType.Other;

        public string RefusalMessage {
            get {
                if( !hunt.Huntable ) {
                    return string.IsNullOrEmpty( hunt.NotHuntableMessage ) ? null : hunt.NotHuntableMessage;
                }
                return Hunting != null && !Hunting.HasWeapon ? HuntingService.NoWeaponMessage : null;
            }
        }

        public override bool CanInteract( ) {
            return hunt.Huntable && !dying && Hunting != null && Hunting.HasWeapon;
        }

        public void InteractionStart( ) {
            Hunting?.BeginStrike( transform );
        }

        public void InteractCancelCallback( ) {
            Hunting?.CancelStrike( );
        }

        public void InteractionCompleted( ) {
            var hunting = Hunting;
            if( hunting == null || dying ) {
                return;
            }
            float power = hunting.LandStrike( );
            if( power <= 0f ) {
                return;
            }
            controller?.GetAttacked( hunting.PlayerPosition );
            killedByPlayer = Health.CurrentValue - power <= 0f;
            Health.CurrentValue -= power;
        }

        protected override void Die( ) {
            if( dying ) {
                return;
            }
            dying = true;
            if( killedByPlayer ) {
                Hunting?.TakeCatch( hunt.Meat, hunt.MeatCount );
            }
            // No death clip: it tips over on its side, then shrinks away.
            deathTween = DOTween.Sequence( )
                .Append( transform.DORotate( transform.eulerAngles + new Vector3( 0f, 0f, 90f ), FallSeconds ).SetEase( Ease.InQuad ) )
                .AppendInterval( 0.4f )
                .Append( transform.DOScale( Vector3.zero, VanishSeconds ).SetEase( Ease.InBack ) )
                .OnComplete( ( ) => base.Die( ) );
        }

        private void Update( ) {
            if( dying ) {
                return;
            }
            controller?.Update( Time.deltaTime );
        }

        protected override void OnEnable( ) {
            base.OnEnable( );
            if( controller != null ) {
                Bind( );
            }
        }

        protected override void OnDisable( ) {
            deathTween?.Kill( );
            Unbind( );
            base.OnDisable( );
        }

        private void Bind( ) {
            if( bound ) {
                return;
            }
            bound = true;
            controller.StateChanged += animalAnimator.Play;
            animalAnimator.Play( controller.State );
        }

        private void Unbind( ) {
            if( !bound ) {
                return;
            }
            bound = false;
            controller.StateChanged -= animalAnimator.Play;
        }
    }
}
