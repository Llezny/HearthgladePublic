using System;
using UnityEngine;

namespace Hearthglade.Gameplay.Events
{
    public class ControlEvents : MonoBehaviour {

        public event Action onInventoryButton;
        public void OnInventoryButton(){
            onInventoryButton?.Invoke();
        }

        public event Action onCraftingButton;
        public void OnCraftingButton(){
            onCraftingButton?.Invoke();
        }

        public event Action onHelpButton;
        public void OnHelpButton(){
            onHelpButton?.Invoke();
        }

        public event Action<bool> onCameraMovementSet;
        public void OnCameraMovementSet( bool enableCameraMovement ){
            onCameraMovementSet?.Invoke( enableCameraMovement );
        }

    }
}
