using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // A round badge that hovers over a world object to say "something can be done here". The frame is shared;
    // every activity (harvest, water, collect...) is just a different glyph sprite passed to SetGlyph.
    public class ActivityBadge : MonoBehaviour {

        [ SerializeField ] Transform visual = null;
        [ SerializeField ] SpriteRenderer glyphRenderer = null;
        [ Tooltip( "The glyph is scaled to fit a square of this size (frame-local units, before the visual's scale)." ) ]
        [ SerializeField ] float glyphBox = 2.6f;
        [ Tooltip( "Total bob range in metres; it never dips below the resting height." ) ]
        [ SerializeField ] float bobHeight = 0.005f;
        [ SerializeField ] float bobSpeed = 1.5f;

        private Camera mainCamera;
        private Vector3 visualBasePosition;

        public void SetGlyph( Sprite glyph ) {
            glyphRenderer.sprite = glyph;
            glyphRenderer.enabled = glyph != null;
            if( glyph == null ) {
                return;
            }
            var size = glyph.bounds.size;
            float scale = glyphBox / Mathf.Max( size.x, size.y );
            glyphRenderer.transform.localScale = new Vector3( scale, scale, 1f );
        }

        private void OnEnable() {
            visualBasePosition = visual.localPosition;
        }

        private void LateUpdate() {
            if( mainCamera == null ) {
                mainCamera = Camera.main;
                if( mainCamera == null ) {
                    return;
                }
            }
            transform.rotation = mainCamera.transform.rotation;
            var position = visualBasePosition;
            position.y += ( Mathf.Sin( Time.time * bobSpeed ) * 0.5f + 0.5f ) * bobHeight / visual.lossyScale.y;
            visual.localPosition = position;
        }
    }
}
