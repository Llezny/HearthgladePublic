"""Stone age hand tools for Hearthglade (docs/EQUIPMENT_PLAN.md, phase 4): stone pickaxe, stone axe, stone spear and stone sickle.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python stone_tools.py
Writes one FBX per tool into EXPORT_DIR, the item icons (512x512, transparent) into ICON_DIR, a preview sheet into PREVIEW_DIR and the
sources into BlenderModelSources/StoneTools.blend.

Shared convention, so one hand pose fits every tool (Blender coordinates, metres, Z up, the same model faces -Z / up Y in Unity):
  * the origin is the middle of the grip, the shaft runs along +Z (the head is above the hand);
  * the cutting edge or the point faces -Y (forward); the head profile lies in the YZ plane and is thick along X;
  * colours are flat palette cells (Colorsheet Tree Normal.png) like every other model: no material of its own in Unity.
The models are chunky and meant for the 1.2 m character of the kit (which the game scales by 0.3).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import cell_uv, face_out, palette_material  # noqa: E402

EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Items"
ICON_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Equipment"
BLEND_PATH = "E:/Repos/Hearthglade/BlenderModelSources/StoneTools.blend"
PREVIEW_DIR = os.environ.get("STONE_TOOLS_PREVIEW_DIR", "C:/Temp/stone_tools")
TRI_BUDGET = 260

# Palette cells (row, col); see world_props.py.
WOOD = (2, 5)         # 947157 brown
WOOD_DARK = (0, 1)    # 513A31 dark brown (knob, grain)
STONE = (5, 5)        # 7C8583 mid grey
STONE_EDGE = (0, 6)   # 444D4B dark grey-green
STONE_LIGHT = (4, 0)  # B8B8B8 light grey (the sharpened edge)
LEATHER = (6, 1)      # B5763F tan, the lashing


def new_obj(name, bm, coloured):
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
    mesh.materials.append(palette_material())
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    print(name, "tris", tris, "dims", tuple(round(v, 3) for v in obj.dimensions))
    assert tris <= TRI_BUDGET, f"{name}: {tris} triangles"
    return obj


def shaft(bm, z0, z1, radius, colour, sides=6, knob=None, taper=1.0):
    """A straight round shaft along Z from z0 to z1 (taper = radius at z1 / radius at z0); knob: radius of a fatter end at z0."""
    faces = []
    rings = []
    stops = [(z0, radius * (knob or 1.0)), (z0 + 0.03, radius), (z1, radius * taper)] if knob else [(z0, radius), (z1, radius * taper)]
    for z, r in stops:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / sides), r * math.sin(2 * math.pi * i / sides), z)) for i in range(sides)])
    ref = Vector((0, 0, (z0 + z1) / 2))
    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            f = bm.faces.new([a[i], a[j], b[j], b[i]])
            face_out(f, ref)
            faces.append((f, colour))
    for ring_pts, cell in ((rings[0], WOOD_DARK), (rings[-1], colour)):
        f = bm.faces.new(ring_pts)
        face_out(f, ref)
        faces.append((f, cell))
    return faces


def band(bm, z0, z1, radius, colour=LEATHER, sides=6):
    """A lashing: a short, slightly fatter ring around the shaft."""
    faces = []
    rings = [[bm.verts.new((radius * math.cos(2 * math.pi * i / sides), radius * math.sin(2 * math.pi * i / sides), z)) for i in range(sides)] for z in (z0, z1)]
    ref = Vector((0, 0, (z0 + z1) / 2))
    for i in range(sides):
        j = (i + 1) % sides
        f = bm.faces.new([rings[0][i], rings[0][j], rings[1][j], rings[1][i]])
        face_out(f, ref)
        faces.append((f, colour))
    for ring_pts in rings:
        f = bm.faces.new(ring_pts)
        face_out(f, ref)
        faces.append((f, colour))
    return faces


def head(bm, points, half_thickness, cap_colour=STONE, edge_colour=STONE_EDGE, sharp=None):
    """A flat head: the polygon `points` ((y, z) pairs) in the YZ plane, extruded along X.

    half_thickness: one value or one per point (the edge of a blade is thinner than its back).
    sharp: indices of the points whose neighbouring side faces are the sharpened edge (painted light)."""
    n = len(points)
    if not isinstance(half_thickness, (list, tuple)):
        half_thickness = [half_thickness] * n
    left = [bm.verts.new((-half_thickness[i], p[0], p[1])) for i, p in enumerate(points)]
    right = [bm.verts.new((half_thickness[i], p[0], p[1])) for i, p in enumerate(points)]
    ref = Vector((0, sum(p[0] for p in points) / n, sum(p[1] for p in points) / n))
    faces = []
    for ring_pts in (left, right):
        f = bm.faces.new(ring_pts)
        face_out(f, ref)
        faces.append((f, cap_colour))
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new([left[i], left[j], right[j], right[i]])
        face_out(f, ref)
        faces.append((f, STONE_LIGHT if sharp and i in sharp else edge_colour))
    return faces


def build_axe():
    bm = bmesh.new()
    faces = shaft(bm, -0.22, 0.26, 0.018, WOOD, knob=1.25)
    # The blade comes forward (-Y) from the haft, thin at the edge; the back of the head is a stubby poll.
    pts = [(0.04, 0.20), (-0.01, 0.185), (-0.07, 0.15), (-0.12, 0.165), (-0.135, 0.235), (-0.12, 0.31), (-0.07, 0.325), (-0.01, 0.29), (0.04, 0.28)]
    thick = [0.024, 0.026, 0.02, 0.005, 0.004, 0.005, 0.02, 0.026, 0.024]
    faces += head(bm, pts, thick, sharp={3, 4})
    faces += band(bm, 0.152, 0.185, 0.027)
    faces += band(bm, 0.285, 0.318, 0.027)
    return new_obj("StoneAxe", bm, faces)


def build_pickaxe():
    bm = bmesh.new()
    faces = shaft(bm, -0.24, 0.30, 0.018, WOOD, knob=1.25)
    # A double pointed bar across the haft, bent down at both ends.
    pts = [(-0.23, 0.245), (-0.12, 0.30), (0.12, 0.30), (0.23, 0.245), (0.13, 0.27), (0.0, 0.265), (-0.13, 0.27)]
    thick = [0.005, 0.022, 0.022, 0.005, 0.02, 0.02, 0.02]
    faces += head(bm, pts, thick, sharp={0, 3})
    faces += band(bm, 0.24, 0.30, 0.027)
    return new_obj("StonePickaxe", bm, faces)


def build_spear():
    bm = bmesh.new()
    faces = shaft(bm, -0.40, 0.50, 0.016, WOOD, taper=0.9, knob=1.3)
    # A leaf shaped flint point, lashed on top of the shaft.
    pts = [(0.0, 0.50), (-0.05, 0.56), (-0.065, 0.65), (0.0, 0.80), (0.065, 0.65), (0.05, 0.56)]
    thick = [0.014, 0.014, 0.012, 0.003, 0.012, 0.014]
    faces += head(bm, pts, thick, sharp={2, 3})
    faces += band(bm, 0.47, 0.53, 0.024)
    return new_obj("StoneSpear", bm, faces)


def build_sickle():
    bm = bmesh.new()
    faces = shaft(bm, -0.14, 0.14, 0.017, WOOD, knob=1.3)
    # A crescent blade curling forward from the top of the handle; its inner (hollow) side is the cutting edge.
    centre = Vector((-0.115, 0.17))
    outer, inner, thick = [], [], []
    steps = 6
    a0, a1 = math.radians(-8), math.radians(205)
    for i in range(steps + 1):
        t = i / steps
        a = a0 + (a1 - a0) * t
        width = 0.05 * (1.0 - 0.9 * t)
        r_out = 0.115 + width * 0.5
        r_in = 0.115 - width * 0.5
        outer.append((centre.x + r_out * math.cos(a), centre.y + r_out * math.sin(a)))
        inner.append((centre.x + r_in * math.cos(a), centre.y + r_in * math.sin(a)))
    pts = outer + list(reversed(inner))
    n = len(pts)
    thick = [0.011] * (steps + 1) + [0.004] * (steps + 1)
    faces += head(bm, pts, thick, sharp=set(range(steps + 1, n)))
    faces += band(bm, 0.12, 0.165, 0.025)
    return new_obj("StoneSickle", bm, faces)


BUILDERS = [build_pickaxe, build_axe, build_spear, build_sickle]


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, mesh_smooth_type="FACE",
                             add_leaf_bones=False, object_types={"MESH"})
    print("exported", path)


def render(obj, path, direction, tilt_degrees, size):
    """Renders the object alone, transparent, from `direction` with its up axis tilted (an icon) or not (a preview)."""
    scene = bpy.context.scene
    render_settings = scene.render
    shading = scene.display.shading
    for other in bpy.data.objects:
        other.hide_render = other is not obj and other.type == "MESH"
    original = obj.matrix_world.copy()
    obj.rotation_euler = (math.radians(tilt_degrees), 0, 0)
    bpy.context.view_layer.update()
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    centre = sum(corners, Vector()) / 8
    extent = max((max(c[i] for c in corners) - min(c[i] for c in corners)) for i in range(3))
    cam_data = bpy.data.cameras.new("IconCamera")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = extent * 1.22
    cam = bpy.data.objects.new("IconCamera", cam_data)
    scene.collection.objects.link(cam)
    cam.location = centre + Vector(direction).normalized() * 3.0
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    render_settings.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    shading.object_outline_color = (0.10, 0.06, 0.04)
    render_settings.resolution_x = render_settings.resolution_y = size
    render_settings.film_transparent = True
    render_settings.image_settings.file_format = "PNG"
    render_settings.image_settings.color_mode = "RGBA"
    render_settings.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print("rendered", path)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)
    obj.matrix_world = original
    obj.rotation_euler = (0, 0, 0)


def main():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    tools = [build() for build in BUILDERS]
    for obj in tools:
        export_fbx(obj, f"{EXPORT_DIR}/{obj.name}.fbx")
    for obj in tools:
        # From the right (+X) the profile of the head is seen; tilted so the tool lies diagonally, head to the upper right.
        render(obj, f"{ICON_DIR}/{obj.name}.png", (1, 0, 0), -45, 512)
        render(obj, f"{PREVIEW_DIR}/{obj.name}_side.png", (1, 0, 0), 0, 384)
        render(obj, f"{PREVIEW_DIR}/{obj.name}_front.png", (0, -1, 0.0), 0, 384)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


if __name__ == "__main__":
    main()
