using DG.Tweening;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    public class ChestAnimation : MonoBehaviour {
        [ SerializeField ] Transform chestLidTranform = null;
        private bool isOpen = false;
        private Vector3 closedRotation = new Vector3( 0, 0, -103 );
        private Vector3 openRotation = Vector3.zero;
    
        public void ChangeState() {
            chestLidTranform.DOLocalRotate( isOpen ? closedRotation : openRotation, 0.5f );
            isOpen = !isOpen;
        }
    }
}
