using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthglade.Gameplay.UI.Menu.Build
{
    // World-space POC grid overlay shown while placing a building piece (docs/BUILDING_SYSTEM_PLAN.md section
    // 12, phase 7 - pulled forward ahead of room detection per user request 2026-09-26). Two unlit meshes
    // parented under this transform: a line grid around the ghost's cell, and a highlight quad over the
    // piece's exact footprint coloured green/red for fits/doesn't-fit. No scene wiring needed - BuildingPlacer
    // creates one instance at runtime.
    public class BuildGridVisualizer : MonoBehaviour
    {
        private const int RADIUS_CELLS = 4;
        private const float EDGE_HIGHLIGHT_THICKNESS_FRACTION = 0.12f;
        private static readonly Color LineColor = new Color( 1f, 1f, 1f, 0.35f );
        private static readonly Color ValidColor = new Color( 0.3f, 1f, 0.3f, 0.45f );
        private static readonly Color InvalidColor = new Color( 1f, 0.3f, 0.3f, 0.45f );

        private Transform linesTransform;
        private MeshFilter linesFilter;
        private Transform highlightTransform;
        private Material highlightMaterial;

        private bool linesBuilt;
        private Vector2Int lastLinesCenter;

        public static BuildGridVisualizer Create() {
            var root = new GameObject( "BuildGridVisualizer" );
            return root.AddComponent<BuildGridVisualizer>();
        }

        void Awake() {
            var shader = Shader.Find( "Sprites/Default" );

            var linesGo = new GameObject( "GridLines" );
            linesGo.transform.SetParent( transform, false );
            linesTransform = linesGo.transform;
            linesFilter = linesGo.AddComponent<MeshFilter>();
            var linesRenderer = linesGo.AddComponent<MeshRenderer>();
            linesRenderer.shadowCastingMode = ShadowCastingMode.Off;
            linesRenderer.receiveShadows = false;
            linesRenderer.sharedMaterial = new Material( shader ) { color = LineColor };

            var highlightGo = new GameObject( "Highlight" );
            highlightGo.transform.SetParent( transform, false );
            highlightTransform = highlightGo.transform;
            var highlightFilter = highlightGo.AddComponent<MeshFilter>();
            var highlightRenderer = highlightGo.AddComponent<MeshRenderer>();
            highlightRenderer.shadowCastingMode = ShadowCastingMode.Off;
            highlightRenderer.receiveShadows = false;
            highlightMaterial = new Material( shader );
            highlightRenderer.sharedMaterial = highlightMaterial;
            highlightFilter.sharedMesh = BuildQuadMesh();

            gameObject.SetActive( false );
        }

        public void Show() {
            gameObject.SetActive( true );
            linesBuilt = false;
        }

        public void Hide() {
            gameObject.SetActive( false );
        }

        // centerCell/y place the line grid (rebuilt only when the ghost moves to a new cell or support
        // layer); boxOriginCell/boxSizeCells/fits position and colour the footprint highlight every call.
        public void UpdateGrid( Vector2Int centerCell, int y, Vector2Int boxOriginCell, Vector2Int boxSizeCells, bool fits ) {
            float cellSize = MapGenerator.TILE_X_OFFSET;
            float worldY = RebuildLines( centerCell, y );

            var boxOrigin = MapHelper.GridToWorldPosition( new Vector2( boxOriginCell.x, boxOriginCell.y ) );
            float halfX = boxSizeCells.x * cellSize * 0.5f;
            float halfZ = boxSizeCells.y * cellSize * 0.5f;
            // boxOrigin is the anchor cell's own centre (BuildCellBox anchors at its lowest corner); shift by
            // half a cell so the quad spans boxSizeCells cells starting at that corner, not just the anchor cell.
            highlightTransform.position = new Vector3(
                boxOrigin.x - cellSize * 0.5f + halfX,
                worldY + 0.015f,
                boxOrigin.z - cellSize * 0.5f + halfZ );
            highlightTransform.localScale = new Vector3( boxSizeCells.x * cellSize, 1f, boxSizeCells.y * cellSize );
            highlightMaterial.color = fits ? ValidColor : InvalidColor;
        }

        // Walls/doors are edge-anchored (BuildEdgeGrid): the highlight is a thin strip along the shared
        // boundary of the two cells instead of a full-cell quad, world-axis-aligned like the cell one above
        // (the quad mesh is never rotated - only its XZ scale changes which axis is the long one).
        public void UpdateEdgeHighlight( int x, int z, EdgeSide side, int y, bool fits ) {
            float cellSize = MapGenerator.TILE_X_OFFSET;
            float worldY = RebuildLines( new Vector2Int( x, z ), y );

            var edgePos = MapHelper.EdgeToWorldPosition( x, z, side );
            highlightTransform.position = new Vector3( edgePos.x, worldY + 0.015f, edgePos.z );
            float thickness = cellSize * EDGE_HIGHLIGHT_THICKNESS_FRACTION;
            highlightTransform.localScale = side == EdgeSide.PlusX
                ? new Vector3( thickness, 1f, cellSize )
                : new Vector3( cellSize, 1f, thickness );
            highlightMaterial.color = fits ? ValidColor : InvalidColor;
        }

        // Rebuilds the line-grid mesh only when the ghost moved to a new cell or support layer; returns the
        // world Y both callers place their highlight at.
        private float RebuildLines( Vector2Int centerCell, int y ) {
            float worldY = y * MapGenerator.TILE_X_OFFSET;
            if( !linesBuilt || centerCell != lastLinesCenter ) {
                linesFilter.sharedMesh = BuildLinesMesh( MapGenerator.TILE_X_OFFSET );
                lastLinesCenter = centerCell;
                linesBuilt = true;
            }
            var linesOrigin = MapHelper.GridToWorldPosition( new Vector2( centerCell.x, centerCell.y ) );
            linesTransform.position = new Vector3( linesOrigin.x, worldY + 0.01f, linesOrigin.z );
            return worldY;
        }

        private static Mesh BuildLinesMesh( float cellSize ) {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float extent = ( RADIUS_CELLS + 0.5f ) * cellSize;

            for( int i = -RADIUS_CELLS; i <= RADIUS_CELLS + 1; i++ ) {
                float offset = ( i - 0.5f ) * cellSize;

                indices.Add( vertices.Count ); vertices.Add( new Vector3( offset, 0f, -extent ) );
                indices.Add( vertices.Count ); vertices.Add( new Vector3( offset, 0f, extent ) );

                indices.Add( vertices.Count ); vertices.Add( new Vector3( -extent, 0f, offset ) );
                indices.Add( vertices.Count ); vertices.Add( new Vector3( extent, 0f, offset ) );
            }

            var colors = new Color[ vertices.Count ];
            for( int i = 0; i < colors.Length; i++ ) {
                colors[ i ] = Color.white;
            }

            var mesh = new Mesh { name = "BuildGridLines" };
            mesh.SetVertices( vertices );
            mesh.SetColors( colors );
            mesh.SetIndices( indices.ToArray(), MeshTopology.Lines, 0 );
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildQuadMesh() {
            var mesh = new Mesh { name = "BuildGridHighlight" };
            mesh.SetVertices( new List<Vector3> {
                new Vector3( -0.5f, 0f, -0.5f ),
                new Vector3( 0.5f, 0f, -0.5f ),
                new Vector3( 0.5f, 0f, 0.5f ),
                new Vector3( -0.5f, 0f, 0.5f ),
            } );
            mesh.SetColors( new[] { Color.white, Color.white, Color.white, Color.white } );
            mesh.SetTriangles( new[] { 0, 2, 1, 0, 3, 2 }, 0 );
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
