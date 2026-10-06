using System.Collections.Generic;
using Hearthglade.Gameplay.Map;
using Unity.Collections;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    // "You're in home" darkening POC (docs/BUILDING_SYSTEM_PLAN.md phase 7), a URP full-screen post process
    // (Assets/Shaders/PostProcess/RoomDarkness.shader, wired into Assets/Settings/DefaultRendererData.asset via
    // the built-in FullScreenPassRendererFeature) instead of a world-space quad mesh - the mesh version only
    // darkened whatever intersected one fixed-height plane (so a tree's foliage above/below that height stayed
    // lit), and the plane's finite edge was visible from this game's angled camera. The post process
    // reconstructs world position per-pixel from the depth buffer, so it is height-independent (no Y-layer
    // concept at all any more) and has no edge geometry to see. Pure view - PlayerController owns the
    // room-shape flood-fill and decides which of the two calls to make; this class only uploads a small mask
    // texture (one texel per room cell) and eases the shader globals the material reads every frame.
    //
    // Cutting away the walls that stand between the camera and the room is a separate job, on the same tick -
    // see RoomWallCutaway, which hides whole pieces rather than touching anything this uploads.
    public class RoomDarknessOverlay
    {
        private static readonly int RoomMaskTexId = Shader.PropertyToID( "_RoomMaskTex" );
        private static readonly int RoomMaskOriginId = Shader.PropertyToID( "_RoomMaskOrigin" );
        private static readonly int RoomMaskSizeId = Shader.PropertyToID( "_RoomMaskSize" );
        private static readonly int RoomCellSizeId = Shader.PropertyToID( "_RoomCellSize" );
        private static readonly int RoomInvertId = Shader.PropertyToID( "_RoomInvert" );
        private static readonly int RoomActiveId = Shader.PropertyToID( "_RoomActive" );

        private Texture2D maskTexture;
        private List<(int x, int z)> maskCells; // the list the texture was last built from
        private float active, activeTarget;
        private float invert, invertTarget;

        // Seen from outside: darken exactly the room's own cells - reads as "there's a roof".
        public void ShowRoof( List<(int x, int z)> roomCells ) {
            BuildMask( roomCells );
            FadeTo( 1f );
        }

        // Seen from inside: darken everything except the room's own cells - reads as "outside is dim".
        public void ShowOutsideDark( List<(int x, int z)> roomCells ) {
            BuildMask( roomCells );
            FadeTo( 0f );
        }

        public void Hide() {
            activeTarget = 0f;
        }

        /// <summary>Drops the darkening at once, with no fade - for leaving the map behind entirely.</summary>
        public void Reset() {
            active = activeTarget = 0f;
            maskCells = null;
            invert = invertTarget;
            Shader.SetGlobalFloat( RoomActiveId, 0f );
        }

        private void FadeTo( float targetView ) {
            invertTarget = targetView;
            activeTarget = 1f;
            // Nothing is on screen to cross from while the effect is off, so begin at the view being asked
            // for - otherwise coming back to a map would briefly blend in whichever one was last shown.
            if( active <= 0f ) {
                invert = targetView;
            }
        }

        // Called every frame, unlike the calls above, which come from the half-second room check. Walking in or
        // out of a doorway swaps the inside view for the roof one, and since those two darken opposite halves
        // of the picture, easing between them is what reads as the room lighting up or dimming rather than
        // snapping. Runs on the same clock as the wall cutaway so the walls and the light move together.
        public void Tick( float deltaTime ) {
            if( active == activeTarget && invert == invertTarget ) {
                return;
            }
            float step = deltaTime / RoomWallCutaway.FadeSeconds;
            active = Mathf.MoveTowards( active, activeTarget, step );
            invert = Mathf.MoveTowards( invert, invertTarget, step );
            Shader.SetGlobalFloat( RoomActiveId, active );
            Shader.SetGlobalFloat( RoomInvertId, invert );
        }

        // Rebuilt every call (every 0.5s tick, same as the room-enclosure check itself) - a room-sized texture
        // (capped by RoomEnclosureDetector's search limit) is cheap enough that tracking "did the shape
        // actually change" isn't worth the extra bookkeeping for a POC.
        private void BuildMask( List<(int x, int z)> roomCells ) {
            // PlayerController hands over the very same list while the room keeps its shape, and the globals
            // stay set between calls, so there is nothing to rebuild or upload.
            if( ReferenceEquals( roomCells, maskCells ) && maskTexture != null ) {
                return;
            }
            maskCells = roomCells;

            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach( var (x, z) in roomCells ) {
                minX = Mathf.Min( minX, x );
                maxX = Mathf.Max( maxX, x );
                minZ = Mathf.Min( minZ, z );
                maxZ = Mathf.Max( maxZ, z );
            }
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            if( maskTexture == null || maskTexture.width != width || maskTexture.height != height ) {
                if( maskTexture != null ) {
                    Object.Destroy( maskTexture );
                }
                maskTexture = new Texture2D( width, height, TextureFormat.R8, false, true ) {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "RoomDarknessMask",
                };
            }

            // Written straight into the texture's own memory (R8, one byte per cell): no managed pixel array.
            var pixels = maskTexture.GetRawTextureData<byte>();
            for( int i = 0; i < pixels.Length; i++ ) {
                pixels[ i ] = 0;
            }
            foreach( var (x, z) in roomCells ) {
                pixels[ ( z - minZ ) * width + ( x - minX ) ] = 255;
            }
            maskTexture.Apply( false, false );

            Shader.SetGlobalTexture( RoomMaskTexId, maskTexture );
            Shader.SetGlobalVector( RoomMaskOriginId, new Vector4( minX, minZ, 0f, 0f ) );
            Shader.SetGlobalVector( RoomMaskSizeId, new Vector4( width, height, 0f, 0f ) );
            Shader.SetGlobalFloat( RoomCellSizeId, MapGenerator.TILE_X_OFFSET );
        }
    }
}
