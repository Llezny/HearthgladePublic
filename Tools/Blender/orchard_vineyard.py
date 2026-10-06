"""Orchard plot, trellis, grapevine stages, fruit and item icons for docs/FARMING_PLAN.md phase 1.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python orchard_vineyard.py
Exports one FBX per piece into EXPORT_DIR and renders the icons. The apple TREE is not made here: by the project's
tree rule (docs in memory: "only Pine looks good, base all trees on it") the Unity builder reuses the Oak mesh
(a recoloured Pine derivative) at three sizes, and only the fruit comes from this script.

Conventions as in building_wood_poc.py: metres, Z up, origin at the bottom centre, flat UV at a palette cell centre
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png), no material of its own in Unity.
OrchardPlot is the 2x2-cell bed centred on its own origin (the prefab offsets it onto its footprint); the trellis and
the vine stages run along X with their thickness along Y, like the fence.
"""
import math
import random

import bmesh
import bpy
from mathutils import Euler, Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Farming"
BUILDABLE_ICONS = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Buildable"
RESOURCE_ICONS = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Resources"
USABLE_ICONS = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Usable"
PREVIEW_PATH = "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/9f6ad552-079f-4eb9-a1f9-a86f0076c0a8/scratchpad/orchard_preview.png"

CELL = 0.3675
TRELLIS_H = 0.5

STEM = (0, 1)        # 513A31 dark brown
BARK_A = (2, 5)      # 947157 brown
BARK_B = (0, 2)      # 9D7C68 tan brown
CUT_WOOD = (0, 3)    # D3B396 light tan
SOIL = (5, 4)        # 7A3B22 soil brown
LEAF_LIGHT = (5, 6)  # 7FBF6A
LEAF_DARK = (5, 7)   # 2F6B3A
LEAF_MID = (4, 1)    # 468450
GRAPE = (3, 3)       # 820070
GRAPE_LIGHT = (3, 4) # C47BC4
APPLE_RED = (6, 0)   # E5484D
SACK = (0, 3)
ROPE = (5, 4)
WHITE = (3, 0)       # FFF9EF


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


def add_box(bm, center, size, cell, rot=(0.0, 0.0, 0.0)):
    """Box with full extents `size`, rotated by Euler `rot` about its own centre. Returns [(face, cell)]."""
    sx, sy, sz = (s / 2 for s in size)
    euler = Euler(rot, "XYZ")
    verts = []
    for dz in (-1, 1):
        for dy in (-1, 1):
            for dx in (-1, 1):
                p = Vector((dx * sx, dy * sy, dz * sz))
                p.rotate(euler)
                verts.append(bm.verts.new(Vector(center) + p))
    faces_idx = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    result = []
    for idx in faces_idx:
        face = bm.faces.new([verts[i] for i in idx])
        face.normal_update()
        if face.normal.dot(face.calc_center_median() - Vector(center)) < 0:
            face.normal_flip()
        result.append((face, cell))
    return result


def add_blob(bm, center, radius, cells, scale=(1.0, 1.0, 1.0), subdiv=1, jitter=0.0, seed=0):
    """A small icosphere (flat shaded); faces alternate between the given palette cells."""
    rng = random.Random(seed)
    before = set(bm.verts)
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    new_verts = [v for v in bm.verts if v not in before]
    for v in new_verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(center)
        if jitter:
            v.co += Vector((rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter)))
    result = []
    faces = {f for v in new_verts for f in v.link_faces}
    for i, face in enumerate(sorted(faces, key=lambda f: f.calc_center_median().z)):
        result.append((face, cells[i % len(cells)]))
    return result


def finish(name, bm, coloured, budget, location=(0.0, 0.0, 0.0)):
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
    obj.location = location
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "dims", tuple(round(v, 4) for v in obj.dimensions))
    assert tris <= budget, f"{name}: {tris} triangles"
    return obj


# ---- orchard plot: a 2x2-cell bed with a log border ------------------------------------------------------------------

