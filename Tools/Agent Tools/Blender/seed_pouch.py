"""Seed pouch icons for Hearthglade (CarrotSeed, StrawberrySeed, ...): a small burlap pouch with a coloured band.

Headless:  blender.exe -b --python "Tools/Agent Tools/Blender/seed_pouch.py"
Renders a 512x512 transparent icon per entry of POUCHES into OUT_DIR (the same Workbench look as render_icon.py).
Colours come from the project palette (Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png, 4x4 px cells, flat
UV at a cell centre), so the model needs no material of its own. Icon-only model: not exported to Unity.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
OUT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Resources"
SEG = 8

CELL_SACK = (0, 3)    # D3B396 light tan burlap
CELL_ROPE = (5, 4)    # 7A3B22 dark brown
CELL_SEEDS = (5, 1)   # F5D65C pale gold

# name -> palette cell of the band that tells the crop apart
POUCHES = {
    "CarrotSeed": (4, 3),       # C5721D orange
    "StrawberrySeed": (6, 0),   # E5484D red
    "RyeSeed": (1, 3),          # ADA66D straw
    "PotatoSeed": (6, 1),       # B5763F tan brown
    "CabbageSeed": (5, 6),      # 7FBF6A light green
    "LingonberrySeed": (4, 2),  # 9F3F3F dark red
    "RaspberrySeed": (3, 3),    # 820070 magenta
    "OysterSpawn": (5, 5),      # 7C8583 grey
    "PeaSeed": (4, 1),          # 468450 green
    "SunflowerSeed": (5, 0),    # E0A81E gold
    "CornSeed": (1, 7),         # F0F1B8 pale yellow
    "TomatoSeed": (1, 6),       # F5CFB7 peach
    "RiceSeed": (3, 0),         # FFF9EF cream
    "CranberrySeed": (6, 0),    # E5484D red
    "WatermelonSeed": (5, 7),   # 2F6B3A dark green
    "OatsSeed": (0, 4),         # BEB296 sand
    "TurnipSeed": (3, 4),       # C47BC4 lilac
    "BlueberrySeed": (2, 4),    # 30495D navy
    "BeanSeed": (2, 1),         # 60703C olive
    "FlaxSeed": (1, 5),         # 60A3B6 blue
    "CottonSeed": (4, 0),       # B8B8B8 light grey
    "ChiliSeed": (4, 2),        # 9F3F3F dark red
    "CattailSeed": (3, 1),      # 574A43 dark brown
}

# (radius, height) rings from the bottom up: base, belly, shoulder, neck (tied with rope), flared cuff
PROFILE = [(0.040, 0.000), (0.078, 0.030), (0.080, 0.070), (0.060, 0.100), (0.030, 0.118), (0.028, 0.132), (0.054, 0.152)]
TARGET_Z = 0.075
DISTANCE = 0.62
BASE_OFFSET = Vector((0.263, -0.337, 0.139))


def cell_uv(cell, size=32, px=4):
    row, col = cell
    return ((col * px + px / 2) / size, 1.0 - (row * px + px / 2) / size)


def build(name, band_cell):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings = []
    for k, (radius, z) in enumerate(PROFILE):
        ring = []
        for i in range(SEG):
            a = 2 * math.pi * i / SEG + (0.2 if k % 2 else 0.0)
            r = radius * (1.0 + 0.06 * math.sin(i * 2.1 + k))
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        rings.append(ring)

    faces = []
    cells = [CELL_SACK, band_cell, CELL_SACK, CELL_SACK, CELL_ROPE, CELL_SACK]
    for k, cell in enumerate(cells):
        lower, upper = rings[k], rings[k + 1]
        for i in range(SEG):
            j = (i + 1) % SEG
            faces.append((bm.faces.new([lower[i], lower[j], upper[j], upper[i]]), cell))
    # Seeds showing in the open cuff.
    centre = bm.verts.new((0.0, 0.0, PROFILE[-1][1] - 0.012))
    for i in range(SEG):
        j = (i + 1) % SEG
        faces.append((bm.faces.new([rings[-1][j], rings[-1][i], centre]), CELL_SEEDS))
    bottom = bm.verts.new((0.0, 0.0, 0.0))
    for i in range(SEG):
        j = (i + 1) % SEG
        faces.append((bm.faces.new([rings[0][i], rings[0][j], bottom]), CELL_SACK))

    bm.faces.ensure_lookup_table()
    bm.normal_update()
    middle = Vector((0.0, 0.0, 0.07))
    for index, (face, _cell) in enumerate(faces):
        c = face.calc_center_median()
        if index >= len(cells) * SEG and index < len(cells) * SEG + SEG:
            outward = face.normal.z > 0          # seeds face up
        else:
            outward = face.normal.dot(c - middle) > 0
        if not outward:
            face.normal_flip()

    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face, cell in faces:
        u, v = cell_uv(cell)
        for loop in face.loops:
            loop[uv_layer].uv = (u, v)
        face.smooth = False
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mat = bpy.data.materials.get("palette")
    if mat is None:
        mat = bpy.data.materials.new("palette")
        mat.use_nodes = True
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(PALETTE)
        tex.interpolation = "Closest"
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    obj.data.materials.append(mat)
    return obj


def render(obj, path):
    scene = bpy.context.scene
    render_settings = scene.render
    shading = scene.display.shading
    camera_data = bpy.data.cameras.new("IconCamera")
    camera = bpy.data.objects.new("IconCamera", camera_data)
    scene.collection.objects.link(camera)
    target = Vector((0.0, 0.0, TARGET_Z))
    camera.location = target + BASE_OFFSET * DISTANCE
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    render_settings.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    shading.object_outline_color = (0.10, 0.06, 0.04)
    render_settings.resolution_x = render_settings.resolution_y = 512
    render_settings.resolution_percentage = 100
    render_settings.film_transparent = True
    render_settings.image_settings.file_format = "PNG"
    render_settings.image_settings.color_mode = "RGBA"
    scene.camera = camera
    render_settings.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

for pouch_name, cell in POUCHES.items():
    if os.path.exists(f"{OUT_DIR}/{pouch_name}.png"):
        continue   # already rendered; delete the png to redo it
    pouch = build(pouch_name, cell)
    pouch.data.calc_loop_triangles()
    print(pouch_name, "tris", len(pouch.data.loop_triangles), "dims", tuple(round(v, 3) for v in pouch.dimensions))
    render(pouch, f"{OUT_DIR}/{pouch_name}.png")
    bpy.data.objects.remove(pouch, do_unlink=True)
