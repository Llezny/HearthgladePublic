using DG.Tweening;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Events;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.UI.HUD;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Hearthglade.Gameplay.Map
{
    [System.Serializable]
    public class GradientColor {
        public Color skyColor;
        public Color equatorColor;
        public Color groundColor;
    }

    public class EnvironmentLightningService {
        private MapEvent loadedMapEvent;
        private Map currentMap;
        
        [ Inject ]
        public EnvironmentLightningService( LoadedMapEvent loadedMapEvent ) { 
            RenderSettings.ambientMode = AmbientMode.Trilight;
            this.loadedMapEvent = loadedMapEvent;
            this.loadedMapEvent.RegisterListener( OnMapChange );
        }

        private void OnMapChange( Map newMap ) {
            this.currentMap = newMap;
            SetEnvironmentLightning( newMap, 0.1f );
        }
        
        public void SetEnvironmentLightning() {
            SetEnvironmentLightning( currentMap );
        }
    
        public void SetEnvironmentLightning( Map map, float time = GameConfig.SUN_TRANSITION_TIME ) {
            var clockManager = Object.FindObjectOfType<ClockManager>();
            if( map is null ) {
                UnityEngine.Debug.LogError( $"Map is null, can not set lightning \n {StackTraceUtility.ExtractStackTrace()}" );
                return;
            }

            var mapType = map.MapType;
            SetEnvironmentLightning( clockManager.IsDay ? mapType.dayLight : mapType.nightLight, time );
        }

        private void SetEnvironmentLightning( GradientColor targetColor, float time ) {
            DOTween.To( ()=>RenderSettings.ambientEquatorColor, x => RenderSettings.ambientEquatorColor = x, targetColor.equatorColor, time );
            DOTween.To( ()=>RenderSettings.ambientGroundColor, x => RenderSettings.ambientGroundColor = x, targetColor.groundColor, time );
            DOTween.To( ()=>RenderSettings.ambientSkyColor, x => RenderSettings.ambientSkyColor = x, targetColor.skyColor, time );
        }
    }
}