def build_orchard():
    size = 2 * CELL
    bm = bmesh.new()
    coloured = []
    soil = size - 0.06
    coloured += add_box(bm, (0, 0, 0.018), (soil, soil, 0.036), SOIL)
    log = 0.05
    for i, sign in enumerate((-1, 1)):
        offset = sign * (size / 2 - log / 2)
        coloured += add_box(bm, (0, offset, 0.025), (size, log, log), BARK_A if i == 0 else BARK_B)
        coloured += add_box(bm, (offset, 0, 0.045), (log, size - 2 * log, log * 0.9), BARK_B if i == 0 else BARK_A)
    return finish("OrchardPlot", bm, coloured, 120)


# ---- trellis: two posts, rails and slats ------------------------------------------------------------------------------

def build_trellis():
    bm = bmesh.new()
    coloured = []
    post = 0.035
    thickness = 0.04
    for sx in (-1, 1):
        coloured += add_box(bm, (sx * (CELL / 2 - post / 2), 0, TRELLIS_H / 2), (post, thickness, TRELLIS_H), STEM)
    inner = CELL - 2 * post + 0.006
    for frac in (0.18, 0.5, 0.86):
        coloured += add_box(bm, (0, 0, TRELLIS_H * frac), (inner, thickness * 0.55, 0.026), BARK_A)
    for x in (-0.07, 0.0, 0.07):
        coloured += add_box(bm, (x, 0, TRELLIS_H * 0.5), (0.014, thickness * 0.4, TRELLIS_H * 0.8), CUT_WOOD)
    return finish("Trellis", bm, coloured, 110)


# ---- vine stages: stems run through the lattice, leaves poke out on both sides ------------------------------------

VINE_Y = 0.028


def leaf(bm, x, z, size, cell, tilt):
    return add_box(bm, (x, VINE_Y * tilt, z), (size, 0.006, size * 0.85), cell, rot=(0.0, math.radians(tilt * 14), math.radians(tilt * 22)))


def build_vine(stage):
    bm = bmesh.new()
    coloured = []
    if stage == 0:
        coloured += add_box(bm, (0, 0, 0.05), (0.014, 0.014, 0.1), LEAF_DARK)
        coloured += leaf(bm, -0.025, 0.09, 0.05, LEAF_LIGHT, 1)
        coloured += leaf(bm, 0.025, 0.07, 0.05, LEAF_MID, -1)
        return finish("VineStage0", bm, coloured, 60)
    if stage == 1:
        for z, x in ((0.07, 0.0), (0.19, 0.012), (0.3, -0.008)):
            coloured += add_box(bm, (x, 0, z), (0.014, 0.014, 0.13), LEAF_DARK)
        for x, z, c, t in ((-0.05, 0.1, LEAF_LIGHT, 1), (0.05, 0.16, LEAF_MID, -1), (-0.055, 0.23, LEAF_LIGHT, -1),
                           (0.05, 0.29, LEAF_MID, 1), (0.0, 0.35, LEAF_LIGHT, 1), (-0.04, 0.33, LEAF_MID, -1)):
            coloured += leaf(bm, x, z, 0.075, c, t)
        return finish("VineStage1", bm, coloured, 130)
    for x in (-0.115, 0.0, 0.115):
        coloured += add_box(bm, (x, 0, 0.22), (0.014, 0.014, 0.44), LEAF_DARK)
    spots = [(-0.115, 0.1), (-0.07, 0.2), (-0.13, 0.3), (-0.07, 0.4), (0.0, 0.13), (0.045, 0.25), (-0.015, 0.36),
             (0.05, 0.44), (0.115, 0.08), (0.075, 0.19), (0.135, 0.32), (0.08, 0.4), (0.0, 0.47), (-0.1, 0.46)]
    for i, (x, z) in enumerate(spots):
        coloured += leaf(bm, x, z, 0.095, (LEAF_LIGHT, LEAF_MID, LEAF_DARK)[i % 3], 1 if i % 2 else -1)
    return finish("VineStage2", bm, coloured, 230)


def build_grape_bunch():
    """One bunch, origin at the top where it hangs from the vine; the Unity builder places several."""
    bm = bmesh.new()
    coloured = []
    coloured += add_box(bm, (0, 0, -0.01), (0.008, 0.008, 0.03), LEAF_DARK)
    coloured += add_blob(bm, (0, 0, -0.045), 0.026, (GRAPE, GRAPE_LIGHT), scale=(1.0, 1.0, 1.35), subdiv=1, jitter=0.002, seed=3)
    coloured += add_blob(bm, (0, 0, -0.085), 0.016, (GRAPE, GRAPE_LIGHT), scale=(1.0, 1.0, 1.3), subdiv=0, seed=5)
    return finish("GrapeBunch", bm, coloured, 130)


