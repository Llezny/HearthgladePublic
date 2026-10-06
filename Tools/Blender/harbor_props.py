"""Low-poly props of the port islands (docs/EXPLORATION_LOOP_PLAN.md, W4): stalls, houses, quay, boat, crates, lanterns, ...

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python harbor_props.py
Exports one FBX per prop into EXPORT_DIR and renders a contact sheet to PREVIEW_PATH.

Conventions as in world_props.py / fence_gate_path.py: metres, Z up, origin at the bottom centre, the front of a prop faces -Y,
flat UV at a palette cell centre (no material of its own in Unity). Scale reference: the player is 0.3 m tall, a map cell is
0.3675 m wide, a wall of a room is 0.74 m high, so a house here is 0.35 m to the eaves.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Euler, Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Harbor"
PREVIEW_PATH = "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/7177f04b-2b47-43b2-b2d6-46adc009a8bb/scratchpad/harbor_props.png"
TRI_BUDGET = 400
CELL = 0.3675

# Palette cells (row, col) from the top-left of the palette image.
BLACK = (0, 0)
WOOD_DARK = (0, 1)
WOOD_MID = (0, 2)
PLANK_TAN = (0, 3)
THATCH = (0, 4)
WATER_TEAL = (0, 5)
WALL_WHITE = (0, 7)
GREEN_L = (1, 0)
BLUE_L = (1, 5)
HAY = (1, 3)
SKIN = (1, 6)
BLUE_D = (2, 4)
PLANK_BROWN = (2, 5)
WALL_CREAM = (3, 0)
PURPLE = (3, 3)
PINK = (3, 4)
STONE = (4, 0)
GREEN = (4, 1)
RED = (4, 2)
ORANGE = (4, 3)
YELLOW = (5, 0)
YELLOW_L = (5, 1)
STONE_D = (5, 5)
ROOF_RUST = (5, 4)
ROOF_GREY = (5, 3)
RED_BRIGHT = (6, 0)


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


def face_out(face, ref):
    face.normal_update()
    if face.normal.dot(face.calc_center_median() - ref) < 0:
        face.normal_flip()


# ---- building blocks: every function returns [(face, palette cell)] ----------------------------------------------------

def add_box(bm, center, size, cell, rot=(0.0, 0.0, 0.0)):
    """A box centred at center, turned about its own centre by the Euler angles rot (radians, XYZ)."""
    c = Vector(center)
    matrix = Euler(rot, "XYZ").to_matrix()
    sx, sy, sz = (s / 2 for s in size)
    verts = []
    for dz in (-1, 1):
        for dy in (-1, 1):
            for dx in (-1, 1):
                verts.append(bm.verts.new(c + matrix @ Vector((dx * sx, dy * sy, dz * sz))))
    result = []
    for idx in [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]:
        face = bm.faces.new([verts[i] for i in idx])
        face_out(face, c)
        result.append((face, cell))
    return result


def add_quad(bm, p0, p1, p2, p3, cell, inside):
    face = bm.faces.new([bm.verts.new(Vector(p)) for p in (p0, p1, p2, p3)])
    face_out(face, Vector(inside))
    return [(face, cell)]


def add_tri(bm, p0, p1, p2, cell, inside):
    face = bm.faces.new([bm.verts.new(Vector(p)) for p in (p0, p1, p2)])
    face_out(face, Vector(inside))
    return [(face, cell)]


def add_cyl(bm, center, radius, height, cell, sides=8, cap=True):
    """An upright cylinder (centre = middle of its height); the bottom is left open."""
    cx, cy, cz = center
    ring_b = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy + radius * math.sin(2 * math.pi * k / sides), cz - height / 2)) for k in range(sides)]
    ring_t = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy + radius * math.sin(2 * math.pi * k / sides), cz + height / 2)) for k in range(sides)]
    result = []
    for k in range(sides):
        face = bm.faces.new([ring_b[k], ring_b[(k + 1) % sides], ring_t[(k + 1) % sides], ring_t[k]])
        face_out(face, Vector(center))
        result.append((face, cell))
    if cap:
        top = bm.faces.new(ring_t)
        face_out(top, Vector(center))
        result.append((top, cell))
    return result


def add_wheel(bm, center, radius, width, cell, sides=8):
    """A cylinder lying along Y (a cart wheel)."""
    cx, cy, cz = center
    ring_a = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy - width / 2, cz + radius * math.sin(2 * math.pi * k / sides))) for k in range(sides)]
    ring_b = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy + width / 2, cz + radius * math.sin(2 * math.pi * k / sides))) for k in range(sides)]
    result = []
    for k in range(sides):
        face = bm.faces.new([ring_a[k], ring_a[(k + 1) % sides], ring_b[(k + 1) % sides], ring_b[k]])
        face_out(face, Vector(center))
        result.append((face, cell))
    for ring in (ring_a, ring_b):
        cap = bm.faces.new(ring)
        face_out(cap, Vector(center))
        result.append((cap, STONE_D if cell != STONE_D else WOOD_DARK))
    return result


def add_blob(bm, center, size, cell, seed=0):
    """A lumpy ball (icosphere, 20 faces), for hay, sacks and fruit."""
    rng = random.Random(seed)
    geo = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    verts = [v for v in geo["verts"]]
    for v in verts:
        j = rng.uniform(0.88, 1.12)
        v.co = Vector(center) + Vector((v.co.x * size[0] * j, v.co.y * size[1] * j, v.co.z * size[2] * j))
    faces = {f for v in verts for f in v.link_faces}
    for face in faces:
        face_out(face, Vector(center))
    return [(f, cell) for f in faces]


def add_gable_roof(bm, center, width, depth, base_z, rise, cell, gable_cell, overhang=0.04):
    """A gable roof, ridge along X; slopes are single faces. center = (x, y) of the roof."""
    cx, cy = center
    w, d = width / 2 + overhang, depth / 2 + overhang
    inside = (cx, cy, base_z + rise * 0.3)
    fl, fr = (cx - w, cy - d, base_z), (cx + w, cy - d, base_z)
    bl, br = (cx - w, cy + d, base_z), (cx + w, cy + d, base_z)
    rl, rr = (cx - w, cy, base_z + rise), (cx + w, cy, base_z + rise)
    result = add_quad(bm, fl, fr, rr, rl, cell, inside) + add_quad(bm, bl, br, rr, rl, cell, inside)
    # Gable ends: inset so they sit under the overhang.
    g = overhang * 0.6
    h = rise * (1 - g / max(w, 0.001))
    gl = [(cx - w + g, cy - depth / 2, base_z), (cx - w + g, cy + depth / 2, base_z), (cx - w + g, cy, base_z + h)]
    gr = [(cx + w - g, cy - depth / 2, base_z), (cx + w - g, cy + depth / 2, base_z), (cx + w - g, cy, base_z + h)]
    result += add_tri(bm, gl[0], gl[1], gl[2], gable_cell, inside) + add_tri(bm, gr[0], gr[1], gr[2], gable_cell, inside)
    return result


# ---- finishing -------------------------------------------------------------------------------------------------------

def finish(name, bm, coloured, budget=TRI_BUDGET):
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face, cell in coloured:
        u, v = cell_uv(cell)
        for loop in face.loops:
            loop[uv_layer].uv = (u, v)
        face.smooth = False
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(palette_material())
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= budget, f"{name}: {tris} triangles"
    return obj


# ---- props -----------------------------------------------------------------------------------------------------------

def build_stall(name, stripe_a, stripe_b, goods):
    """A market stall: counter with goods, four posts, a striped awning sloping toward the front (-Y)."""
    bm = bmesh.new()
    coloured = []
    w, d = 0.62, 0.34
    front_h, back_h = 0.30, 0.40
    for sx in (-1, 1):
        coloured += add_box(bm, (sx * (w / 2 - 0.02), d / 2 - 0.02, back_h / 2), (0.025, 0.025, back_h), WOOD_DARK)
        coloured += add_box(bm, (sx * (w / 2 - 0.02), -d / 2 + 0.02, front_h / 2), (0.025, 0.025, front_h), WOOD_DARK)
    coloured += add_box(bm, (0, d / 2 - 0.02, 0.17), (w - 0.05, 0.012, 0.32), WOOD_MID)  # back wall
    coloured += add_box(bm, (0, -0.07, 0.065), (w - 0.07, 0.13, 0.13), PLANK_TAN)  # counter
    coloured += add_box(bm, (0, -0.07, 0.14), (w - 0.03, 0.15, 0.02), PLANK_BROWN)  # counter top

    # Awning: stripes of single faces from the back eave to a little past the front posts.
    stripes = 6
    y0, y1 = d / 2 + 0.01, -d / 2 - 0.06
    inside = (0, 0, 0.2)
    for i in range(stripes):
        x0, x1 = -w / 2 - 0.02 + (w + 0.04) * i / stripes, -w / 2 - 0.02 + (w + 0.04) * (i + 1) / stripes
        cell = stripe_a if i % 2 == 0 else stripe_b
        coloured += add_quad(bm, (x0, y0, back_h), (x1, y0, back_h), (x1, y1, front_h - 0.01), (x0, y1, front_h - 0.01), cell, inside)
        coloured += add_quad(bm, (x0, y1, front_h - 0.01), (x1, y1, front_h - 0.01), (x1, y1, front_h - 0.05), (x0, y1, front_h - 0.05), cell, (0, 0, 0.1))

    # Goods on the counter.
    rng = random.Random(3)
    for k, x in enumerate((-0.19, 0.0, 0.19)):
        coloured += add_box(bm, (x, -0.07, 0.17), (0.15, 0.11, 0.04), WOOD_MID)
        item_cell = goods[k % len(goods)]
        for j in range(3):
            jx = x + (j - 1) * 0.04 + rng.uniform(-0.008, 0.008)
            if item_cell == "log":
                coloured += add_box(bm, (jx, -0.07, 0.2), (0.035, 0.1, 0.035), PLANK_BROWN, (0, 0, rng.uniform(-0.2, 0.2)))
            else:
                coloured += add_box(bm, (jx, -0.07 + rng.uniform(-0.02, 0.02), 0.205), (0.035, 0.035, 0.035), item_cell, (0, 0, rng.uniform(0, 1)))
    return finish(name, bm, coloured)


def build_house(name, wall, roof, size, wall_h, rise, door_x=-0.1, windows=(0.2,), chimney=True):
    w, d = size
    bm = bmesh.new()
    coloured = []
    coloured += add_box(bm, (0, 0, 0.02), (w + 0.02, d + 0.02, 0.04), STONE_D)  # plinth
    coloured += add_box(bm, (0, 0, 0.04 + wall_h / 2), (w, d, wall_h), wall)
    coloured += add_gable_roof(bm, (0, 0), w, d, 0.04 + wall_h, rise, roof, wall)
    # Front door and windows (on the -Y face).
    door_h = min(0.2, wall_h * 0.62)
    coloured += add_box(bm, (door_x, -d / 2 - 0.004, 0.04 + door_h / 2), (0.1, 0.012, door_h), WOOD_DARK)
    coloured += add_box(bm, (door_x, -d / 2 - 0.012, 0.04 + door_h + 0.01), (0.13, 0.012, 0.02), PLANK_BROWN)  # lintel
    for wx in windows:
        coloured += add_box(bm, (wx, -d / 2 - 0.004, 0.04 + wall_h * 0.58), (0.085, 0.012, 0.085), BLUE_L)
        coloured += add_box(bm, (wx, -d / 2 - 0.01, 0.04 + wall_h * 0.58 - 0.05), (0.11, 0.014, 0.014), WOOD_MID)  # sill
    if chimney:
        coloured += add_box(bm, (w * 0.28, d * 0.12, 0.04 + wall_h + rise * 0.55), (0.07, 0.07, rise * 0.9), STONE_D)
    return finish(name, bm, coloured)


def build_house_large():
    """The trading house: a wide hall with a sign board over the door and a low annex."""
    w, d, wall_h, rise = 1.0, 0.7, 0.42, 0.28
    bm = bmesh.new()
    coloured = []
    coloured += add_box(bm, (0, 0, 0.025), (w + 0.03, d + 0.03, 0.05), STONE_D)
    coloured += add_box(bm, (0, 0, 0.05 + wall_h / 2), (w, d, wall_h), WALL_CREAM)
    # Half timbers on the front.
    for x in (-0.46, -0.16, 0.16, 0.46):
        coloured += add_box(bm, (x, -d / 2 - 0.004, 0.05 + wall_h / 2), (0.025, 0.01, wall_h), WOOD_DARK)
    coloured += add_box(bm, (0, -d / 2 - 0.004, 0.05 + wall_h - 0.015), (w, 0.01, 0.025), WOOD_DARK)
    coloured += add_gable_roof(bm, (0, 0), w, d, 0.05 + wall_h, rise, ROOF_RUST, WALL_CREAM, overhang=0.06)
    coloured += add_box(bm, (0, -d / 2 - 0.006, 0.05 + 0.1), (0.14, 0.014, 0.2), WOOD_DARK)  # door
    coloured += add_box(bm, (0, -d / 2 - 0.05, 0.05 + 0.27), (0.26, 0.012, 0.09), PLANK_TAN)  # sign board
    coloured += add_box(bm, (0, -d / 2 - 0.046, 0.05 + 0.27), (0.2, 0.014, 0.05), YELLOW_L)
    for x in (-0.3, 0.3):
        coloured += add_box(bm, (x, -d / 2 - 0.004, 0.05 + 0.24), (0.1, 0.012, 0.1), BLUE_L)
        coloured += add_box(bm, (x, -d / 2 - 0.01, 0.05 + 0.18), (0.13, 0.014, 0.014), WOOD_MID)
    coloured += add_box(bm, (-0.32, 0.12, 0.05 + wall_h + rise * 0.5), (0.08, 0.08, rise), STONE_D)
    # Annex on the right side, lower, with its own roof.
    aw, ad, ah = 0.34, 0.46, 0.28
    ax = w / 2 + aw / 2 - 0.02
    coloured += add_box(bm, (ax, 0.05, 0.05 + ah / 2), (aw, ad, ah), WALL_WHITE)
    coloured += add_gable_roof(bm, (ax, 0.05), aw, ad, 0.05 + ah, 0.16, ROOF_RUST, WALL_WHITE, overhang=0.03)
    coloured += add_box(bm, (ax, 0.05 - ad / 2 - 0.004, 0.05 + 0.08), (0.08, 0.012, 0.16), WOOD_DARK)
    return finish("HarborHouseLarge", bm, coloured, budget=500)


def build_quay():
    """A boardwalk module, two cells long: planks on beams, posts below. Used on the shore and over the water."""
    bm = bmesh.new()
    coloured = []
    length, width = CELL * 2, CELL
    planks = 7
    for i in range(planks):
        x = -length / 2 + length * (i + 0.5) / planks
        coloured += add_box(bm, (x, 0, 0.0), (length / planks - 0.004, width, 0.03), PLANK_TAN if i % 2 == 0 else PLANK_BROWN)
    for y in (-width * 0.38, width * 0.38):
        coloured += add_box(bm, (0, y, -0.03), (length, 0.03, 0.03), WOOD_DARK)
    for x in (-length / 2 + 0.03, length / 2 - 0.03):
        for y in (-width * 0.4, width * 0.4):
            coloured += add_box(bm, (x, y, -0.1), (0.035, 0.035, 0.2), WOOD_DARK)
    obj = finish("HarborQuay", bm, coloured)
    # The deck top is the origin plane (z = 0.015 above it), so a module placed at ground level stands 1.5 cm proud.
    return obj


def build_bollard():
    bm = bmesh.new()
    coloured = add_cyl(bm, (0, 0, 0.045), 0.032, 0.09, WOOD_DARK, sides=6)
    coloured += add_cyl(bm, (0, 0, 0.08), 0.042, 0.02, STONE_D, sides=6)
    coloured += add_cyl(bm, (0, 0, 0.04), 0.036, 0.012, ORANGE, sides=6, cap=False)  # a coil of rope
    return finish("HarborBollard", bm, coloured)


def build_crate():
    bm = bmesh.new()
    coloured = add_box(bm, (0, 0, 0.065), (0.13, 0.13, 0.13), PLANK_TAN)
    for z in (0.02, 0.11):
        coloured += add_box(bm, (0, 0, z), (0.138, 0.138, 0.018), WOOD_MID)
    for x in (-0.057, 0.057):
        coloured += add_box(bm, (x, 0, 0.065), (0.018, 0.138, 0.138), WOOD_MID)
    return finish("HarborCrate", bm, coloured)


def build_barrel():
    bm = bmesh.new()
    coloured = add_cyl(bm, (0, 0, 0.075), 0.065, 0.15, PLANK_BROWN, sides=8)
    for z in (0.035, 0.115):
        coloured += add_cyl(bm, (0, 0, z), 0.0685, 0.016, WOOD_DARK, sides=8, cap=False)
    return finish("HarborBarrel", bm, coloured)


def build_sacks():
    bm = bmesh.new()
    coloured = []
    coloured += add_blob(bm, (-0.05, 0.0, 0.05), (0.075, 0.06, 0.05), WALL_CREAM, 1)
    coloured += add_blob(bm, (0.06, 0.02, 0.045), (0.07, 0.055, 0.045), HAY, 2)
    coloured += add_blob(bm, (0.0, -0.01, 0.12), (0.065, 0.055, 0.045), WALL_CREAM, 3)
    return finish("HarborSacks", bm, coloured)


def build_lantern():
    bm = bmesh.new()
    coloured = add_box(bm, (0, 0, 0.17), (0.022, 0.022, 0.34), WOOD_DARK)
    coloured += add_box(bm, (0.035, 0, 0.32), (0.07, 0.014, 0.014), WOOD_DARK)  # arm
    coloured += add_box(bm, (0.07, 0, 0.285), (0.045, 0.045, 0.05), YELLOW_L)  # lamp
    coloured += add_box(bm, (0.07, 0, 0.318), (0.058, 0.058, 0.012), WOOD_DARK)  # lid
    coloured += add_box(bm, (0.07, 0, 0.258), (0.05, 0.05, 0.01), WOOD_DARK)
    coloured += add_box(bm, (0, 0, 0.02), (0.05, 0.05, 0.04), STONE_D)
    return finish("HarborLantern", bm, coloured)


def build_signpost():
    bm = bmesh.new()
    coloured = add_box(bm, (0, 0, 0.16), (0.024, 0.024, 0.32), WOOD_DARK)
    coloured += add_box(bm, (0.06, -0.016, 0.27), (0.17, 0.01, 0.055), PLANK_TAN, (0, 0, 0.0))
    coloured += add_tri(bm, (0.145, -0.021, 0.2975), (0.145, -0.021, 0.2425), (0.185, -0.021, 0.27), PLANK_TAN, (0.1, 0.0, 0.27))
    coloured += add_box(bm, (-0.05, -0.016, 0.2), (0.15, 0.01, 0.05), PLANK_BROWN)
    coloured += add_tri(bm, (-0.125, -0.021, 0.225), (-0.125, -0.021, 0.175), (-0.165, -0.021, 0.2), PLANK_BROWN, (-0.1, 0.0, 0.2))
    return finish("HarborSignpost", bm, coloured)


def build_flowerbed():
    bm = bmesh.new()
    coloured = add_box(bm, (0, 0, 0.03), (0.62, 0.2, 0.06), WOOD_MID)
    coloured += add_box(bm, (0, 0, 0.062), (0.58, 0.16, 0.012), WOOD_DARK)
    rng = random.Random(5)
    colours = [PINK, YELLOW_L, RED_BRIGHT, PURPLE, WALL_CREAM]
    for i in range(10):
        x = -0.26 + 0.52 * i / 9
        y = rng.uniform(-0.05, 0.05)
        h = rng.uniform(0.04, 0.075)
        coloured += add_box(bm, (x, y, 0.065 + h / 2), (0.008, 0.008, h), GREEN)
        coloured += add_box(bm, (x, y, 0.065 + h + 0.008), (0.03, 0.03, 0.02), colours[i % len(colours)], (0, 0, rng.uniform(0, 1)))
    return finish("HarborFlowerBed", bm, coloured)


def build_cart():
    bm = bmesh.new()
    coloured = add_box(bm, (0, 0, 0.1), (0.36, 0.22, 0.03), PLANK_BROWN)
    for y in (-0.105, 0.105):
        coloured += add_box(bm, (0, y, 0.135), (0.36, 0.012, 0.05), PLANK_TAN)
    coloured += add_box(bm, (-0.174, 0, 0.135), (0.012, 0.22, 0.05), PLANK_TAN)
    coloured += add_box(bm, (0.174, 0, 0.135), (0.012, 0.22, 0.05), PLANK_TAN)
    for y in (-0.13, 0.13):
        coloured += add_wheel(bm, (-0.03, y, 0.075), 0.075, 0.02, WOOD_DARK)
    coloured += add_box(bm, (-0.03, 0, 0.075), (0.02, 0.26, 0.02), WOOD_DARK)  # axle
    for y in (-0.06, 0.06):
        coloured += add_box(bm, (0.27, y, 0.095), (0.2, 0.014, 0.014), WOOD_MID, (0, 0, 0))  # shafts
    coloured += add_blob(bm, (0.0, 0.0, 0.16), (0.16, 0.1, 0.07), HAY, 7)
    coloured += add_blob(bm, (0.07, 0.02, 0.2), (0.09, 0.07, 0.05), HAY, 8)
    return finish("HarborCart", bm, coloured)


def build_drying_rack():
    bm = bmesh.new()
    coloured = []
    for x in (-0.22, 0.22):
        coloured += add_box(bm, (x, 0, 0.16), (0.022, 0.022, 0.32), WOOD_DARK)
        coloured += add_box(bm, (x, 0, 0.01), (0.022, 0.12, 0.02), WOOD_MID)
    coloured += add_box(bm, (0, 0, 0.30), (0.5, 0.018, 0.018), WOOD_MID)
    rng = random.Random(9)
    for i in range(6):
        x = -0.19 + 0.38 * i / 5
        coloured += add_box(bm, (x, 0, 0.24), (0.035, 0.03, 0.1), (GREEN, GREEN_L, HAY)[i % 3], (0, rng.uniform(-0.1, 0.1), 0))
        coloured += add_box(bm, (x, 0, 0.292), (0.01, 0.01, 0.014), WOOD_DARK)
    return finish("HarborDryingRack", bm, coloured)


def build_boat():
    bm = bmesh.new()
    outline = [(-0.27, -0.085), (0.18, -0.105), (0.33, 0.0), (0.18, 0.105), (-0.27, 0.085)]
    bottom = [bm.verts.new((x * 0.8, y * 0.7, 0.0)) for x, y in outline]
    top = [bm.verts.new((x, y, 0.075)) for x, y in outline]
    coloured = []
    inside = Vector((0, 0, 0.04))
    for k in range(5):
        face = bm.faces.new([bottom[k], bottom[(k + 1) % 5], top[(k + 1) % 5], top[k]])
        face_out(face, inside)
        coloured.append((face, PLANK_BROWN))
    floor = bm.faces.new([bm.verts.new((x * 0.8, y * 0.7, 0.012)) for x, y in outline])
    face_out(floor, Vector((0, 0, -1)) * -1 + inside)
    coloured.append((floor, PLANK_TAN))
    rim_cells = []
    for k in range(5):
        a, b = outline[k], outline[(k + 1) % 5]
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0.078)
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        coloured += add_box(bm, mid, (length, 0.018, 0.014), WOOD_DARK, (0, 0, math.atan2(b[1] - a[1], b[0] - a[0])))
    for x in (-0.12, 0.06):
        coloured += add_box(bm, (x, 0, 0.05), (0.04, 0.17, 0.014), PLANK_TAN)
    coloured += add_box(bm, (-0.02, 0.14, 0.09), (0.26, 0.012, 0.012), WOOD_MID, (0, 0, 0.35))  # an oar
    return finish("HarborBoat", bm, coloured)


BUILDERS = [
    lambda: build_stall("HarborStallFruit", RED, WALL_CREAM, [RED_BRIGHT, YELLOW_L, GREEN_L]),
    lambda: build_stall("HarborStallHerbs", GREEN, WALL_CREAM, [GREEN, GREEN_L, YELLOW_L]),
    lambda: build_stall("HarborStallTimber", BLUE_D, WALL_CREAM, ["log", "log", "log"]),
    lambda: build_house("HarborHouseA", WALL_WHITE, ROOF_RUST, (0.72, 0.56), 0.32, 0.2, door_x=-0.12, windows=(0.2,)),
    lambda: build_house("HarborHouseB", PLANK_TAN, THATCH, (0.6, 0.5), 0.3, 0.22, door_x=0.1, windows=(-0.18,)),
    lambda: build_house("HarborHouseC", WALL_CREAM, ROOF_GREY, (0.84, 0.58), 0.34, 0.22, door_x=0.0, windows=(-0.27, 0.27)),
    build_house_large,
    build_quay,
    build_bollard,
    build_crate,
    build_barrel,
    build_sacks,
    build_lantern,
    build_signpost,
    build_flowerbed,
    build_cart,
    build_drying_rack,
    build_boat,
]


def export(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    saved = obj.location.copy()
    obj.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    obj.location = saved


def render_sheet(objects, path):
    scene = bpy.context.scene
    columns = 6
    spacing = 1.25
    for i, obj in enumerate(objects):
        obj.location = ((i % columns) * spacing, -(i // columns) * spacing, 0)
    rows = (len(objects) + columns - 1) // columns
    centre = Vector(((columns - 1) * spacing / 2, -(rows - 1) * spacing / 2, 0.2))
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = columns * spacing * 0.95
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = centre + Vector((0.0, -6.0, 6.0))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    render = scene.render
    render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_object_outline = True
    render.resolution_x = 1800
    render.resolution_y = int(1800 * (rows * spacing * 0.95) / (columns * spacing * 0.95)) + 300
    render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.55, 0.75, 0.55)
    render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendered", path)


def main():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    objects = [build() for build in BUILDERS]
    for obj in objects:
        export(obj, f"{EXPORT_DIR}/{obj.name}.fbx")
    render_sheet(objects, PREVIEW_PATH)


main()
