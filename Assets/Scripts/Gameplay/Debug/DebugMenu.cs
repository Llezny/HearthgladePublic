using Hearthglade.Gameplay.Debug.Commands;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Debug {
    public class DebugMenu : MonoBehaviour {
        // A single-finger loop, not a multi-finger swipe: most Android OEM skins hijack
        // 2/3/4-finger swipes for screenshot/screen-recording, so those touches never reach Unity.
        // Requiring a near-full turn AND closure back near the start point rules out a half
        // circle and rules out joystick dragging, which arcs but never loops back on itself.
        private const float MinLoopPathLength = 250f;
        private const float MinTotalTurnDegrees = 350f;
        private const float MinStepDistance = 4f;
        private const float MaxClosureDistance = 60f;

        [ SerializeField ] private GameObject menuPanel;

        private GraphicsCommands graphicsCommands;
        private bool tracking;
        private Vector2 startPosition;
        private Vector2 lastPosition;
        private float lastHeading;
        private bool hasLastHeading;
        private float totalTurnDegrees;
        private float pathLength;

        [ Inject ]
        public void Construct( GraphicsCommands graphicsCommands ) {
            this.graphicsCommands = graphicsCommands;
        }

        private void Update() {
            if( Input.GetKeyDown( KeyCode.BackQuote ) ) {
                Toggle();
            }
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            DetectCircleGesture();
#endif
        }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        private void DetectCircleGesture() {
            if( Input.touchCount != 1 ) {
                tracking = false;
                return;
            }

            var touch = Input.GetTouch( 0 );

            if( touch.phase == TouchPhase.Began ) {
                tracking = true;
                startPosition = touch.position;
                lastPosition = touch.position;
                hasLastHeading = false;
                totalTurnDegrees = 0f;
                pathLength = 0f;
                return;
            }

            if( !tracking ) {
                return;
            }

            if( touch.phase == TouchPhase.Canceled ) {
                tracking = false;
                return;
            }

            var delta = touch.position - lastPosition;
            if( delta.magnitude < MinStepDistance ) {
                return;
            }

            var heading = Mathf.Atan2( delta.y, delta.x ) * Mathf.Rad2Deg;
            if( hasLastHeading ) {
                totalTurnDegrees += Mathf.DeltaAngle( lastHeading, heading );
            }
            lastHeading = heading;
            hasLastHeading = true;
            pathLength += delta.magnitude;
            lastPosition = touch.position;

            var closureDistance = ( touch.position - startPosition ).magnitude;
            if( pathLength >= MinLoopPathLength && Mathf.Abs( totalTurnDegrees ) >= MinTotalTurnDegrees && closureDistance <= MaxClosureDistance ) {
                Open();
                tracking = false;
            }
        }
#endif

        public void Open() {
            menuPanel.SetActive( true );
        }

        public void Close() {
            menuPanel.SetActive( false );
        }

        private void Toggle() {
            menuPanel.SetActive( !menuPanel.activeSelf );
        }

        public void LogGraphicsInfo() {
            graphicsCommands.LogGraphicsInfo();
        }
    }
}