def build_wild_vine():
    """A wild grapevine: two leaning sticks, leaves and a few bunches (the gatherable WildGrapes world object)."""
    bm = bmesh.new()
    coloured = []
    height = 0.46
    rng = random.Random(11)
    for sign in (-1, 1):
        base = Vector((sign * 0.12, 0.0, 0.0))
        top = Vector((-sign * 0.01, 0.0, height))
        mid = (base + top) / 2
        length = (top - base).length
        angle = math.atan2(top.x - base.x, top.z - base.z)
        coloured += add_box(bm, mid, (0.022, 0.022, length), BARK_A if sign < 0 else BARK_B, rot=(0.0, angle, 0.0))
        for i in range(5):
            t = 0.18 + i * 0.17
            point = base + (top - base) * t
            side = 0.03 if (i + (sign > 0)) % 2 else -0.03
            tilt = 1 if side > 0 else -1
            coloured += add_box(bm, (point.x, point.y + side, point.z + rng.uniform(-0.01, 0.01)), (0.085, 0.007, 0.07),
                                (LEAF_LIGHT, LEAF_MID, LEAF_DARK)[(i + sign) % 3],
                                rot=(0.0, math.radians(tilt * 14), math.radians(tilt * 24 + rng.uniform(-10, 10))))
    for x, z, y in ((-0.05, 0.27, 0.05), (0.04, 0.2, -0.05)):
        coloured += add_blob(bm, (x, y, z), 0.03, (GRAPE, GRAPE_LIGHT), scale=(1.0, 1.0, 1.4), subdiv=1, jitter=0.002, seed=int(z * 100))
    return finish("WildVine", bm, coloured, 380)

def build_apple_fruit():
    bm = bmesh.new()
    coloured = add_blob(bm, (0, 0, 0.0), 0.03, (APPLE_RED, APPLE_RED, (6, 0)), subdiv=0)
    coloured += add_box(bm, (0, 0, 0.032), (0.006, 0.006, 0.014), STEM)
    return finish("AppleFruit", bm, coloured, 60)


# ---- icon-only models ---------------------------------------------------------------------------------------------

def build_sapling_icon():
    bm = bmesh.new()
    coloured = []
    coloured += add_blob(bm, (0, 0, 0.012), 0.06, (SOIL, STEM), scale=(1.0, 1.0, 0.45), subdiv=1)
    coloured += add_box(bm, (0, 0, 0.1), (0.016, 0.016, 0.18), BARK_A)
    coloured += add_box(bm, (0.02, 0, 0.19), (0.07, 0.012, 0.05), LEAF_LIGHT, rot=(0, math.radians(-25), 0))
    coloured += add_box(bm, (-0.02, 0, 0.17), (0.07, 0.012, 0.05), LEAF_MID, rot=(0, math.radians(25), 0))
    coloured += add_box(bm, (0.0, 0.0, 0.215), (0.05, 0.05, 0.012), LEAF_LIGHT, rot=(0, 0, math.radians(45)))
    coloured += add_box(bm, (0.035, 0.0, 0.05), (0.008, 0.008, 0.14), STEM, rot=(0, math.radians(10), 0))
    return finish("AppleSaplingIcon", bm, coloured, 200)


def build_cutting_icon():
    bm = bmesh.new()
    coloured = []
    coloured += add_box(bm, (-0.02, 0, 0.09), (0.018, 0.018, 0.2), BARK_A, rot=(0, math.radians(14), 0))
    coloured += add_box(bm, (0.035, 0, 0.085), (0.016, 0.016, 0.18), BARK_B, rot=(0, math.radians(-16), 0))
    coloured += add_box(bm, (-0.01, 0, 0.012), (0.07, 0.03, 0.02), ROPE, rot=(0, 0, math.radians(8)))
    for x, z, c, t in ((-0.07, 0.17, LEAF_LIGHT, 1), (0.01, 0.2, LEAF_MID, -1), (0.07, 0.15, LEAF_LIGHT, 1), (0.015, 0.1, LEAF_DARK, -1)):
        coloured += add_box(bm, (x, 0, z), (0.07, 0.07, 0.008), c, rot=(math.radians(t * 12), 0, math.radians(t * 30)))
    return finish("GrapeCuttingIcon", bm, coloured, 120)


