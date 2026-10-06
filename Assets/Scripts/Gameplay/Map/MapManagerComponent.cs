// using System;
// using Hearthglade.Gameplay.UI.Menu.MainMenu;
// using UnityEngine;
// using VContainer;

// namespace Hearthglade.Gameplay.Map {
//     public class MapManagerComponent : MonoBehaviour, ISaveable {
        
//         [field:SerializeField]
//         public Transform MapHolder { get; private set; }

//         public MapManager MapManager { get; private set; }
//         private SaveManager saveManager;
        
//         [ Inject ]
//         public void Construct( MapManager mapManager, SaveManager saveManager ) {
//             this.MapManager = mapManager;
//             this.MapManager.MapHolder = this.MapHolder;
//             this.saveManager = saveManager;
//         }

//         private void Start( ) {
//             this.MapManager.Setup( );
//         }

//         public object CaptureState( ) {
//             return new MapManagerSaveData( MapManager );
//         }

//         public void RestoreState( object state ) {
//             saveManager.RegisterISavable( this );
//             this.MapManager.RestoreState( state );
//         }
        
//         void OnDestroy() {
//             this.MapManager.CleanMapEvents( );
//         }
//     }
// }