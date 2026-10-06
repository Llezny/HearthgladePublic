using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace Hearthglade.Gameplay.Common.Extension {
    public static class DoTweenExtensions {
        public static TweenerCore<Vector3, Vector3, VectorOptions> DOMoveInTargetLocalSpace(this Transform transform, Transform target, Vector3 targetLocalEndPosition, float duration){
            var tween = DOTween.To(
                () => transform.position - target.transform.position,
                x => transform.position = x + target.transform.position,
                targetLocalEndPosition, 
                duration);
            tween.SetTarget(transform);
            return tween;
        }
    }
}