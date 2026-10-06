"""WoodenFloor: a single 1x1 (footprint) floor tile for the building system rebuild
(docs/BUILDING_SYSTEM_PLAN.md, section 9 / Phase 8f). Cell-anchored like furniture, not edge-anchored like
the wall/door (building_wood_poc.py) - its footprint is exactly one grid cell (MapGenerator.TILE_X_OFFSET =
0.3675m) on both X and Z, origin at the bottom centre of that footprint (same convention as every other
cell-anchored piece: BuildingPlacer positions it at the cell's centre, ground height).

Modelled as 3 parallel plank strips over a thin dark backing plate, not 3 boxes with an open physical gap
straight through to whatever is below - the backing shows through the narrow seams as a shadow line (reads
as "small gaps, like parquet") without ever exposing bare ground/terrain through the tile.

Planks run full height (ground to PLANK_TOP) so the backing's colour is never exposed as a two-tone skirt
along a plank's own length (earlier version: planks floated above the backing, which left the backing's
colour showing as a band around the whole tile - looked like a rug with a trimmed edge). But the planks do
NOT reach all the way to the cell's outer X edge: each outer edge gets a half-width GAP margin (EDGE_MARGIN)
the same way every internal seam does, so when two tiles sit side by side their two half-gaps add up to one
full-width gap - visually identical to an internal seam. Without that margin, the top-down "plank, gap,
plank, gap, plank" rhythm would break at every tile boundary (two edge planks butting with no gap, reading
as one double-wide board there).

Run through the Blender MCP (execute_blender_code), paste into Blender's Scripting tab, or headless:
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python building_floor_poc.py
Units are metres, Z up, origin at the bottom centre. Colours come from the project palette
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png), same as building_wood_poc.py.
"""
import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment"
NAME = "WoodenFloor"
TRI_BUDGET = 80

CELL = 0.3675            # one building-grid cell footprint (MapGenerator.TILE_X_OFFSET), both X and Z
GAP = 0.012              # visible seam width between planks - and, halved, at each outer edge (see below)
PLANK_COUNT = 3
PLANK_WIDTH = CELL / PLANK_COUNT - GAP   # a full GAP is "spent" per plank: (COUNT-1) internal + 2 halves
EDGE_MARGIN = GAP / 2    # half-gap margin at each outer X edge, so two tiles' margins sum to one full gap
PLANK_HEIGHT = 0.03      # plank thickness (Blender Z -> Unity Y, "up")
BACKING_HEIGHT = 0.018   # thin backing plate under the seams, reads as the gap's shadow
PLANK_TOP = BACKING_HEIGHT + PLANK_HEIGHT - 0.003  # plank top height above ground; planks run ground-to-top

# Palette cells (row, col), same palette as building_wood_poc.py/world_props.py.
STEM = (0, 1)      # 513A31 dark brown - backing plate (the "gap" shadow)
CUT_WOOD = (0, 3)  # D3B396 light tan - plank faces


def cell_uv(cell, size=32, px=4):
    row, col = cell
    return ((col * px + px / 2) / size, 1.0 - (row * px + px / 2) / size)


def palette_material():
    mat = bpy.data.materials.get("palette")
    if mat is not None:
        return mat
    mat = bpy.data.materials.new("palette")
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(PALETTE, check_existing=True)
    tex.interpolation = "Closest"
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def add_box(bm, center, size, cell):
    """An axis-aligned box (center = Vector, size = (x, y, z) full extents). Returns [(face, cell)]."""
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    verts = [
        bm.verts.new((cx + dx * sx, cy + dy * sy, cz + dz * sz))
        for dz in (-1, 1) for dy in (-1, 1) for dx in (-1, 1)
    ]
    faces_idx = [
        (0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3),
    ]
    result = []
    for idx in faces_idx:
        face = bm.faces.new([verts[i] for i in idx])
        face.normal_update()
        face_out(face, Vector(center))
        result.append((face, cell))
    return result


def face_out(face, ref):
    face.normal_update()
    if face.normal.dot(face.calc_center_median() - ref) < 0:
        face.normal_flip()


def finish(name, bm, coloured):
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face, cell in coloured:
        u, v = cell_uv(cell)
        for loop in face.loops:
            loop[uv_layer].uv = (u, v)
        face.smooth = False
    mesh = bpy.data.meshes.new(name)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(palette_material())
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= TRI_BUDGET, f"{name}: {tris} triangles"
    return obj


def build_wooden_floor():
    bm = bmesh.new()
    coloured = []
    # Backing plate: the full footprint, full Z depth - thin and dark, shows through every seam (the
    # internal ones AND the half-gap margin at each outer X edge) as their "gap" shadow, without ever
    # exposing bare ground/terrain through the tile.
    coloured += add_box(bm, (0, 0, BACKING_HEIGHT / 2), (CELL, CELL, BACKING_HEIGHT), STEM)
    # Three plank strips, full depth AND full height (ground to PLANK_TOP - no floating skirt), inset
    # EDGE_MARGIN from each outer X edge and separated by a full GAP internally, so the seam rhythm
    # ("plank, gap, plank, gap, plank") continues unbroken across a tile boundary instead of two edge
    # planks butting together with no gap there.
    for i in range(PLANK_COUNT):
        left_edge = -CELL / 2 + EDGE_MARGIN + i * (PLANK_WIDTH + GAP)
        cx = left_edge + PLANK_WIDTH / 2
        coloured += add_box(bm, (cx, 0, PLANK_TOP / 2), (PLANK_WIDTH, CELL, PLANK_TOP), CUT_WOOD)
    return finish(NAME, bm, coloured)


def export_fbx(obj, filepath):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=filepath, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    print("exported", filepath)


old = bpy.data.objects.get(NAME)
if old is not None:
    bpy.data.objects.remove(old, do_unlink=True)
cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

floor_obj = build_wooden_floor()

if __name__ == "__main__" and bpy.app.background:
    export_fbx(floor_obj, EXPORT_DIR + "/WoodenFloor.fbx")
