using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.Player.Controller
{
    [ System.Serializable ]
    public class MovementTargetIndicator {

        public MovementTargetIndicator( Image indicatorImage ) {
            this.indicatorImage = indicatorImage;
        }

        public void MoveTo( Vector3 pos ) {
            indicatorImage.transform.position = new Vector3( pos.x, 0.13f, pos.z );
        }

        public Image indicatorImage;
    
    }
}
