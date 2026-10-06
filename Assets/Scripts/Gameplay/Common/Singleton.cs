using UnityEngine;

namespace CaptureTheFlag.Common  { 
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
        
        protected static T instance;
        public static T Instance {
            get {
                if ( instance is null ) {
                    instance = ( T ) FindObjectOfType( typeof( T ) );
                    if ( instance is null ) {
                        instance = new GameObject( nameof( T ), typeof( T ) ).GetComponent<T>();
                    }
                }
                return instance;
            }
        }

        protected virtual void Awake( ) {
            if ( instance != null && instance != this ) {
                Destroy( this.gameObject  );
            }
            else {
                instance = this as T;
            }
        }
    }
    
    public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour {
        protected override void Awake( ) {
            base.Awake(  );
            DontDestroyOnLoad( this );
        }
    }
}
