using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.MainMenu {

    // Animates the firefly dots MainMenuBuilder lays out: each meanders along its own smooth, continuously
    // re-targeted path around its designed spot, and independently fades low and back on its own slow,
    // random rhythm - like real fireflies drifting and dimming, not a synchronized twinkle.
    public class MainMenuFireflies : MonoBehaviour {

        const float WanderRadius   = 70f;  // how far from its origin a firefly is allowed to drift
        const float MinLegTime     = 3.2f; // one leg of the wander (origin -> a new random point); 20% faster than the 4-8s baseline
        const float MaxLegTime     = 6.4f;

        const float MinScaleFactor = 0.6f; // extra per-instance size variety on top of the baked-in design size
        const float MaxScaleFactor = 1.5f;
        const float MinAlphaFactor = 0.45f; // extra per-instance brightness variety on top of the baked-in design alpha
        const float MaxAlphaFactor = 1f;

        const float MinFadeDelay = 3f;   // how long a firefly stays lit before it starts dimming
        const float MaxFadeDelay = 10f;
        const float MinFadeTime  = 2.5f; // slow fade, both dimming and re-lighting
        const float MaxFadeTime  = 5.5f;
        const float MinDarkAlpha = 0f;   // fireflies fully go dark sometimes, not just dim
        const float MaxDarkAlpha = 0.12f;

        private void OnEnable() {
            foreach( Transform child in transform ) {
                var image = child.GetComponent<Image>();
                var rect = child.GetComponent<RectTransform>();
                if( image == null || rect == null ) { continue; }

                rect.localScale = Vector3.one * Random.Range( MinScaleFactor, MaxScaleFactor );
                var baseAlpha = image.color.a * Random.Range( MinAlphaFactor, MaxAlphaFactor );
                var color = image.color;
                color.a = baseAlpha;
                image.color = color;

                Wander( rect, rect.anchoredPosition );
                ScheduleFade( image, baseAlpha );
            }
        }

        // Keeps re-targeting to a new random point around the ORIGINAL spot (not the current one), so the
        // drift stays a bounded, organic meander instead of an unbounded random walk across the screen.
        private void Wander( RectTransform rect, Vector2 origin ) {
            if( rect == null ) { return; }
            var target = origin + Random.insideUnitCircle * WanderRadius;
            var duration = Random.Range( MinLegTime, MaxLegTime );
            rect.DOAnchorPos( target, duration )
                .SetEase( Ease.OutQuad ) // quick dart, gentle settle - how most insects move, not a smooth glide
                .OnComplete( () => Wander( rect, origin ) )
                .SetId( rect );
        }

        private void ScheduleFade( Image image, float baseAlpha ) {
            DOVirtual.DelayedCall( Random.Range( MinFadeDelay, MaxFadeDelay ), () => RunFade( image, baseAlpha ) )
                .SetId( image );
        }

        private void RunFade( Image image, float baseAlpha ) {
            if( image == null ) { return; }
            var darkAlpha = baseAlpha * Random.Range( MinDarkAlpha, MaxDarkAlpha );
            var sequence = DOTween.Sequence();
            sequence.Append( image.DOFade( darkAlpha, Random.Range( MinFadeTime, MaxFadeTime ) ).SetEase( Ease.InOutSine ) );
            sequence.Append( image.DOFade( baseAlpha, Random.Range( MinFadeTime, MaxFadeTime ) ).SetEase( Ease.InOutSine ) );
            sequence.OnComplete( () => ScheduleFade( image, baseAlpha ) );
            sequence.SetId( image );
        }

        private void OnDisable() {
            foreach( Transform child in transform ) {
                DOTween.Kill( child.GetComponent<RectTransform>() );
                DOTween.Kill( child.GetComponent<Image>() );
            }
        }
    }
}
