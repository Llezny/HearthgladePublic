"""Low-poly gatherables for Hearthglade world generation phase 3: IronOre, GoldOre, Watermelon, Stick.

Run through the Blender MCP (execute_blender_code) or paste into Blender's Scripting tab. Units are metres, Z up, origin at the
bottom centre of each object (they are placed side by side only for the preview, see PREVIEW_X). Like the mushrooms, colours come
from the project palette (Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): every face gets a flat UV at the centre of
one palette cell, so the models need no texture or material of their own in Unity. Prefabs scale them by 0.3.

Export one object at a time with:

    bpy.ops.export_scene.fbx(filepath=..., use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False)
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
NAMES = ["IronOre", "GoldOre", "Watermelon", "Stick"]
PREVIEW_X = {"IronOre": 0.0, "GoldOre": 0.7, "Watermelon": 1.4, "Stick": 2.1}
TRI_BUDGET = 130

# Palette cells (row, col) from the top-left of the palette image.
ROCK_SIDE = (0, 6)    # 444D4B dark grey-green
ROCK_TOP = (5, 5)     # 7C8583 mid grey (added to the palette 2026-09-21)
IRON_A = (5, 2)       # A9B4BD steel (added)
IRON_B = (4, 3)       # C5721D rust orange
GOLD_A = (5, 1)       # F5D65C light gold (added)
GOLD_B = (5, 0)       # E0A81E gold (added)
MELON_LIGHT = (5, 6)  # 7FBF6A light green (added)
MELON_DARK = (5, 7)   # 2F6B3A dark green (added)
STEM = (0, 1)         # 513A31 dark brown
BARK_A = (2, 5)       # 947157 brown
BARK_B = (0, 2)       # 9D7C68 tan brown
CUT_WOOD = (0, 3)     # D3B396 light tan


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
    """Flips the face so that it points away from the point ref (a point inside the piece)."""
    face.normal_update()
    if face.normal.dot(face.calc_center_median() - ref) < 0:
        face.normal_flip()


def finish(name, bm, coloured):
    """coloured: list of (face, palette cell). Turns the bmesh into the object <name>."""
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
    obj.location = (PREVIEW_X[name], 0.0, 0.0)
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= TRI_BUDGET, f"{name}: {tris} triangles"
    return obj


# ---- ore rocks ----------------------------------------------------------------------------------------------------------

def shard(bm, point, normal, size, height, rng, cells):
    """A chunky four sided nugget half sunk into the surface at point; returns [(face, cell)]."""
    t1 = normal.cross(Vector((0, 0, 1)))
    if t1.length < 0.1:
        t1 = normal.cross(Vector((1, 0, 0)))
    t1.normalize()
    t2 = normal.cross(t1)
    base = []
    for k in range(4):
        a = 2 * math.pi * k / 4 + rng.uniform(-0.25, 0.25)
        r = size * rng.uniform(0.85, 1.15)
        base.append(bm.verts.new(point + (t1 * math.cos(a) + t2 * math.sin(a)) * r - normal * 0.02))
    apex = bm.verts.new(point + normal * height + t1 * rng.uniform(-0.02, 0.02) + t2 * rng.uniform(-0.02, 0.02))
    inside = point - normal * 0.06
    result = []
    for k in range(4):
        face = bm.faces.new([base[k], base[(k + 1) % 4], apex])
        face_out(face, inside)
        result.append((face, cells[k % 2]))
    return result


def build_ore(name, seed, shard_cells, shards):
    rng = random.Random(seed)
    rx, ry, rz, cz = 0.24, 0.23, 0.17, 0.09
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in bm.verts:
        j = rng.uniform(0.88, 1.12)
        v.co = Vector((v.co.x * rx * j, v.co.y * ry * j, max(0.0, cz + v.co.z * rz * j)))
    flat = [f for f in bm.faces if all(v.co.z < 0.001 for v in f.verts)]
    bmesh.ops.delete(bm, geom=flat, context="FACES")
    bm.faces.ensure_lookup_table()
    coloured = []
    for face in bm.faces:
        face.normal_update()
        coloured.append((face, ROCK_TOP if face.normal.z > 0.7 else ROCK_SIDE))

    for i in range(shards):
        # Spread over the upper half of the rock; the golden angle keeps them from bunching up.
        a = i * 2.39996 + rng.uniform(-0.3, 0.3)
        dz = 0.2 + 0.7 * (i / max(1, shards - 1)) ** 0.7
        dxy = math.sqrt(1 - dz * dz)
        d = Vector((dxy * math.cos(a), dxy * math.sin(a), dz))
        point = Vector((rx * d.x, ry * d.y, cz + rz * d.z))
        normal = Vector((d.x / rx, d.y / ry, d.z / rz)).normalized()
        coloured += shard(bm, point, normal, rng.uniform(0.065, 0.085), rng.uniform(0.06, 0.09), rng, shard_cells)
    return finish(name, bm, coloured)


# ---- watermelon -----------------------------------------------------------------------------------------------------------

def build_watermelon():
    name = "Watermelon"
    seg = 10
    a, b, c, cz = 0.24, 0.185, 0.19, 0.19
    elevations = [-1.1, -0.45, 0.15, 0.7, 1.15]
    rng = random.Random(7)
    bm = bmesh.new()
    rings = []
    for e in elevations:
        ring = []
        for i in range(seg):
            ang = 2 * math.pi * i / seg
            w = 1.0 + rng.uniform(-0.03, 0.03)
            ring.append(bm.verts.new((a * math.cos(e) * math.cos(ang) * w, b * math.cos(e) * math.sin(ang) * w, cz + c * math.sin(e))))
        rings.append(ring)
    top = bm.verts.new((0, 0, cz + c))
    centre = Vector((0, 0, cz))
    coloured = []
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            face = bm.faces.new([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]])
            face_out(face, centre)
            coloured.append((face, MELON_LIGHT if i % 2 == 0 else MELON_DARK))
    for i in range(seg):
        j = (i + 1) % seg
        face = bm.faces.new([rings[-1][i], rings[-1][j], top])
        face_out(face, centre)
        coloured.append((face, MELON_LIGHT if i % 2 == 0 else MELON_DARK))
    bottom = bm.faces.new(rings[0])
    face_out(bottom, centre)
    coloured.append((bottom, MELON_DARK))

    # A short stalk on top.
    stalk_low = [bm.verts.new((0.02 * math.cos(t), 0.02 * math.sin(t), cz + c - 0.01)) for t in (0, math.pi / 2, math.pi, 1.5 * math.pi)]
    stalk_high = [bm.verts.new((0.013 * math.cos(t) + 0.012, 0.013 * math.sin(t), cz + c + 0.05)) for t in (0, math.pi / 2, math.pi, 1.5 * math.pi)]
    axis = Vector((0.006, 0, cz + c + 0.02))
    for i in range(4):
        j = (i + 1) % 4
        face = bm.faces.new([stalk_low[i], stalk_low[j], stalk_high[j], stalk_high[i]])
        face_out(face, axis)
        coloured.append((face, STEM))
    cap = bm.faces.new(stalk_high)
    face_out(cap, axis)
    coloured.append((cap, STEM))
    return finish(name, bm, coloured)


# ---- sticks -----------------------------------------------------------------------------------------------------------------

def tube(bm, points, radii, sides=5):
    """A branch through the points; returns [(face, cell)]. Ends are light (cut wood), the rest is bark."""
    rings = []
    centres = [Vector(p) for p in points]
    for i, p in enumerate(centres):
        direction = (centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]).normalized()
        side = direction.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(direction)
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(p + (side * math.cos(ang) + up * math.sin(ang)) * radii[i]))
        rings.append(ring)
    result = []
    for i in range(len(rings) - 1):
        ref = (centres[i] + centres[i + 1]) / 2
        for k in range(sides):
            face = bm.faces.new([rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]])
            face_out(face, ref)
            result.append((face, BARK_A if (k + i) % 2 == 0 else BARK_B))
    for ring, other in ((rings[0], centres[-1]), (rings[-1], centres[0])):
        face = bm.faces.new(ring)
        face_out(face, other)
        result.append((face, CUT_WOOD))
    return result


def build_stick():
    bm = bmesh.new()
    coloured = []
    coloured += tube(bm, [(-0.34, 0.0, 0.03), (0.0, 0.03, 0.03), (0.34, -0.02, 0.03)], [0.03, 0.028, 0.02])
    coloured += tube(bm, [(0.0, 0.03, 0.035), (0.13, 0.15, 0.04)], [0.018, 0.012])
    coloured += tube(bm, [(-0.2, -0.14, 0.025), (0.08, -0.15, 0.025), (0.3, -0.10, 0.03)], [0.025, 0.024, 0.018])
    return finish("Stick", bm, coloured)


# ---- build everything ---------------------------------------------------------------------------------------------------------

for old in NAMES:
    obj = bpy.data.objects.get(old)
    if obj is not None:
        bpy.data.objects.remove(obj, do_unlink=True)
cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

build_ore("IronOre", 11, (IRON_A, IRON_B), 7)
build_ore("GoldOre", 23, (GOLD_A, GOLD_B), 7)
build_watermelon()
build_stick()
