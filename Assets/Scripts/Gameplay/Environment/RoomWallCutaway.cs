using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthglade.Gameplay.Environment
{
    // Dollhouse cutaway: while the player stands inside a room, the wall and door pieces between the camera and
    // that room fade out, so the interior isn't lost behind its own camera-facing walls, and fade back in on
    // the way out. Pure view - PlayerController owns the room-shape flood-fill and decides when to call this.
    //
    // Which pieces those are comes from BuildEdgeGrid's own records rather than from anything per-pixel: the
    // grid knows exactly which cell edge each placed piece occupies, so "is this one of the room's
    // camera-facing walls" is an exact question about the two cells that edge separates. The fade itself is the
    // only part the shader does (BuildingPieceFade.shader, one number per renderer), because a fragment can't
    // know which piece it came from - at a corner a near wall's post and the far wall's end piece occupy the
    // same space with the same facing, so no per-pixel rule can tell them apart.
    public class RoomWallCutaway
    {
        // Shared with RoomDarknessOverlay: the walls thinning out and the room's light coming up are one
        // movement to the eye, so they run off one number rather than two that have to be kept in step.
        public const float FadeSeconds = 0.4f;

        private static readonly int CutawayFadeId = Shader.PropertyToID( "_CutawayFade" );

        // 1 = solid, 0 = gone. A piece is tracked from the moment it starts fading out until it is solid again.
        private sealed class Piece
        {
            public GameObject Root;
            public Renderer[] Renderers;
            public ShadowCastingMode[] ShadowModes;
            public float Fade = 1f;
            public float Target;
        }

        // A plain list, not a dictionary keyed by the piece: a destroyed GameObject makes an unreliable key,
        // and a room's worth of walls is short enough that looking one up by scanning costs nothing.
        private readonly List<Piece> pieces = new ();
        private readonly MaterialPropertyBlock propertyBlock = new ();
        private readonly HashSet<(int x, int z)> roomCells = new ();
        private readonly HashSet<(int x, int z)> visitedBlocks = new ();
        private readonly HashSet<GameObject> cutThisTick = new ();

        // Points the room's camera-facing walls at "gone" and everything else this is still tracking back at
        // "solid", so a piece that has dropped out of the cutaway fades back in rather than sticking.
        public void Apply( Map.Map map, List<(int x, int z)> cells, Vector3 cameraForward ) {
            if( map?.EdgeGrid == null ) {
                FadeIn();
                return;
            }

            roomCells.Clear();
            foreach( var cell in cells ) {
                roomCells.Add( cell );
            }

            // Stepping across a wall into the room along this axis means stepping away from the camera, which
            // is what makes that wall one standing in front of the room rather than behind it.
            int awayX = cameraForward.x >= 0f ? 1 : -1;
            int awayZ = cameraForward.z >= 0f ? 1 : -1;

            // A piece is filed under whichever block it was placed on, and a wall sits on a cell edge - so the
            // room's own walls can be filed one cell outside it, hence the margin.
            cutThisTick.Clear();
            visitedBlocks.Clear();
            foreach( var cell in cells ) {
                for( int dx = -1; dx <= 1; dx++ ) {
                    for( int dz = -1; dz <= 1; dz++ ) {
                        CollectPiecesFiledUnder( map, ( cell.x + dx, cell.z + dz ), awayX, awayZ );
                    }
                }
            }

            foreach( var piece in pieces ) {
                piece.Target = cutThisTick.Contains( piece.Root ) ? 0f : 1f;
            }
        }

        /// <summary>Fades everything back in - the player is no longer inside a room.</summary>
        public void FadeIn() {
            foreach( var piece in pieces ) {
                piece.Target = 1f;
            }
        }

        /// <summary>Puts every piece back solid at once, with no fade - for leaving the map behind entirely.</summary>
        public void Reset() {
            foreach( var piece in pieces ) {
                piece.Fade = 1f;
                PushToRenderers( piece );
            }
            pieces.Clear();
        }

        // Backwards, so a piece that has finished can be dropped as we go. A piece stops being tracked once it
        // is solid again, so this idles at zero cost whenever the player is not in a room.
        public void Tick( float deltaTime ) {
            float step = deltaTime / FadeSeconds;
            for( int i = pieces.Count - 1; i >= 0; i-- ) {
                var piece = pieces[ i ];

                // Despawned, or its chunk was hidden: hand the renderers back as they were, so nothing carries
                // a half-faded state into whatever the pool gives this instance to be next.
                if( piece.Root == null || !piece.Root.activeInHierarchy ) {
                    piece.Fade = 1f;
                    PushToRenderers( piece );
                    pieces.RemoveAt( i );
                    continue;
                }

                piece.Fade = Mathf.MoveTowards( piece.Fade, piece.Target, step );
                PushToRenderers( piece );
                if( piece.Fade >= 1f ) {
                    pieces.RemoveAt( i );
                }
            }
        }

        private void CollectPiecesFiledUnder( Map.Map map, (int x, int z) block, int awayX, int awayZ ) {
            if( !visitedBlocks.Add( block ) ) {
                return;
            }
            var model = map.GetModel( new SerializableVector2Int( block.x, block.z ) );
            if( model?.blockObjects == null ) {
                return;
            }
            foreach( var sceneObject in model.blockObjects.Values ) {
                if( !sceneObject.isSpawned || sceneObject.gameObject == null ) {
                    continue;
                }
                // Null for everything that isn't edge-anchored - furniture, trees, whatever else sits here.
                var box = map.EdgeGrid.BoxOf( sceneObject.GetHashCode() );
                if( box == null || box.Value.Kind != EdgeKind.Wall || !StandsBetweenCameraAndRoom( box.Value, awayX, awayZ ) ) {
                    continue;
                }
                cutThisTick.Add( sceneObject.gameObject );
                Track( sceneObject.gameObject );
            }
        }

        // An edge is named from its lower-coordinate cell (see BuildEdgeGrid), so it separates that cell from
        // the neighbour one step along its axis. The piece on it is a wall of this room when exactly one of the
        // two is a room cell, and it stands in front of the room when that cell is the one further from the
        // camera. The room's far walls have it on their camera side instead, and are left alone.
        private bool StandsBetweenCameraAndRoom( BuildEdgeBox box, int awayX, int awayZ ) {
            bool separatesAlongX = box.Side == EdgeSide.PlusX;
            var upperCell = separatesAlongX ? ( box.X + 1, box.Z ) : ( box.X, box.Z + 1 );

            bool lowerInRoom = roomCells.Contains( ( box.X, box.Z ) );
            bool upperInRoom = roomCells.Contains( upperCell );
            if( lowerInRoom == upperInRoom ) {
                return false;
            }
            return ( separatesAlongX ? awayX : awayZ ) > 0 ? upperInRoom : lowerInRoom;
        }

        private void Track( GameObject root ) {
            foreach( var tracked in pieces ) {
                if( tracked.Root == root ) {
                    return;
                }
            }
            // Only the renderers that are on right now, so a piece hidden for some other reason stays hidden
            // and gets its own shadow setting back rather than this one's idea of it.
            var found = root.GetComponentsInChildren<Renderer>();
            var kept = new List<Renderer>( found.Length );
            var modes = new List<ShadowCastingMode>( found.Length );
            foreach( var pieceRenderer in found ) {
                if( pieceRenderer.enabled ) {
                    kept.Add( pieceRenderer );
                    modes.Add( pieceRenderer.shadowCastingMode );
                }
            }
            if( kept.Count == 0 ) {
                return;
            }
            pieces.Add( new Piece {
                Root = root,
                Renderers = kept.ToArray(),
                ShadowModes = modes.ToArray(),
            } );
        }

        private void PushToRenderers( Piece piece ) {
            bool solid = piece.Fade >= 1f;
            bool gone = piece.Fade <= 0f;
            for( int i = 0; i < piece.Renderers.Length; i++ ) {
                var pieceRenderer = piece.Renderers[ i ];
                if( pieceRenderer == null ) {
                    continue;
                }
                // Nothing left to draw at zero, and drawing it anyway would keep paying for a wall made
                // entirely of discarded pixels.
                pieceRenderer.enabled = !gone;
                // The shadow pass doesn't dither (see BuildingPieceFade.shader), so a half-faded wall would
                // otherwise throw a whole one.
                pieceRenderer.shadowCastingMode = solid ? piece.ShadowModes[ i ] : ShadowCastingMode.Off;

                pieceRenderer.GetPropertyBlock( propertyBlock );
                propertyBlock.SetFloat( CutawayFadeId, piece.Fade );
                pieceRenderer.SetPropertyBlock( propertyBlock );
            }
        }
    }
}
