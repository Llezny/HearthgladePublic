"""Low-poly cooking pot for Hearthglade cooking system - the visual that appears over a Fireplace
once it is upgraded to the Pot tier (see CookingStation.cs / CookingStationLevel.Pot).

Run through the Blender MCP (execute_blender_code) or paste into Blender's Scripting tab. Units are
metres, Z up, origin at the bottom centre. Colours come from the project palette
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): every face gets a flat UV at the centre
of one palette cell, so the model needs no texture or material of its own in Unity.

Export with:

    bpy.ops.export_scene.fbx(filepath=..., use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             global_scale=1.0, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False)
"""
import math

import bmesh
import bpy
from mathutils import Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
TRI_BUDGET = 220

# Palette cells (row, col) from the top-left of the palette image - see Tools/Agent Tools/Blender/world_props.py.
POT_BODY = (5, 2)   # A9B4BD steel
POT_DARK = (5, 3)   # 6E7A85 dark steel


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
    print(name, "tris", tris, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= TRI_BUDGET, f"{name}: {tris} triangles"
    return obj


def ring(bm, z, radius, seg=8):
    return [bm.verts.new((radius * math.cos(2 * math.pi * i / seg), radius * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)]


def band(bm, ring_a, ring_b, cell, centre_ref):
    seg = len(ring_a)
    faces = []
    for i in range(seg):
        j = (i + 1) % seg
        f = bm.faces.new([ring_a[i], ring_a[j], ring_b[j], ring_b[i]])
        face_out(f, centre_ref)
        faces.append((f, cell))
    return faces


def band_inward(bm, ring_a, ring_b, cell, axis_ref):
    """Same band, but facing inward (for a concave cavity) - flips whatever band() would give."""
    faces = band(bm, ring_a, ring_b, cell, axis_ref)
    for f, _ in faces:
        f.normal_flip()
    return faces


def cap(bm, ring_pts, cell, inside_ref):
    f = bm.faces.new(ring_pts)
    face_out(f, inside_ref)
    return [(f, cell)]


def tube(bm, points, radii, cell_a, cell_b, sides=6):
    """An arc/branch through the points; returns [(face, cell)] with alternating colours around."""
    rings = []
    centres = [Vector(p) for p in points]
    for i, p in enumerate(centres):
        direction = (centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]).normalized()
        side = direction.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = direction.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(direction)
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / sides) + up * math.sin(2 * math.pi * k / sides)) * radii[i]) for k in range(sides)])
    result = []
    for i in range(len(rings) - 1):
        ref = (centres[i] + centres[i + 1]) / 2
        for k in range(sides):
            f = bm.faces.new([rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]])
            face_out(f, ref)
            result.append((f, cell_a if k % 2 == 0 else cell_b))
    return result


def build_pot():
    bm = bmesh.new()
    coloured = []
    centre = Vector((0, 0, 0.09))
    r0 = ring(bm, 0.00, 0.085, 8)
    r1 = ring(bm, 0.03, 0.12, 8)
    r2 = ring(bm, 0.14, 0.15, 8)
    r3 = ring(bm, 0.17, 0.14, 8)

    coloured += band(bm, r0, r1, POT_BODY, centre)
    coloured += band(bm, r1, r2, POT_BODY, centre)
    coloured += band(bm, r2, r3, POT_DARK, centre)
    coloured += cap(bm, r0, POT_DARK, Vector((0, 0, 0.05)))

    # Concave interior (rim down to a small inner floor) so food can visually sit inside the pot.
    inner_mid = ring(bm, 0.13, 0.115, 8)
    inner_floor = ring(bm, 0.07, 0.06, 8)
    coloured += band_inward(bm, r3, inner_mid, POT_DARK, centre)
    coloured += band_inward(bm, inner_mid, inner_floor, POT_DARK, centre)
    coloured += cap(bm, inner_floor, POT_DARK, Vector((0, 0, 0.03)))

    # Bail handle arcing over the top, so the pot reads as something hung over a fire.
    apex_z = 0.29
    handle_pts = [
        (0.13, 0, 0.16),
        (0.09, 0, apex_z),
        (0.0, 0, apex_z + 0.02),
        (-0.09, 0, apex_z),
        (-0.13, 0, 0.16),
    ]
    coloured += tube(bm, handle_pts, [0.011] * 5, POT_DARK, POT_DARK, sides=6)

    return finish("Pot", bm, coloured)


old = bpy.data.objects.get("Pot")
if old is not None:
    bpy.data.objects.remove(old, do_unlink=True)
cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

build_pot()
