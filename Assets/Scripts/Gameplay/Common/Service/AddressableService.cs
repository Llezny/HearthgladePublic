using System;
using System.Threading.Tasks;
using Hearthglade.Gameplay.Environment.Cooking;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Hearthglade.Gameplay.Common.Service {
    public class AddressableService : MonoBehaviour {

        public AsyncOperationHandle<T> Download<T>( string addressableName, Action<AsyncOperationHandle<T>> onComplete, bool releaseHandleOnComplete = false ) {
            var handle = Addressables.LoadAssetAsync<T>( addressableName );
                handle.Completed += _ => {
                    if( handle.Status == AsyncOperationStatus.Failed ) {
                        UnityEngine.Debug.LogError( $"Addressable download failed. {handle.ToString()}");
                        return; 
                    }
                    onComplete.Invoke( handle );
                };
                if ( releaseHandleOnComplete ) {
                    Addressables.Release( handle );
                }
                return handle;
        } 
    }
}