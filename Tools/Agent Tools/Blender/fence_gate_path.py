"""Low-poly garden pieces for docs/FARMING_PLAN.md phase 2: WoodenFence, WoodenFenceGateFrame, WoodenFenceGateLeaf and CobblePath.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python fence_gate_path.py
Exports one FBX per piece into EXPORT_DIR, renders the item icons into ICON_DIR and a preview into PREVIEW_PATH.
Same conventions as building_wood_poc.py / building_floor_poc.py: metres, Z up, origin at the bottom centre, flat UV
at a palette cell centre (no material of its own in Unity), a cell is MapGenerator.TILE_X_OFFSET = 0.3675 m.

The fence and the gate are edge pieces (BuildEdgeGrid, kind Fence): they run along X, thickness along Y, one cell wide
and one cell tall, and fill the whole cell width so a run of fences has no gaps. The gate leaf's origin sits at the
hinge (the frame's left post inner edge), like WoodenDoorLeaf. The cobble tile is a floor tile (BuildFloorGrid):
its height must stay Map.FloorTileHeight (0.047 m), because things standing on it rest at that height.
"""
import math

import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment"
ICON_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Buildable"
PREVIEW_PATH = "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/9f6ad552-079f-4eb9-a1f9-a86f0076c0a8/scratchpad/garden_preview.png"
TRI_BUDGET = 130

CELL = 0.3675
WIDTH = CELL
HEIGHT = CELL
THICKNESS = 0.04
EMBED = 0.003
POST_W = WIDTH * 0.1
FLOOR_HEIGHT = 0.047

STEM = (0, 1)        # 513A31 dark brown - posts
BARK_A = (2, 5)      # 947157 brown - rails
BARK_B = (0, 2)      # 9D7C68 tan brown - leaf pickets
CUT_WOOD = (0, 3)    # D3B396 light tan - pickets
SLAB = (0, 6)        # 444D4B dark grey - cobble backing
STONES = [(4, 0), (5, 2), (5, 5), (5, 3)]   # B8B8B8, A9B4BD, 7C8583, 6E7A85 greys


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


def add_box(bm, center, size, cell, yaw=0.0):
    """Axis-aligned box (rotated about its own vertical axis by yaw radians). Returns [(face, cell)]."""
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    cos_a, sin_a = math.cos(yaw), math.sin(yaw)
    verts = []
    for dz in (-1, 1):
        for dy in (-1, 1):
            for dx in (-1, 1):
                x, y = dx * sx, dy * sy
                verts.append(bm.verts.new((cx + x * cos_a - y * sin_a, cy + x * sin_a + y * cos_a, cz + dz * sz)))
    faces_idx = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    result = []
    for idx in faces_idx:
        face = bm.faces.new([verts[i] for i in idx])
        face.normal_update()
        if face.normal.dot(face.calc_center_median() - Vector(center)) < 0:
            face.normal_flip()
        result.append((face, cell))
    return result


def finish(name, bm, coloured, budget=TRI_BUDGET):
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
    print(name, "tris", tris, "dims", tuple(round(v, 4) for v in obj.dimensions))
    assert tris <= budget, f"{name}: {tris} triangles"
    return obj


# ---- fence --------------------------------------------------------------------------------------------------------

def build_fence():
    bm = bmesh.new()
    coloured = []
    for sx in (-1, 1):
        coloured += add_box(bm, (sx * (WIDTH / 2 - POST_W / 2), 0, HEIGHT / 2), (POST_W, THICKNESS, HEIGHT), STEM)
    inner = WIDTH - 2 * POST_W + 2 * EMBED
    rail_t = THICKNESS * 0.6
    for frac in (0.3, 0.72):
        coloured += add_box(bm, (0, 0, HEIGHT * frac), (inner, rail_t, HEIGHT * 0.1), BARK_A)
    picket_t = THICKNESS * 0.45
    count = 4
    span = WIDTH - 2 * POST_W
    for i in range(count):
        x = -span / 2 + span * (i + 0.5) / count
        coloured += add_box(bm, (x, 0, HEIGHT * 0.45), (WIDTH * 0.07, picket_t, HEIGHT * 0.88), CUT_WOOD)
    return finish("WoodenFence", bm, coloured)


# ---- gate ---------------------------------------------------------------------------------------------------------

GATE_POST_H = HEIGHT * 1.12
HINGE_X = -(WIDTH / 2 - POST_W)
LEAF_W = WIDTH - 2 * POST_W
LEAF_H = HEIGHT * 0.86


def build_gate_frame():
    bm = bmesh.new()
    coloured = []
    for sx in (-1, 1):
        coloured += add_box(bm, (sx * (WIDTH / 2 - POST_W / 2), 0, GATE_POST_H / 2), (POST_W, THICKNESS, GATE_POST_H), STEM)
    return finish("WoodenFenceGateFrame", bm, coloured)


