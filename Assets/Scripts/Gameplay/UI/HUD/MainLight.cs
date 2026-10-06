using System.Collections;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Events;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD
{
    [RequireComponent(typeof(Light))]
    public class MainLight : MonoBehaviour {

        public float minLuminosity = 0; 
        public float maxLuminosity = 1;
        private Light lightComponent = null;
        public bool isPlayerUnderground = false;
        private MapEvent loadedMapEvent = null;

        private void Awake() {
            lightComponent = GetComponent<Light>();
            loadedMapEvent = ResourceLoader.LoadAll<LoadedMapEvent>( ResourceLoader.EVENTS_PATH )[0];
        }

        private void OnEnable() {
            loadedMapEvent.RegisterListener( SetLightning );
        }

        private void OnDestroy() {
            loadedMapEvent.UnregisterListener( SetLightning );
        }

        public void FadeInSun(){
            StartCoroutine( FadeSun( true, GameConfig.SUN_TRANSITION_TIME ) ); 
        }

        public void FadeOutSun(){
            StartCoroutine( FadeSun( false, GameConfig.SUN_TRANSITION_TIME ) ); 
        }

        private void SetLightUnderground() {
            isPlayerUnderground = true;
            lightComponent.intensity = 0;
        }

        private void SetLightningSurface() {
            isPlayerUnderground = false;
            var clock = FindObjectOfType<ClockManager>() as ClockManager;
            var isDay = clock.IsDay;
            lightComponent.intensity = isDay ? 2 : 0;
        }

        public void SetLightning( Map.Map currentMap ) {
            var isMapUnderground = currentMap.MapType.isUnderground;
            UnityEngine.Debug.Log( "Setting lightning to: " + (isMapUnderground ? "underground" : "surface") );
            if( isMapUnderground ){
                SetLightUnderground();
            }
            else {
                SetLightningSurface();
            }
        }

        IEnumerator FadeSun( bool fadeIn, float duration ) {

            float counter = 0f;
            float from, to;
            float currentIntensity = lightComponent.intensity;

            if ( fadeIn ) {
                from = minLuminosity;
                to = maxLuminosity;
            }
            else {
                from = maxLuminosity;
                to = minLuminosity;
            }

            while( counter < duration ) {
                if( isPlayerUnderground ) {
                    yield break;
                }
                counter += Time.deltaTime;
                lightComponent.intensity = Mathf.Lerp( from, to, counter / duration );
            
                yield return null;
            }
        }
    }
}
