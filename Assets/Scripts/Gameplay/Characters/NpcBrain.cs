using Animancer;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.Trade;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// What a standing NPC does: idles, turns to the player who comes near and waves once, talks while the player trades, and turns
    /// back to where it was set when the player leaves. It moves only the body of the model, never the root of the prefab.
    /// </summary>
    public class NpcBrain : MonoBehaviour
    {
        [SerializeField] AnimancerComponent animancer;
        [SerializeField] Transform body;
        [SerializeField] AnimationClip idle;
        [SerializeField] AnimationClip wave;
        [SerializeField] AnimationClip talk;
        [SerializeField, Tooltip("The player this close gets noticed and greeted (metres).")] float noticeDistance = 0.9f;
        [SerializeField, Tooltip("A noticed player has to go this far to be greeted again (metres).")] float leaveDistance = 1.4f;
        [SerializeField] float turnDegreesPerSecond = 220f;
        [SerializeField] float talkSeconds = 3f;
        [SerializeField] float fadeSeconds = 0.25f;

        private PlayerController player;
        private Trader trader;
        private Quaternion homeRotation;
        private bool noticed;
        private float talkUntil;
        private float gestureUntil;

        [Inject]
        public void Construct(PlayerController player)
        {
            this.player = player;
        }

        private void OnEnable()
        {
            homeRotation = body.rotation;
            PlayIdle(true);
            trader = GetComponent<Trader>();
            if (trader != null)
            {
                trader.Interacted += Talk;
            }
        }

        private void OnDisable()
        {
            if (trader != null)
            {
                trader.Interacted -= Talk;
            }
        }

        /// <summary>The player is dealing with this NPC: talk for a while.</summary>
        public void Talk()
        {
            talkUntil = Time.time + talkSeconds;
            gestureUntil = talkUntil;
            Play(talk);
        }

        private void Update()
        {
            bool talking = Time.time < talkUntil;
            var toPlayer = Vector3.zero;
            if (player != null)
            {
                toPlayer = player.transform.position - body.position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;
                if (noticed && distance > leaveDistance)
                {
                    noticed = false;
                }
                else if (!noticed && distance < noticeDistance)
                {
                    noticed = true;
                    if (!talking && wave != null)
                    {
                        gestureUntil = Time.time + wave.length;
                        Play(wave);
                    }
                }
            }

            bool attentive = (noticed || talking) && toPlayer.sqrMagnitude > 1e-4f;
            var target = attentive ? Quaternion.LookRotation(toPlayer) : homeRotation;
            body.rotation = Quaternion.RotateTowards(body.rotation, target, turnDegreesPerSecond * Time.deltaTime);

            // A gesture that has run its course goes back to idle.
            if (gestureUntil > 0f && Time.time >= gestureUntil)
            {
                gestureUntil = 0f;
                PlayIdle(false);
            }
        }

        private void PlayIdle(bool randomStart)
        {
            var state = Play(idle);
            if (state != null && randomStart)
            {
                state.NormalizedTime = Random.value;
            }
        }

        private AnimancerState Play(AnimationClip clip)
        {
            return clip != null && animancer != null ? animancer.Play(clip, fadeSeconds) : null;
        }
    }
}