def build_grapes_icon():
    bm = bmesh.new()
    coloured = []
    rows = [(0.0, 0.14, 1), (0.0, 0.1, 2), (0.0, 0.06, 3), (0.0, 0.025, 2)]
    k = 0
    for _, z, count in rows:
        for i in range(count):
            x = (i - (count - 1) / 2) * 0.034
            coloured += add_blob(bm, (x, 0, z), 0.019, (WHITE, WHITE), subdiv=1, seed=k)
            k += 1
    coloured += add_box(bm, (0.0, 0, 0.175), (0.012, 0.012, 0.04), WHITE)
    coloured += add_box(bm, (0.04, 0, 0.17), (0.07, 0.06, 0.01), WHITE, rot=(0, 0, math.radians(30)))
    return finish("GrapesIcon", bm, coloured, 400)


# ---- export, icons, preview -------------------------------------------------------------------------------------

def export_fbx(obj, filepath):
    saved = obj.location.copy()
    obj.location = (0.0, 0.0, 0.0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=filepath, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    obj.location = saved
    print("exported", filepath)


def setup_render(flat_white=False):
    scene = bpy.context.scene
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


def render(objects, path, width=512, height=512, fill=1.0, direction=(0.263, -0.337, 0.139)):
    scene = bpy.context.scene
    corners = [obj.matrix_world @ Vector(c) for obj in objects for c in obj.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    centre = (low + high) / 2
    size = max(high.x - low.x, high.y - low.y, high.z - low.z)
    camera = bpy.data.objects.new("IconCamera", bpy.data.cameras.new("IconCamera"))
    scene.collection.objects.link(camera)
    camera.location = centre + Vector(direction).normalized() * (size / 0.2) * 0.80 * 0.75 * fill
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

orchard = build_orchard()
trellis = build_trellis()
vines = [build_vine(i) for i in range(3)]
bunch = build_grape_bunch()
wild_vine = build_wild_vine()
fruit = build_apple_fruit()
sapling = build_sapling_icon()
cutting = build_cutting_icon()
grapes = build_grapes_icon()

if bpy.app.background:
    import os
    os.makedirs(EXPORT_DIR, exist_ok=True)
    for obj in (orchard, trellis, bunch, fruit, wild_vine, *vines):
        export_fbx(obj, f"{EXPORT_DIR}/{obj.name}.fbx")

    setup_render()
    for obj_list, path in (([orchard], f"{BUILDABLE_ICONS}/OrchardPlotIcon.png"),
                           ([trellis, vines[2]], f"{BUILDABLE_ICONS}/TrellisIcon.png"),
                           ([sapling], f"{RESOURCE_ICONS}/AppleSapling.png"),
                           ([cutting], f"{RESOURCE_ICONS}/GrapeCutting.png")):
        hide_all_but(obj_list)
        render(obj_list, path)

    # Grapes: a white glyph like the other food icons (apple.png, strawberry.png).
    scene = bpy.context.scene
    hide_all_but([grapes])
    saved = scene.display.shading.show_object_outline
    scene.display.shading.show_object_outline = False
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (1.0, 1.0, 1.0)
    render([grapes], f"{USABLE_ICONS}/grapes.png", fill=1.0)
    scene.display.shading.show_object_outline = saved
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"

    # Preview: orchard, trellis with each vine stage, a bunch and fruit.
    for obj in bpy.data.objects:
        obj.hide_render = obj in (sapling, cutting, grapes)
    orchard.location = (-0.6, 0.0, 0.0)
    trellis.location = (0.0, 0.0, 0.0)
    for i, vine in enumerate(vines):
        vine.location = (i * 0.45 - 0.0, 0.0, 0.0)
        if i:
            trellis_copy = trellis.copy()
            trellis_copy.data = trellis.data
            bpy.context.scene.collection.objects.link(trellis_copy)
            trellis_copy.location = (i * 0.45, 0.0, 0.0)
    bunch.location = (0.0, -0.1, 0.2)
    fruit.location = (-0.4, -0.5, 0.0)
    render([obj for obj in bpy.data.objects if not obj.hide_render], PREVIEW_PATH, 1400, 800, fill=0.95)
