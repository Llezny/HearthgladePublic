using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player;
using UnityEngine;

namespace Hearthglade.Gameplay.Animation
{
    /// <summary>
    /// The one place that decides which clip the player plays for an interaction:
    /// <list type="number">
    /// <item>the interactable's own clip if it has one (<see cref="IInteractable.AnimationOverride"/>: fishing, the well);</item>
    /// <item>otherwise, for gathering, the motion of <see cref="GatherMotionRules"/>: the clip of the tool in use, gathering by hand, or picking up;
    /// a strike at an animal (<see cref="InteractionType.Attack"/>) plays the clip of the weapon in use;</item>
    /// <item>nothing for an interaction that is not gathering.</item>
    /// </list>
    /// A tool group without a clip of its own falls back to gathering by hand.
    /// </summary>
    public sealed class InteractionAnimationPicker
    {
        // Where the work lands in a clip that brings no hit time of its own.
        public const float DefaultHitTime = 0.5f;
        // The blend into a clip that brings no fade of its own.
        public const float DefaultFadeIn = 0.15f;

        private readonly PlayerToolService tools;
        private readonly GatherAnimationSet set;

        public InteractionAnimationPicker( PlayerToolService tools ) {
            this.tools = tools;
            set = Resources.Load<GatherAnimationSet>( GatherAnimationSet.ResourcePath );
            if( set == null ) {
                UnityEngine.Debug.LogError( $"No GatherAnimationSet at Resources/{GatherAnimationSet.ResourcePath}" );
            }
        }

        // Asked once the interaction has started, so the tool in use is known.
        public bool TryPick( IInteractable interactable, out GatherAnimationSet.Motion picked ) {
            picked = default;
            // An instant interaction is over before it starts: a work loop would play on after it.
            if( interactable == null || interactable.InteractionTiming == InteractionTiming.Instant ) {
                return false;
            }
            if( interactable.AnimationOverride != null ) {
                picked = new GatherAnimationSet.Motion { Clip = interactable.AnimationOverride, HitTime = DefaultHitTime, FadeIn = DefaultFadeIn };
                return true;
            }
            var gathered = interactable.Gathered;
            var inUse = tools.InUse.Found ? tools.InUse.Definition.ToolGroup : ToolGroup.None;
            // A strike at an animal is work with the weapon in use, like gathering with a tool.
            bool isAttack = interactable.InteractionType == InteractionType.Attack;
            if( isAttack && inUse == ToolGroup.None ) {
                return false;
            }
            bool isWork = gathered != null || isAttack;
            var motion = GatherMotionRules.For( isWork, gathered != null ? gathered.Skill : GatherSkill.None, inUse );
            return set != null && TryGetMotion( motion, inUse, out picked );
        }

        private bool TryGetMotion( GatherMotion motion, ToolGroup inUse, out GatherAnimationSet.Motion chosen ) {
            switch( motion ) {
                case GatherMotion.Tool:
                    if( set.TryGet( inUse, out chosen ) ) {
                        return true;
                    }
                    chosen = set.Hands;
                    return chosen.IsSet;
                case GatherMotion.Hands:
                    chosen = set.Hands;
                    return chosen.IsSet;
                case GatherMotion.Pickup:
                    chosen = set.Pickup;
                    return chosen.IsSet;
                default:
                    chosen = default;
                    return false;
            }
        }
    }
}
