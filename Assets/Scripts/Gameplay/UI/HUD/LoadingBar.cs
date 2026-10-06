using System;
using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.HUD
{
    public class LoadingBar : MonoBehaviour {
        Action onLoad;
        Action onCancel;
        float waitTime = 0;
        float currentWaitTime = 0;
        static PlayerController playerController = null; 
        
        [SerializeField] GameObject fill = null;
        [SerializeField] Slider slider = null;
        
        public void EnableLoadingBar( Action finishCallback, float duration = 1, Action cancelCallback = null ){
            if ( playerController == null ) {
                playerController = GameObject.Find("Player").GetComponent<PlayerController>();
            }
            waitTime = duration;
            currentWaitTime = 0;
            playerController.isDuringInteraction = true;
            slider.value = 0;
            fill.SetActive(true);
            this.onLoad += finishCallback;
            this.onCancel += cancelCallback;
        }

        void CompleteLoading() {
            playerController.isDuringInteraction = false;
            onLoad?.Invoke();
        }

        public void OnCancel() {
            onCancel?.Invoke();
        }

        void Update(){
            if(currentWaitTime < waitTime){
                currentWaitTime += Time.deltaTime;
                slider.value     = (currentWaitTime/waitTime);
            }
        
            else {
                if(playerController == null)
                {
                    UnityEngine.Debug.Log("pc is null");
                    return;
                }
                if(playerController.isDuringInteraction) {
                    CompleteLoading();
                }
                Destroy( this.gameObject );
            }
        }
    }
}


