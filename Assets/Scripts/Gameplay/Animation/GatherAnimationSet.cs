using System;
using Hearthglade.Core.Items;
using UnityEngine;

namespace Hearthglade.Gameplay.Animation
{
    /// <summary>
    /// The clips of gathering, one per <see cref="GatherMotion"/>: picking up, gathering by hand, and one per tool group (pickaxes share one clip,
    /// axes another). <see cref="InteractionAnimationPicker"/> chooses among them; the set is loaded from Resources, so adding a group is data only.
    /// </summary>
    [CreateAssetMenu(menuName = "Hearthglade/Animation/Gather animation set", fileName = "GatherAnimations")]
    public class GatherAnimationSet : ScriptableObject
    {
        public const string ResourcePath = "ScriptableObjects/ToolAnimations";

        [Serializable]
        public struct Motion
        {
            public AnimationClip Clip;
            [Tooltip("Where in the clip (0-1) the work lands: the target shakes and the hit sound plays.")]
            [Range(0.05f, 0.95f)] public float HitTime;
            [Tooltip("Seconds of the blend from what the player was doing: longer for a loop that is played crouched, so getting down is not a snap.")]
            [Min(0f)] public float FadeIn;

            public bool IsSet => Clip != null;
        }

        [Serializable]
        public struct Entry
        {
            public ToolGroup Group;
            public AnimationClip Clip;
            [Range(0.05f, 0.95f)] public float HitTime;
            [Min(0f)] public float FadeIn;
        }

        [Tooltip("Something no tool helps with, picked up by hand: sticks, mushrooms, herbs.")]
        [SerializeField] Motion pickup;
        [Tooltip("Something a tool could help with, gathered by hand because none is in use: crops, fruit, bushes.")]
        [SerializeField] Motion hands;
        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Motion Pickup => pickup;
        public Motion Hands => hands;

        public bool TryGet(ToolGroup group, out Motion motion)
        {
            foreach (var candidate in entries)
            {
                if (candidate.Group == group && candidate.Clip != null)
                {
                    motion = new Motion { Clip = candidate.Clip, HitTime = candidate.HitTime, FadeIn = candidate.FadeIn };
                    return true;
                }
            }
            motion = default;
            return false;
        }
    }
}
