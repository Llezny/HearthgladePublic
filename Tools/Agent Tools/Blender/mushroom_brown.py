"""Low-poly brown mushroom (porcini) for Hearthglade, 88 triangles, shared palette texture.

Made live in Blender 5.2 through the Blender MCP, kept here so it can be regenerated or tweaked. Paste it into Blender's
Scripting tab (or run it through execute_blender_code). It replaces the default cube, so use a fresh scene. Export
afterwards with:

    bpy.ops.export_scene.fbx(filepath=..., use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False)

Units are metres, Z up, origin at the bottom of the stem. Colours come from the project palette
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png, 32x32 image of 4x4 px cells): every face gets a flat UV at
the centre of one cell, so the model needs no texture or material of its own in Unity.
"""
import math

import bmesh
import bpy
from mathutils import Vector

NAME = "MushroomBrown"
SEG = 8
TRI_BUDGET = 100
PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"

# Palette cells (row, col) from the top-left of the palette image.
CELL_CAP = (2, 5)     # 947157 chestnut brown
CELL_STEM = (0, 3)    # D3B396 light tan
CELL_GILLS = (0, 4)   # BEB296 pale tan

STEM = [(0.050, 0.000), (0.060, 0.045), (0.036, 0.100)]   # (radius, height): bulging base, narrower top
CAP = [(0.100, 0.105), (0.090, 0.140), (0.052, 0.172)]    # rim, shoulder, crown
APEX_Z = 0.185


def cell_uv(cell, size=32, px=4):
    row, col = cell
    return ((col * px + px / 2) / size, 1.0 - (row * px + px / 2) / size)


def ring(bm, radius, z, wobble=0.0, phase=0.0):
    verts = []
    for i in range(SEG):
        a = 2 * math.pi * i / SEG
        r = radius * (1.0 + wobble * math.sin(i * 2.4 + phase))
        verts.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
    return verts


mesh = bpy.data.meshes.new(NAME)
bm = bmesh.new()
faces = []  # (face, palette cell, group)


def band(lower, upper, cell, group):
    for i in range(SEG):
        j = (i + 1) % SEG
        faces.append((bm.faces.new([lower[i], lower[j], upper[j], upper[i]]), cell, group))


def fan(base, top, cell, group):
    for i in range(SEG):
        j = (i + 1) % SEG
        faces.append((bm.faces.new([base[i], base[j], top]), cell, group))


stem = [ring(bm, r, z) for r, z in STEM]
cap = [ring(bm, r, z, wobble=0.05, phase=k) for k, (r, z) in enumerate(CAP)]
apex = bm.verts.new((0.0, 0.0, APEX_Z))

band(stem[0], stem[1], CELL_STEM, "stem")
band(stem[1], stem[2], CELL_STEM, "stem")
band(stem[2], cap[0], CELL_GILLS, "under")
band(cap[0], cap[1], CELL_CAP, "cap")
band(cap[1], cap[2], CELL_CAP, "cap")
fan(cap[2], apex, CELL_CAP, "cap")

# Winding: every face must point away from the mushroom (downwards for the underside).
bm.faces.ensure_lookup_table()
bm.normal_update()
middle = Vector((0.0, 0.0, 0.10))
for face, _cell, group in faces:
    c = face.calc_center_median()
    if group == "under":
        outward = face.normal.z < 0
    elif group == "stem":
        outward = face.normal.x * c.x + face.normal.y * c.y > 0
    else:
        outward = face.normal.dot(c - middle) > 0
    if not outward:
        face.normal_flip()

uv_layer = bm.loops.layers.uv.new("UVMap")
for face, cell, _group in faces:
    u, v = cell_uv(cell)
    for loop in face.loops:
        loop[uv_layer].uv = (u, v)
    face.smooth = False

bm.to_mesh(mesh)
bm.free()

cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

obj = bpy.data.objects.new(NAME, mesh)
bpy.context.scene.collection.objects.link(obj)

mat = bpy.data.materials.new("palette")
mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(PALETTE)
tex.interpolation = "Closest"
bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
obj.data.materials.append(mat)

mesh.calc_loop_triangles()
tris = len(mesh.loop_triangles)
print("tris", tris, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions))
assert tris <= TRI_BUDGET, f"{tris} triangles"
