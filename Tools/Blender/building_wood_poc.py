"""Low-poly building POC pieces for the building system rebuild (docs/BUILDING_SYSTEM_PLAN.md, phase 0):
WoodenWall, WoodenDoorFrame and WoodenDoorLeaf. All three occupy the same 1x1 (footprint) x 2 (height)
cell bounding box on the planned building grid (cell = MapGenerator.TILE_X_OFFSET/TILE_Z_OFFSET =
0.3675m), so they slot into the same grid unit.

Run through the Blender MCP (execute_blender_code), paste into Blender's Scripting tab, or headless:
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python building_wood_poc.py
(headless mode also exports the FBX files, see EXPORT_DIR below). Units are metres, Z up, origin at
the bottom centre (mushroom/world_props convention). Colours come from the project palette
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): every face gets a flat UV at the centre of
one palette cell, no texture/material of their own needed in Unity.

The door used to be a single fused mesh ("modelled shut", open/close deferred to later gameplay logic -
see git history). Now that the gameplay logic exists (docs/BUILDING_SYSTEM_PLAN.md, door opening), the
leaf has to be its own object so it can rotate independently while the frame (posts + lintel) stays put
in the wall opening. WoodenDoorLeaf's origin sits at the hinge (the frame's left post inner edge, at the
opening's bottom), not the wall's bottom-centre like every other piece here - Unity parents the leaf under
the frame at that offset and rotates the leaf's own transform to open it.

Export one object at a time with:

    bpy.ops.export_scene.fbx(filepath=..., use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False)
"""
import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment"
NAMES = ["WoodenWall", "WoodenDoorFrame", "WoodenDoorLeaf"]
PREVIEW_X = {"WoodenWall": 0.0, "WoodenDoorFrame": 0.0, "WoodenDoorLeaf": 0.0}
TRI_BUDGET = 150

CELL = 0.3675          # one building-grid cell (MapGenerator.TILE_X_OFFSET)
# Width/height now fill the full logical cell box exactly (2026-09-26): the wall/door is edge-anchored
# (BuildEdgeGrid) and a piece narrower/shorter than its cell left a visible gap between straight-run
# segments and at 90-degree corners, even though the edge grid snapped their positions correctly. Only
# THICKNESS stays scaled down - that axis is a stylistic panel thickness, not something a neighbouring
# piece needs to meet flush, so there is no correctness reason to fill the whole cell depth with it (and
# doing so would turn the wall into a solid Minecraft-style block, which was explicitly not wanted).
THICKNESS_SCALE = 0.7  # user's original "30% smaller" request (2026-09-26), kept for thickness only now
WIDTH = CELL                    # footprint, fills the cell exactly
HEIGHT = 2 * CELL               # 2 cells tall, fills the cell exactly
THICKNESS = 0.06 * THICKNESS_SCALE
EMBED = 0.003           # posts/frame own the outer edge; panels/beams/lintel sink this far INTO
                         # them instead of ending flush, so no two faces share the exact same plane
                         # (that was the cause of the z-fighting/flicker the user reported)

