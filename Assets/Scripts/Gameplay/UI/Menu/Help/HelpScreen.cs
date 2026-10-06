using System;
using DG.Tweening;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Events;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Help
{
    public class HelpScreen : Common.Menu {

        private ControlEvents controlEvents;

        [ Inject ]
        public void Construct( ControlEvents controlEvents ) {
            this.controlEvents = controlEvents;
        }

        void OnEnable(){
            controlEvents.onHelpButton += OpenMenuFromButton;
        }

        void OnDisable(){
            controlEvents.onHelpButton -= OpenMenuFromButton;
        }

        private void OpenMenuFromButton() {
            OpenMenu();
        }

        public override void OpenMenu() {
            base.OpenMenu();
            contentRectTransform.DOScale( 1, 0.1f );
        }

        public override void CloseMenu( bool checkIfFadeOutShroud = true ) {
            base.CloseMenu( checkIfFadeOutShroud );
            contentRectTransform.DOScale( 0, 0.1f );
        }

        protected override void Start() {
            base.Start();
            bool IsFirstSession = PlayerPrefs.GetInt( "HasPlayed" ) == 0;

            if( IsFirstSession && GameConfig.HELP_SCREEN_ENABLED ) {
                OpenMenu();
                PlayerPrefs.SetInt( "HasPlayed", 1 );
                PlayerPrefs.Save();
            }
        }

        public static Vector2[] GetPointsOnCircle(Vector2 _center, float _radius, int n){
            Vector2[] points = new Vector2[n];
            float step = 360f / n;
            int j = 0;
            for(float degree = 0; degree < 360; degree += step){
                float radian = (degree * 3.14f) / 180; 
                points[j] = new Vector2((float)Math.Sin(radian), (float)Math.Cos(radian));
                j++;
            }
            return points;
        }
    }
}
