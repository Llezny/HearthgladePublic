using Hearthglade.Gameplay.Common.SceneLoader;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using UnityEngine;

namespace Hearthglade.Gameplay.Common {
    public class ResourceLoader {

        public static readonly string SO_PATH = "ScriptableObjects/";
        public static readonly string SCENES_PATH = SO_PATH + "Scenes";
        public static readonly string EVENTS_PATH = SO_PATH + "Events";
        public static readonly string PORTS_PATH = SO_PATH + "Ports";
        public static readonly string EXPEDITIONS_PATH = SO_PATH + "Expeditions";
        public static readonly string SHIP_TREE_PATH = SO_PATH + "ShipTree";
        public static readonly string SAVE_DATA_CONTAINER = SO_PATH + "Save/SaveDataContainer" ;

        public static T Load<T>( string path ) where T : Object {
            return Resources.Load<T>( path );
        }
        
        public static T[] LoadAll<T>( string path ) where T : Object {
            return Resources.LoadAll<T>( path );
        }

        public static T[] LoadScriptableObjectsOfType<T>( ) where T : Object {
            return LoadAll<T>( SO_PATH );
        }
        
        public static GameScene[] LoadScenes( ) => LoadAll<GameScene>( SCENES_PATH );
        public static SaveDataContainer LoadSaveContainer( ) => Load<SaveDataContainer>( SAVE_DATA_CONTAINER );

    }
}