# Palette cells (row, col), same palette as world_props.py.
STEM = (0, 1)      # 513A31 dark brown - frame/posts
BARK_A = (2, 5)    # 947157 brown - cross beams
BARK_B = (0, 2)    # 9D7C68 tan brown - plank sides
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
    # verts index: dz,dy,dx major->minor: 0=(-,-,-) 1=(-,-,+) 2=(-,+,-) 3=(-,+,+) 4=(+,-,-) 5=(+,-,+) 6=(+,+,-) 7=(+,+,+)
    faces_idx = [
        (0, 1, 3, 2),  # bottom
        (4, 6, 7, 5),  # top
        (0, 4, 5, 1),  # -y
        (2, 3, 7, 6),  # +y
        (0, 2, 6, 4),  # -x
        (1, 5, 7, 3),  # +x
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
    obj.location = (PREVIEW_X[name], 0.0, 0.0)
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= TRI_BUDGET, f"{name}: {tris} triangles"
    return obj


# ---- wooden wall --------------------------------------------------------------------------------------------------

def build_wooden_wall():
    bm = bmesh.new()
    coloured = []
    post_w = WIDTH * 0.12
    # Posts own the outer edge (x = +-WIDTH/2) for the full height; everything else stops short of
    # it and sinks EMBED into the post instead, so no face is coplanar with the post's outer face.
    panel_half_w = (WIDTH / 2 - post_w) + EMBED
    # Main panel (plank face), origin at bottom centre -> centre z = HEIGHT/2.
    panel_t = THICKNESS * 0.7
    coloured += add_box(bm, (0, 0, HEIGHT / 2), (panel_half_w * 2, panel_t, HEIGHT), CUT_WOOD)
    # Two horizontal cross beams, protruding slightly on both faces (thicker than the panel, so the
    # panel's own face is buried inside the beam where they overlap instead of coinciding with it).
    # Depth stops EMBED short of the post's own Y-extent on each face - using the full THICKNESS here
    # (same as the posts) made the beam's front/back faces exactly coplanar with the posts' faces in
    # the sliver where a beam crosses a post (beams reach into the post by EMBED, same as the panel),
    # which is the z-fighting on the wall's sides the user reported.
    beam_h = HEIGHT * 0.09
    beam_t = THICKNESS - 2 * EMBED
    for frac in (0.18, 0.82):
        coloured += add_box(bm, (0, 0, HEIGHT * frac), (panel_half_w * 2, beam_t, beam_h), BARK_A)
    # Four corner posts, full height, protruding slightly.
    for sx in (-1, 1):
        cx = sx * (WIDTH / 2 - post_w / 2)
        coloured += add_box(bm, (cx, 0, HEIGHT / 2), (post_w, THICKNESS, HEIGHT), STEM)
    return finish("WoodenWall", bm, coloured)


# ---- wooden door frame (static: posts + lintel, stays put when the leaf swings) ------------------------------------

POST_W = WIDTH * 0.14
LEAF_W = WIDTH - 2 * POST_W                  # opening width between the posts
HINGE_X = -(WIDTH / 2 - POST_W)               # left post's inner edge, in the frame's own (bottom-centre) space


def build_wooden_door_frame():
    bm = bmesh.new()
    coloured = []
    # Two side posts + a lintel, same footprint/height as the wall so it occupies the same cell.
    for sx in (-1, 1):
        cx = sx * (WIDTH / 2 - POST_W / 2)
        coloured += add_box(bm, (cx, 0, HEIGHT / 2), (POST_W, THICKNESS, HEIGHT), STEM)
    # Same recess trick as the wall: stop short of the posts' outer edge instead of spanning the
    # full width, so the lintel does not share a face with them where they overlap in Z.
    lintel_half_w = (WIDTH / 2 - POST_W) + EMBED
    lintel_h = HEIGHT * 0.1
    coloured += add_box(bm, (0, 0, HEIGHT - lintel_h / 2), (lintel_half_w * 2, THICKNESS, lintel_h), STEM)
    return finish("WoodenDoorFrame", bm, coloured)


# ---- wooden door leaf (hinged: rotates about its own origin, which sits at the hinge, not the wall's

def build_wooden_door_leaf():
    lintel_h = HEIGHT * 0.1
    leaf_h = HEIGHT - lintel_h
    leaf_t = THICKNESS * 0.55
    # Everything below is in hinge-relative space (local x=0 is the hinge): fills the opening, centred
    # in it (world x=0 in the frame's space), which is local x = -HINGE_X = LEAF_W / 2.
    leaf_cx = LEAF_W / 2

    bm = bmesh.new()
    coloured = []
    coloured += add_box(bm, (leaf_cx, 0, leaf_h / 2), (LEAF_W * 0.96, leaf_t, leaf_h * 0.97), BARK_B)
    # Two plank seams on the leaf for texture.
    seam_w = LEAF_W * 0.06
    for frac in (-0.22, 0.22):
        coloured += add_box(bm, (leaf_cx + LEAF_W * frac, 0, leaf_h / 2), (seam_w, THICKNESS * 0.6, leaf_h * 0.9), BARK_A)
    # Handle, near the free edge (away from the hinge).
    handle_x = leaf_cx + LEAF_W * 0.32
    coloured += add_box(bm, (handle_x, THICKNESS * 0.35, leaf_h * 0.5), (LEAF_W * 0.05, THICKNESS * 0.25, LEAF_W * 0.05), STEM)
    return finish("WoodenDoorLeaf", bm, coloured)


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


# ---- build everything -----------------------------------------------------------------------------------------------

for old in NAMES:
    obj = bpy.data.objects.get(old)
    if obj is not None:
        bpy.data.objects.remove(obj, do_unlink=True)
cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

wall_obj = build_wooden_wall()
frame_obj = build_wooden_door_frame()
leaf_obj = build_wooden_door_leaf()

if __name__ == "__main__" and bpy.app.background:
    export_fbx(wall_obj, EXPORT_DIR + "/WoodenWall.fbx")
    export_fbx(frame_obj, EXPORT_DIR + "/WoodenDoorFrame.fbx")
    export_fbx(leaf_obj, EXPORT_DIR + "/WoodenDoorLeaf.fbx")
