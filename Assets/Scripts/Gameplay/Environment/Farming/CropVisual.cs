using Hearthglade.Core.Farming;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // The look of one crop: one child per growth stage and optional fruit objects. Draws a PlotView, holds no state.
    public class CropVisual : MonoBehaviour {
        [ Tooltip( "One object per growth stage, in order; the last one is the mature plant." ) ]
        [ SerializeField ] GameObject[] stages = new GameObject[ 0 ];
        [ Tooltip( "Fruit shown on the mature plant, e.g. berries or apples. Hidden one by one as they are picked." ) ]
        [ SerializeField ] GameObject[] fruits = new GameObject[ 0 ];
        [ Tooltip( "A plant whose head always looks east (a sunflower): turned in Orient so that the head, which points at Head Yaw in the prefab, faces world +X." ) ]
        [ SerializeField ] bool facesEast;
        [ Tooltip( "Yaw in degrees (Unity, 0 = +Z) the head points to in the prefab." ) ]
        [ SerializeField ] float headYaw;

        // World +X is east (grid x grows to the east, see BlockModel).
        public const float EastYaw = 90f;

        public void Orient() {
            if( facesEast ) {
                transform.rotation = Quaternion.Euler( 0f, EastYaw - headYaw, 0f );
            }
        }

        public void Apply( PlotView view ) {
            int shownStage = Mathf.Clamp( view.Stage, 0, Mathf.Max( 0, stages.Length - 1 ) );
            for( int i = 0; i < stages.Length; i++ ) {
                if( stages[ i ] != null ) {
                    stages[ i ].SetActive( i == shownStage );
                }
            }

            int fruitsShown = view.MaxYield <= 0 ? 0 : Mathf.CeilToInt( view.ReadyYield * fruits.Length / ( float ) view.MaxYield );
            for( int i = 0; i < fruits.Length; i++ ) {
                if( fruits[ i ] != null ) {
                    fruits[ i ].SetActive( i < fruitsShown );
                }
            }
        }
    }
}
