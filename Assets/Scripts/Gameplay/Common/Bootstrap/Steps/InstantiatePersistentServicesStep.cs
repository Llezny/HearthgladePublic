using UnityEngine;

namespace Hearthglade.Gameplay.Common.Bootstrap.Steps {

    /// <summary>
    /// Instantiates every Autorun BootstrapServiceSO's prefab as DontDestroyOnLoad — the persistent
    /// services (LoadingScreen, AudioManager, SceneLoader...) that survive every scene
    /// load. Runs before the loading screen exists, so unlike the other bootstrap steps it cannot
    /// report progress to it; Object.Instantiate is expected to be effectively instant.
    /// </summary>
    public static class InstantiatePersistentServicesStep {
        public static void Run( ) {
            var services = ResourceLoader.LoadScriptableObjectsOfType<BootstrapServiceSO>( );
            foreach ( var service in services ) {
                if ( service.Autorun ) {
                    Object.DontDestroyOnLoad( Object.Instantiate( service.ServicePrefab ) );
                }
            }
        }
    }
}
