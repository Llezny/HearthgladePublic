"""Shared helpers of the animal model scripts (deer.py, hen.py): a rig of rigid parts, lofted tubes, palette colours, FBX export
and preview renders. Import it from a script run with `blender -b --python`:

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from animal_common import *

Every animal is built from rigid parts: each part is its own mesh whose origin is the joint it turns around, parented like the body
part it belongs to. There is no armature: Unity animates the joints with transform curves. Colours come from the project palette
(Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): every face gets a flat UV at the centre of one palette cell, so the model
needs no texture or material of its own in Unity. Metres, Z up, the animal faces -Y and stands on Z = 0.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

V = Vector
PROJECT = "E:/Repos/Hearthglade"
PALETTE = PROJECT + "/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"


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


def tube(bm, rings, sides, start_cap=None, end_cap=None, colour=None, belly=None, ring_colours=None):
    """A lofted tube through rings = [(centre, half width along X, half depth), ...] (world coordinates).

    The second axis of every ring is perpendicular to X and to the path. start_cap / end_cap: None = open, "flat" = one polygon,
    or a Vector = a point the cap comes to. colour: palette cell of the wall; belly: cell for wall faces looking down;
    ring_colours: {ring index: cell} for the wall segment that starts at that ring, "cap_start" / "cap_end": cap cells.
    Returns [(face, cell)].
    """
    axis_u = Vector((1, 0, 0))
    ring_verts = []
    for i, (centre, ru, rv) in enumerate(rings):
        prev_c = rings[max(i - 1, 0)][0]
        next_c = rings[min(i + 1, len(rings) - 1)][0]
        direction = (next_c - prev_c).normalized()
        axis_v = direction.cross(axis_u).normalized()
        verts = []
        for k in range(sides):
            ang = 2 * math.pi * (k + 0.5) / sides
            verts.append(bm.verts.new(centre + axis_u * ru * math.cos(ang) + axis_v * rv * math.sin(ang)))
        ring_verts.append(verts)

    result = []
    for i in range(len(rings) - 1):
        ref = (rings[i][0] + rings[i + 1][0]) / 2
        for k in range(sides):
            face = bm.faces.new([ring_verts[i][k], ring_verts[i][(k + 1) % sides], ring_verts[i + 1][(k + 1) % sides], ring_verts[i + 1][k]])
            face_out(face, ref)
            cell = (ring_colours or {}).get(i, colour)
            if belly is not None and face.normal.z < -0.4:
                cell = belly
            result.append((face, cell))

    middle = (rings[0][0] + rings[-1][0]) / 2
    for cap, ring, other, key in ((start_cap, ring_verts[0], rings[-1][0], "cap_start"), (end_cap, ring_verts[-1], rings[0][0], "cap_end")):
        if cap is None:
            continue
        cell = (ring_colours or {}).get(key, colour)
        if isinstance(cap, str):
            face = bm.faces.new(ring)
            face_out(face, other)
            result.append((face, cell))
        else:
            tip = bm.verts.new(cap)
            for k in range(sides):
                face = bm.faces.new([ring[k], ring[(k + 1) % sides], tip])
                face_out(face, middle)
                result.append((face, cell))
    return result


def diamond(bm, centre, side, colour, size=0.012):
    """A tiny four faced dark diamond looking outwards (side = +1 / -1 along X): an eye."""
    c = centre
    pts = [bm.verts.new(c + V((size * side, 0, 0))), bm.verts.new(c + V((0, size * 1.5, size))), bm.verts.new(c + V((0, size * 1.5, -size))),
           bm.verts.new(c + V((0, -size * 1.5, 0)))]
    faces = []
    for tri in ((0, 1, 2), (0, 3, 1), (0, 2, 3), (1, 3, 2)):
        f = bm.faces.new([pts[i] for i in tri])
        face_out(f, c - V((size * 2.5 * side, 0, 0)))
        faces.append((f, colour))
    return faces


class Rig:
    """The parts of one animal. part(name, builder, joint, parent) builds a mesh in world coordinates and hangs it under its parent."""

    def __init__(self, name):
        self.root = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(self.root)
        self.objects = {}
        self.joints = {}
        self.tris = 0

    def part(self, name, builder, joint, parent=None):
        bm = bmesh.new()
        coloured = builder(bm)
        for v in bm.verts:
            v.co -= joint
        uv_layer = bm.loops.layers.uv.new("UVMap")
        for face, cell in coloured:
            u, v = cell_uv(cell)
            for loop in face.loops:
                loop[uv_layer].uv = (u, v)
            face.smooth = False
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(palette_material())
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = self.objects[parent] if parent else self.root
        obj.location = joint - (self.joints[parent] if parent else V((0, 0, 0)))
        mesh.calc_loop_triangles()
        self.tris += len(mesh.loop_triangles)
        self.objects[name] = obj
        self.joints[name] = joint
        return obj


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def export_fbx(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    # bake_space_transform=True scrambles the local positions of children in a hierarchy; Unity bakes the axis conversion on import.
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, mesh_smooth_type="FACE",
                             add_leaf_bones=False, object_types={"EMPTY", "MESH"}, use_armature_deform_only=False)
    print("exported", path)


def render_previews(out_dir, prefix, target, ortho_scale):
    """Side, front and three quarter views (Workbench with palette colours) so the model can be judged without opening Blender."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    render = scene.render
    shading = scene.display.shading
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    render.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    render.resolution_x = render.resolution_y = 640
    render.film_transparent = False
    render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.55, 0.75, 0.55)
    views = {"side": V((1.0, 0, 0)), "front": V((0, -1.0, 0)), "three_quarter": V((0.7, -0.7, 0.35))}
    for name, direction in views.items():
        cam.location = target + direction.normalized() * 3.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        render.filepath = f"{out_dir}/{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
        print("rendered", render.filepath)
