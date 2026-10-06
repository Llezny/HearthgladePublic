using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Hearthglade.Gameplay.Animation
{
    [Serializable]
    public class AnimationController : IAnimationController{

        private string addressableGroupPrefix = "PlayerAnim";
        private AnimancerComponent animancerComponent;
        private Dictionary<string, AnimationClip> animations = new Dictionary<string, AnimationClip>();

        public static AnimationController CreateComponent( GameObject target, Animator animator, string addressableGroupPrefix = "PlayerAnim" ) {
            var animationController = new AnimationController();
            animationController.animancerComponent = AnimancerHelper.AddAnimancer( target );
            animationController.animancerComponent.Animator = animator;
            animationController.addressableGroupPrefix = addressableGroupPrefix;
            return animationController;
        }

        public void PlayAnimation( string animName, float normalizedEndTime = 0.5f, Action animationCallback = null ) {
            if( !animations.TryGetValue( animName, out var clip ) ) {
                Addressables.LoadAssetAsync<AnimationClip>($"{addressableGroupPrefix}/{animName}").Completed += 
                    handle => AddAnimAndSet( handle, animName, normalizedEndTime, animationCallback );
                return;
            }
            PlayAnimation( clip, normalizedEndTime, animationCallback );
        }

        private void AddAnimAndSet( AsyncOperationHandle<AnimationClip> clipHandle, string animName, float normalizedEndTime, Action animationCallback ) {
            var clip = clipHandle.Result;
            animations.TryAdd( animName, clip );
            PlayAnimation( clip, normalizedEndTime, animationCallback );
        }

        private void PlayAnimation( AnimationClip clip, float normalizedEndTime, Action animationCallback) {
            // The clip is loaded asynchronously: its owner may be gone by the time it arrives (scene change, despawn).
            if( animancerComponent == null ) {
                return;
            }
            var animState = animancerComponent.Play( clip, 0.15f, FadeMode.NormalizedSpeed );
            animState.Events.NormalizedEndTime = normalizedEndTime;
            if( animationCallback is not null ) {
                animState.Events.Add( normalizedEndTime, animationCallback );
            }
        }
    }
}
