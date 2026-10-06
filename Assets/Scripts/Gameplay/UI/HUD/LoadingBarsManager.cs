using System;
using System.Collections.Generic;
using Hearthglade.Gameplay.Items;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD
{
    public class LoadingBarsManager : MonoBehaviour
    {
        [SerializeField] GameObject loadingBarPrefab = null;
        [SerializeField] List<LoadingBar> loadingBars = null;
        public static LoadingBarsManager instance = null;

        void Awake(){
            instance = this;
        }
        public void CreateLoadingBar(Action finishCallback, float duration = 2f, Action cancelCallback = null ){
            UnityEngine.Debug.Log("Created loading bar.");
            GameObject bar = Instantiate(
                loadingBarPrefab,
                this.transform
            );
            var barComponent = bar.GetComponent<LoadingBar>();
            barComponent.EnableLoadingBar( finishCallback, duration, cancelCallback );
            DeleteAllWaitableBars();
            loadingBars.Add(barComponent);
        }

        public void DeleteAllWaitableBars(){
            foreach ( var bar in loadingBars ) {
                if( bar != null ) {
                    bar.OnCancel();
                    Destroy( bar.gameObject );
                    continue;
                }
                UnityEngine.Debug.LogWarning( "Would be great to not have this nullcheck in future" );
            }
            loadingBars.Clear();
        }

    }
}
