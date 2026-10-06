using DG.Tweening;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    public class DoorAnimation : MonoBehaviour {
        [ SerializeField ] Transform doorLeafTransform = null;
        // The leaf's own collider blocks the opening; disabling it while open is simpler and more robust
        // than relying on the swing angle to geometrically clear the passage.
        [ SerializeField ] Collider doorLeafCollider = null;
        private bool isOpen = false;
        [ SerializeField ] Vector3 closedRotation = new ( 0, 180, 0 );
        [ SerializeField ] Vector3 openRotation = new ( 0, 60, 0 );

        private void Awake( ) {
            // The prefab's baked-in leaf rotation is whatever the model builder left it at, which can drift
            // out of sync with closedRotation (that's what made the very first interaction swing from the
            // wrong pose instead of a clean closed->open arc). Snap to the real "closed" pose on spawn so
            // isOpen=false always matches what's actually on screen.
            doorLeafTransform.localRotation = Quaternion.Euler( closedRotation );
            doorLeafCollider.enabled = true;
        }

        /// <summary>Toggles open/closed and returns the new isOpen state, so Door can also free/reoccupy its
        /// EdgeGrid edge (Map.CanMoveTo doesn't know about the leaf's rotation or collider at all).</summary>
        public bool ChangeState() {
            isOpen = !isOpen;
            // Kill any tween still running from a previous click first - two DOLocalRotate calls stacked on
            // the same transform fight over every frame's value and can leave the leaf stuck mid-swing.
            doorLeafTransform.DOKill();
            doorLeafTransform.DORotate( isOpen ? openRotation : closedRotation, 0.4f );
            doorLeafCollider.enabled = !isOpen;
            return isOpen;
        }
    }
}