def build_gate_leaf():
    bm = bmesh.new()
    coloured = []
    leaf_t = THICKNESS * 0.5
    z0 = HEIGHT * 0.05
    stile_w = LEAF_W * 0.1
    # Everything is in hinge space: local x = 0 is the hinge, the leaf runs towards +x.
    for x in (stile_w / 2, LEAF_W - stile_w / 2):
        coloured += add_box(bm, (x, 0, z0 + LEAF_H / 2), (stile_w, leaf_t, LEAF_H), BARK_B)
    inner = LEAF_W - 2 * stile_w + 2 * EMBED
    for frac in (0.22, 0.78):
        coloured += add_box(bm, (LEAF_W / 2, 0, z0 + LEAF_H * frac), (inner, leaf_t * 0.8, LEAF_H * 0.12), BARK_A)
    for x in (LEAF_W * 0.38, LEAF_W * 0.62):
        coloured += add_box(bm, (x, 0, z0 + LEAF_H / 2), (LEAF_W * 0.07, leaf_t * 0.7, LEAF_H * 0.9), CUT_WOOD)
    return finish("WoodenFenceGateLeaf", bm, coloured)


# ---- cobble path --------------------------------------------------------------------------------------------------

def build_cobble():
    bm = bmesh.new()
    coloured = []
    slab_h = 0.03
    coloured += add_box(bm, (0, 0, slab_h / 2), (CELL, CELL, slab_h), SLAB)
    cols = 3
    gap = 0.014
    size = (CELL - gap * (cols + 1)) / cols
    # (column offset, row offset) per row: a running bond, so it does not look like a grid of tiles.
    layout = [(0.0, -1), (0.012, 0), (-0.01, 1)]
    k = 0
    for shift, row in layout:
        for col in range(cols):
            cx = -CELL / 2 + gap + size / 2 + col * (size + gap) + shift * (col - 1) * 0.0 + shift
            cx = max(-CELL / 2 + size / 2 + 0.006, min(CELL / 2 - size / 2 - 0.006, cx))
            cy = row * (size + gap)
            top = FLOOR_HEIGHT - (0.0 if k % 4 == 0 else 0.004 * (k % 3))
            stone = STONES[(k * 3 + row) % len(STONES)]
            yaw = math.radians(((k * 37) % 17) - 8)
            sx = size * (0.9 + 0.1 * ((k * 5) % 3) / 2)
            coloured += add_box(bm, (cx, cy, top / 2), (sx, size * 0.92, top), stone, yaw)
            k += 1
    return finish("CobblePath", bm, coloured, budget=140)


# ---- export, icons, preview ---------------------------------------------------------------------------------------

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


def setup_render(scene):
    r = scene.render
    shading = scene.display.shading
    r.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    shading.object_outline_color = (0.10, 0.06, 0.04)
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    r.resolution_percentage = 100


def render(objects, path, width=512, height=512, fill=1.0):
    """Frames the objects' combined bounding box from the fixed icon angle and renders it."""
    scene = bpy.context.scene
    corners = [obj.matrix_world @ Vector(c) for obj in objects for c in obj.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    centre = (low + high) / 2
    size = max(high.x - low.x, high.y - low.y, high.z - low.z)
    camera = bpy.data.objects.new("IconCamera", bpy.data.cameras.new("IconCamera"))
    scene.collection.objects.link(camera)
    direction = Vector((0.263, -0.337, 0.139)).normalized()
    camera.location = centre + direction * (size / 0.2) * 0.80 * 0.75 * fill
    camera.rotation_euler = (centre - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def hide_all_but(objects):
    for obj in bpy.data.objects:
        obj.hide_render = obj not in objects


cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

fence = build_fence()
frame = build_gate_frame()
leaf = build_gate_leaf()
cobble = build_cobble()
frame.location = (1.0, 0.0, 0.0)
leaf.location = (1.0 + HINGE_X, 0.0, 0.0)
cobble.location = (0.0, -0.6, 0.0)
bpy.context.view_layer.update()

if bpy.app.background:
    for piece, name in ((fence, "WoodenFence"), (frame, "WoodenFenceGateFrame"), (leaf, "WoodenFenceGateLeaf"), (cobble, "CobblePath")):
        saved = piece.location.copy()
        piece.location = (0.0, 0.0, 0.0)
        export_fbx(piece, f"{EXPORT_DIR}/{name}.fbx")
        piece.location = saved

    setup_render(bpy.context.scene)
    for pieces, name in (([fence], "FenceIcon"), ([frame, leaf], "FenceGateIcon"), ([cobble], "CobblePathIcon")):
        hide_all_but(pieces)
        render(pieces, f"{ICON_DIR}/{name}.png")

    # A preview of the whole set: a run of two fences, a gate and a 2x2 patch of cobble.
    for obj in bpy.data.objects:
        obj.hide_render = False
    second = fence.copy()
    second.data = fence.data
    bpy.context.scene.collection.objects.link(second)
    second.location = (-CELL, 0.0, 0.0)
    fence.location = (0.0, 0.0, 0.0)
    frame.location = (CELL, 0.0, 0.0)
    leaf.location = (CELL + HINGE_X, 0.0, 0.0)
    leaf.rotation_euler = (0.0, 0.0, math.radians(-70))
    for i, (dx, dy) in enumerate(((0, 0), (1, 0), (0, 1), (1, 1))):
        piece = cobble if i == 0 else cobble.copy()
        if i:
            piece.data = cobble.data
            bpy.context.scene.collection.objects.link(piece)
        piece.location = (-0.2 + dx * CELL, -0.5 + dy * CELL, 0.0)
        piece.rotation_euler = (0.0, 0.0, math.radians(90 * i))
    render([fence, second, frame, leaf, cobble], PREVIEW_PATH, 1400, 800, fill=1.25)
