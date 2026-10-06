"""Birch, Oak and Spruce as recolored derivatives of the Pine mesh (2026-09-23, second revision).

The user rejected a from-scratch icosphere-canopy build for Birch/Oak ("only Pine looks good, base the
rest on it") and asked for a literal recipe: take the Pine mesh, thin/thicken the trunk, recolor trunk
and foliage per species. This script does exactly that - it does NOT generate new geometry.

Prerequisite: Pine.fbx must already exist (see pine_decimate.py) at
Assets/Arts/Models/Environment/Trees/Pine.fbx - a real branch-based conifer mesh (758 polys / 1395
tris), not a billboard, historically misnamed SpruceTree.fbx.

Pine's own mesh has ONE material and no baked-in trunk/foliage split, so the split here is geometric,
not texture-based: a face is "trunk" if BOTH its centre is within radius 0.12 of the vertical (Z) axis
AND below 25% of the tree's total height - this consistently isolates the short bare trunk section
(verified 2026-09-23: 84 of 758 faces) without catching branches that happen to pass close to the axis
higher up. Reuse this same (0.12, 0.25) pair unless the base mesh changes.

Run through the Blender MCP (execute_blender_code) or paste into Blender's Scripting tab; import the
existing Pine.fbx first, apply its rotation (see the "orientation" pitfall in
[[workflow-new-tree-species]] memory), then run build_species() per species below. Export each with the
project's standard recipe (see pine_decimate.py). On the Unity side, DON'T assume Blender's material
slot order survives FBX export - it can flip (seen in practice: slot 0/trunk, the minority of faces,
came back as Unity submesh 0 with the LARGER indexCount). Map materials to submeshes by index count
(the trunk submesh is always the minority) rather than by slot number.
"""
import math

import bpy
from mathutils import Vector

R_THRESH = 0.12
H_THRESH = 0.25


def is_trunk(x, y, z, hmin, hrange):
    r = math.sqrt(x * x + y * y)
    hf = (z - hmin) / hrange
    return r < R_THRESH and hf < H_THRESH


def hx(h):
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, 1)


def build_species(name, trunk_scale, trunk_color_hex, foliage_color_hex):
    """trunk_scale multiplies the XY (radius) of trunk-classified vertices only - <1 thins it, >1
    thickens it. Colors are simple flat Principled BSDF base colors, just for local sanity-checking;
    the real per-species Materials are created directly in Unity (Assets/Arts/Materials/Trees/,
    Universal Render Pipeline/Lit, flat _BaseColor, no texture)."""
    src = bpy.data.objects["PineBase"]
    mesh_copy = src.data.copy()
    obj = bpy.data.objects.new(name, mesh_copy)
    bpy.context.scene.collection.objects.link(obj)

    hmin = min(v.co.z for v in mesh_copy.vertices)
    hmax = max(v.co.z for v in mesh_copy.vertices)
    hrange = hmax - hmin

    vert_is_trunk = [is_trunk(v.co.x, v.co.y, v.co.z, hmin, hrange) for v in mesh_copy.vertices]
    for v, t in zip(mesh_copy.vertices, vert_is_trunk):
        if t:
            v.co.x *= trunk_scale
            v.co.y *= trunk_scale

    mat_trunk = bpy.data.materials.new(name + "_Bark")
    mat_trunk.use_nodes = True
    next(n for n in mat_trunk.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = hx(trunk_color_hex)
    mat_foliage = bpy.data.materials.new(name + "_Leaf")
    mat_foliage.use_nodes = True
    next(n for n in mat_foliage.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = hx(foliage_color_hex)
    mesh_copy.materials.clear()
    mesh_copy.materials.append(mat_trunk)
    mesh_copy.materials.append(mat_foliage)

    for f in mesh_copy.polygons:
        trunk_face = all(vert_is_trunk[i] for i in f.vertices)
        f.material_index = 0 if trunk_face else 1

    mesh_copy.update()
    print(name, "verts", len(mesh_copy.vertices), "polys", len(mesh_copy.polygons))
    return obj


# ---- import Pine.fbx and fix its orientation (see the module docstring / memory) --------------------

for old in ("PineBase", "Birch", "Oak", "Spruce"):
    o = bpy.data.objects.get(old)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)

before = set(o.name for o in bpy.data.objects)
bpy.ops.import_scene.fbx(filepath="E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Trees/Pine.fbx")
after = set(o.name for o in bpy.data.objects)
pine_obj = bpy.data.objects[next(iter(after - before))]
pine_obj.name = "PineBase"
for o in bpy.data.objects:
    o.select_set(o.name == "PineBase")
bpy.context.view_layer.objects.active = pine_obj
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# ---- species (2026-09-23 colours/thicknesses) --------------------------------------------------------

build_species("Birch", trunk_scale=0.75, trunk_color_hex="D8D8D8", foliage_color_hex="B7C873")
build_species("Oak", trunk_scale=1.45, trunk_color_hex="947157", foliage_color_hex="899E52")
build_species("Spruce", trunk_scale=1.05, trunk_color_hex="513A31", foliage_color_hex="3D8475")
print("DONE - export each with the standard FBX recipe (see pine_decimate.py)")
