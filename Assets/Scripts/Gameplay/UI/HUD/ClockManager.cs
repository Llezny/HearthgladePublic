using System;
using Hearthglade.Core.Farming;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.HUD
{
    public class ClockManager : HUD, ISaveable, IWorldClock {
        [ Header( "Clock" ) ]
        [ SerializeField ] float currentTimeInMinutes = 0;
        [ SerializeField ] int daysCounter = 1;
        [ SerializeField ] Slider slider = null;
        [ SerializeField ] Sprite sunIcon = null;
        [ SerializeField ] Sprite moonIcon = null;
        [ SerializeField ] Transform mainLight = null;
        [ SerializeField ] MainLight mainLightComponent = null;

        private bool isDay = true;
        public bool IsDay{ get { return isDay; } set { isDay = value; } }

        // Time of day resets every half-day, so total world minutes are rebuilt from the day counter and phase.
        public long NowMinutes => ( long ) ( daysCounter - 1 ) * GameConfig.MINUTES_PER_DAY
            + ( isDay ? 0 : GameConfig.MINUTES_PER_DAY / 2 )
            + ( long ) currentTimeInMinutes;
        private float timeScale = GameConfig.DEFAULT_TIMESCALE;
        private float clockSpeed = GameConfig.DEFAULT_CLOCK_SPEED;
        protected override Vector2 positionOnClose { get => new( 0, 200 ); }
        protected override Vector2 positionOnOpen { get => new( 0, 0); }
        protected override bool hideOnPause => true;
        protected override bool hideOnAnyMenuShow => true;

        [ SerializeField ] TextMeshProUGUI tooltipText = null;
        private EnvironmentLightningService environmentLightningService = null;
        private SaveManager saveManager;

        [ System.Serializable ]
        public class SaveData {
            public SaveData( bool isDay, float currentTimeInMinutes, int daysCounter ){
                this.isDay = isDay;
                this.currentTimeInMinutes = currentTimeInMinutes;
                this.daysCounter = daysCounter;
            }
            public bool isDay;
            public float currentTimeInMinutes;
            public int daysCounter;
        }
        
        [ Inject ]
        public void Construct( EnvironmentLightningService environmentLightningService, SaveManager saveManager ) {
            this.environmentLightningService = environmentLightningService;
            this.saveManager = saveManager;
            saveManager.RegisterISavable(this);
        }

        protected void Start() {
            slider.maxValue = GameConfig.MINUTES_PER_DAY / 2;
            mainLightComponent = mainLight.gameObject.GetComponent<MainLight>();
            
            if( saveManager.TryGetState<ClockManager>( out var gameState) ) {
                RestoreState(gameState);
            }
        }

        void Update(){
            AddTime( clockSpeed );
            UpdateToolTip();
            if( currentTimeInMinutes > GameConfig.MINUTES_PER_DAY / 2 ){
                SwitchTimeOfTheDay();
                UnityEngine.Debug.Log("switching time of the day");
            }
        }

        public void SwitchTimeOfTheDay() {
            // var sunPos = sunIconTransform.localPosition;
            // var moonPos = moonIconTransform.localPosition;
            // sunIconTransform.DOLocalMove( moonPos, 0.5f );
            // moonIconTransform.DOLocalMove( sunPos, 0.5f );
            if( isDay ){
                mainLightComponent.FadeOutSun();
            }
            else{
                daysCounter ++;
                mainLightComponent.FadeInSun();
            }
            currentTimeInMinutes = 0f;
            isDay = !isDay;
            environmentLightningService.SetEnvironmentLightning();
        }

        public void AddTime( float multiplier = 1f ){
            var val = Time.deltaTime * multiplier * timeScale;
            currentTimeInMinutes += val;
            slider.value = currentTimeInMinutes;
        }

        private string GetFormattedTime( float timeInMinutes ){
            TimeSpan ts = TimeSpan.FromMinutes( timeInMinutes );
            return $"{ts.Hours:D2} : {ts.Minutes:D2}";
        } 

        private void UpdateToolTip(){
            tooltipText.text = $"DAY {daysCounter} \n {GetFormattedTime( currentTimeInMinutes )}";
        }

        public object CaptureState() {
            ResourceLoader.LoadSaveContainer( ).SaveHeader.DaysCounter = daysCounter;
            return new SaveData(
                this.isDay,
                this.currentTimeInMinutes,
                this.daysCounter
            );
        }

        public void RestoreState( object state ) {
            //var jState = (JContainer)state;
            var data = JsonConvert.DeserializeObject<SaveData>( state.ToString( ) );
            
            if( !data.isDay ) {
                // var sunPos = sunIconTransform.localPosition;
                // var moonPos = moonIconTransform.localPosition;
                // sunIconTransform.DOLocalMove( moonPos, 0.1f );
                // moonIconTransform.DOLocalMove( sunPos, 0.1f );
                // mainLightComponent.lightComponent.intensity = mainLightComponent.minLuminosity;

            }
            
            this.isDay = data.isDay;
            this.currentTimeInMinutes = data.currentTimeInMinutes;
            this.daysCounter = data.daysCounter;
        }

        public void ResetTimeScale() {
            timeScale = GameConfig.DEFAULT_TIMESCALE;
        }

        public void SetTimeScale( float newTimeScale ) {
            timeScale = newTimeScale;
        }

        public float GetTimeScale() {
            return timeScale;
        }
    }
}
