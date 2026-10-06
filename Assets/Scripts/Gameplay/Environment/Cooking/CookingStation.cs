using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Environment.Cooking {
    public class CookingStation : SceneObject, ISceneObjectWithData {

        // Fireplace prefabs leave this at Fireplace and get upgraded in-game by using a Pot on them;
        // a stone oven prefab is placed already set to StoneOven.
        [ SerializeField ] private CookingStationLevel initialLevel = CookingStationLevel.Fireplace;
        public CookingStationLevel InitialLevel => initialLevel;

        // Shown/hidden in the world as the station's actual level changes - stays inactive at
        // Fireplace level, appears once a Pot is dropped in.
        [ SerializeField ] private GameObject potVisual;

        private CookingService cookingService;
        // The chunk goes inactive before its objects are handed back, by which time the service has forgotten us.
        private object capturedState;

        [ Inject ]
        public void Construct( CookingService cookingService ) {
            this.cookingService = cookingService;
            capturedState = null;
            if (!this.cookingService.RegisterCookingStation( this )) {
                UnityEngine.Debug.LogError( "Cooking station with id: " + gameObject.GetHashCode() + " already exists!" );
            }
            cookingService.StationUpgraded += OnStationUpgraded;
            UpdatePotVisual( initialLevel );
        }

        // Chunk objects are pooled: a despawn only disables, and the next spawn injects again.
        private void OnDisable() {
            if( cookingService != null ) {
                capturedState = cookingService.CaptureCookingStationState( this );
                cookingService.StationUpgraded -= OnStationUpgraded;
                cookingService.UnregisterCookingStation( this );
                cookingService = null;
            }
        }

        private void OnStationUpgraded( CookingStation station, CookingStationState state ) {
            if( station == this ) {
                UpdatePotVisual( state.Level );
            }
        }

        private void UpdatePotVisual( CookingStationLevel level ) {
            if( potVisual != null ) {
                potVisual.SetActive( level >= CookingStationLevel.Pot );
            }
        }

        public void Hover( ) { }

        public override void InteractionStart() {
            cookingService.SetActiveCookingStation( this );
        }

        public object CaptureState( ) {
            return cookingService != null ? cookingService.CaptureCookingStationState( this ) : capturedState;
        }

        public void RestoreState(object state) {
            if( state is null ) {
                return;
            }
            cookingService.RestoreCookingStationState( this, state.ToString() );
            UpdatePotVisual( cookingService.GetCookingStationLevel( this ) );
        }



    }
}
