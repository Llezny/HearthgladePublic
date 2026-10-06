"""Shared helpers for the crop model scripts (crops_batch1.py, ...).

Headless:  blender.exe -b --python Tools/Blender/crops_batch1.py
Conventions as in orchard_vineyard.py: metres, Z up, origin at the bottom centre of the plant, every face gets a flat UV
at a cell centre of the project palette (Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png), so Unity needs no
material of its own (Normal.mat). A bed cell is 0.3675 m wide (the model has to stay under ~0.30 m across).

Building blocks: Builder (one mesh), box, blob, cylinder, prism_path (tapered tube along a polyline), blade (a thin
lozenge-section strip that bends: grass blades, corn leaves, petals, leaflets, pods) and cap (a half-dome mushroom
shelf). Every shell is closed, so normals are recalculated once in finish().
"""
import math
import random

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

PALETTE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"
MODEL_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Farming/Crops"
ICON_RESOURCES = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Resources"
ICON_USABLE = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Usable"

# palette cells (row, col) -> hex
STEM_DARK = (0, 1)     # 513A31
BARK_A = (2, 5)        # 947157
BARK_B = (0, 2)        # 9D7C68
CUT_WOOD = (0, 3)      # D3B396
SAND = (0, 4)          # BEB296
GREY_DARK = (0, 6)     # 444D4B
GREY_LIGHT = (0, 7)    # CAC6B8
LIME = (1, 0)          # A6BC51
LIME_LIGHT = (1, 1)    # B7C873
LEAF_YG = (1, 2)       # 9AAB55
STRAW = (1, 3)         # ADA66D
PEACH = (1, 6)         # F5CFB7
PALE_YELLOW = (1, 7)   # F0F1B8
OLIVE_DARK = (2, 0)    # 45502D
OLIVE = (2, 1)         # 60703C
OLIVE_LIGHT = (2, 2)   # 899E52
OLIVE_MID = (2, 3)     # 6F803F
CREAM = (3, 0)         # FFF9EF
BROWN_DARK = (3, 1)    # 574A43
GREEN_DEEP = (3, 2)    # 505E35
MAGENTA = (3, 3)       # 820070
LILAC = (3, 4)         # C47BC4
GREEN_MID = (4, 1)     # 468450
RED_DARK = (4, 2)      # 9F3F3F
ORANGE = (4, 3)        # C5721D
GOLD = (5, 0)          # E0A81E
GOLD_LIGHT = (5, 1)    # F5D65C
GREY_MID = (5, 5)      # 7C8583
GREEN_LIGHT = (5, 6)   # 7FBF6A
GREEN_DARK = (5, 7)    # 2F6B3A
SOIL = (5, 4)          # 7A3B22
RED = (6, 0)           # E5484D
TAN = (6, 1)           # B5763F


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


class Builder:
    """One mesh under construction: a bmesh plus the palette cell of every face."""

    def __init__(self):
        self.bm = bmesh.new()
        self.coloured = []

    def face(self, verts, cell):
        face = self.bm.faces.new(verts)
        self.coloured.append((face, cell))
        return face

    def mark(self):
        return len(self.bm.verts)

    def transform_since(self, mark, matrix):
        """Applies a matrix to every vertex created after `mark` (to build a part upright, then tilt it into place)."""
        self.bm.verts.ensure_lookup_table()
        bmesh.ops.transform(self.bm, matrix=matrix, verts=list(self.bm.verts[mark:]))

    def finish(self, name, budget, location=(0.0, 0.0, 0.0)):
        uv_layer = self.bm.loops.layers.uv.new("UVMap")
        for face, cell in self.coloured:
            u, v = cell_uv(cell)
            for loop in face.loops:
                loop[uv_layer].uv = (u, v)
            face.smooth = False
        bmesh.ops.recalc_face_normals(self.bm, faces=list(self.bm.faces))
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.data.materials.append(palette_material())
        obj.location = location
        mesh.calc_loop_triangles()
        tris = len(mesh.loop_triangles)
        print(name, "tris", tris, "dims", tuple(round(v, 3) for v in obj.dimensions),
              "OVER BUDGET %d" % budget if tris > budget else "")
        return obj


def rot_matrix(rot):
    return Euler(rot, "XYZ").to_matrix().to_4x4()


# ---- primitives -----------------------------------------------------------------------------------------------------

def box(b, center, size, cell, rot=(0.0, 0.0, 0.0)):
    sx, sy, sz = (s / 2 for s in size)
    m = Euler(rot, "XYZ").to_matrix()
    verts = []
    for dz in (-1, 1):
        for dy in (-1, 1):
            for dx in (-1, 1):
                verts.append(b.bm.verts.new(Vector(center) + m @ Vector((dx * sx, dy * sy, dz * sz))))
    for idx in [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]:
        b.face([verts[i] for i in idx], cell)


def blob(b, center, radius, cells, scale=(1.0, 1.0, 1.0), subdiv=1, jitter=0.0, seed=0, rot=(0.0, 0.0, 0.0),
         by_normal=False):
    """A flat-shaded icosphere. Faces alternate between `cells` by height; by_normal=True gives cells[0] to the upper
    faces and cells[1] to the lower ones instead."""
    rng = random.Random(seed)
    before = set(b.bm.verts)
    bmesh.ops.create_icosphere(b.bm, subdivisions=subdiv, radius=radius)
    new_verts = [v for v in b.bm.verts if v not in before]
    m = Euler(rot, "XYZ").to_matrix()
    for v in new_verts:
        p = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
        if jitter:
            p += Vector((rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter)))
        v.co = m @ p + Vector(center)
    faces = {f for v in new_verts for f in v.link_faces}
    ordered = sorted(faces, key=lambda f: f.calc_center_median().z)
    for i, face in enumerate(ordered):
        if by_normal:
            cell = cells[0] if face.calc_center_median().z >= Vector(center).z else cells[1]
        else:
            cell = cells[i % len(cells)]
        b.coloured.append((face, cell))


def cylinder(b, center, radius, height, sides, cell_side, cell_top=None, cell_bottom=None, rot=(0.0, 0.0, 0.0),
             radius_top=None, twist=0.0):
    """A prism along local Z (radius_top makes a frustum); rotated about its centre."""
    m = Euler(rot, "XYZ").to_matrix()
    radius_top = radius if radius_top is None else radius_top
    bottom, top = [], []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        bottom.append(b.bm.verts.new(Vector(center) + m @ Vector((math.cos(a) * radius, math.sin(a) * radius, -height / 2))))
        top.append(b.bm.verts.new(Vector(center) + m @ Vector((math.cos(a + twist) * radius_top, math.sin(a + twist) * radius_top, height / 2))))
    side_cells = cell_side if isinstance(cell_side, list) else [cell_side]
    for i in range(sides):
        j = (i + 1) % sides
        b.face([bottom[i], bottom[j], top[j], top[i]], side_cells[i % len(side_cells)])
    b.face(list(reversed(bottom)), cell_bottom or side_cells[0])
    b.face(top, cell_top or side_cells[0])


def _frames(pts):
    """A tangent and two perpendicular axes per point of a polyline, without twisting."""
    frames = []
    prev_u = None
    n = len(pts)
    for i in range(n):
        if i == 0:
            t = (pts[1] - pts[0]).normalized()
        elif i == n - 1:
            t = (pts[-1] - pts[-2]).normalized()
        else:
            t = (pts[i + 1] - pts[i - 1]).normalized()
        if prev_u is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
            u = t.cross(ref).normalized()
        else:
            u = (prev_u - t * prev_u.dot(t)).normalized()
        v = t.cross(u).normalized()
        frames.append((t, u, v))
        prev_u = u
    return frames


def prism_path(b, pts, radii, sides, cell, cell_end=None, cap_start=True, cap_end=True, tip=False):
    """A tapered tube through a polyline. tip=True closes the last point to a single vertex (a spike)."""
    pts = [Vector(p) for p in pts]
    frames = _frames(pts)
    rings = []
    for p, (t, u, v), r in zip(pts, frames, radii):
        if r <= 0.0:
            rings.append([b.bm.verts.new(p)])
            continue
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            ring.append(b.bm.verts.new(p + u * (math.cos(a) * r) + v * (math.sin(a) * r)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        lo, hi = rings[i], rings[i + 1]
        for s in range(sides):
            s2 = (s + 1) % sides
            if len(lo) == 1:
                b.face([lo[0], hi[s2], hi[s]], cell)
            elif len(hi) == 1:
                b.face([lo[s], lo[s2], hi[0]], cell_end or cell)
            else:
                b.face([lo[s], lo[s2], hi[s2], hi[s]], cell)
    if cap_start and len(rings[0]) > 1:
        b.face(list(reversed(rings[0])), cell)
    if cap_end and len(rings[-1]) > 1:
        b.face(rings[-1], cell_end or cell)


def blade(b, base, azimuth, length, width, thick, rise, droop, cell, prof=(0.0, 1.0, 0.0), cell_under=None):
    """A strip with a flat lozenge cross-section that bends like a leaf.

    azimuth: heading in the XY plane (rad). rise: angle above the horizontal at the base (rad, negative = hanging).
    droop: how much that angle changes over the whole length (negative bends it down). prof: width multiplier per
    station (0 = a single point, so [0, 1, 0] is a pointed leaf of 8 triangles).
    """
    n = len(prof)
    heading = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
    side = Vector((-math.sin(azimuth), math.cos(azimuth), 0.0))
    seg = length / (n - 1)
    p = Vector(base)
    rings = []
    for i in range(n):
        if i > 0:
            a = rise + droop * ((i - 0.5) / (n - 1))
            p = p + (heading * math.cos(a) + Vector((0, 0, 1)) * math.sin(a)) * seg
        a_here = rise + droop * (i / (n - 1))
        fwd = heading * math.cos(a_here) + Vector((0, 0, 1)) * math.sin(a_here)
        up = fwd.cross(side).normalized()
        w = width * prof[i] / 2
        if prof[i] <= 0.0:
            rings.append([b.bm.verts.new(p)])
            continue
        rings.append([b.bm.verts.new(p - side * w), b.bm.verts.new(p + up * thick), b.bm.verts.new(p + side * w),
                      b.bm.verts.new(p - up * thick)])
    under = cell_under or cell
    for i in range(n - 1):
        lo, hi = rings[i], rings[i + 1]
        for k in range(4):
            k2 = (k + 1) % 4
            face_cell = cell if k < 2 else under
            if len(lo) == 1 and len(hi) == 1:
                continue
            if len(lo) == 1:
                b.face([lo[0], hi[k2], hi[k]], face_cell)
            elif len(hi) == 1:
                b.face([lo[k], lo[k2], hi[0]], face_cell)
            else:
                b.face([lo[k], lo[k2], hi[k2], hi[k]], face_cell)
    if len(rings[0]) == 4:
        b.face(list(reversed(rings[0])), under)
    if len(rings[-1]) == 4:
        b.face(rings[-1], cell)


def cap(b, base, azimuth, radius, height, cell_top, cell_alt, cell_gill, arc=6, tilt=0.0):
    """A half-dome mushroom shelf growing out of a vertical surface at `base`, pointing along `azimuth`."""
    mark = b.mark()
    arc_verts = []
    for i in range(arc + 1):
        phi = -math.pi / 2 + math.pi * i / arc
        arc_verts.append(b.bm.verts.new(Vector((math.cos(phi) * radius, math.sin(phi) * radius, 0.0))))
    apex = b.bm.verts.new(Vector((radius * 0.32, 0.0, height)))
    for i in range(arc):
        b.face([arc_verts[i], arc_verts[i + 1], apex], cell_top if i % 2 == 0 else cell_alt)
    b.face([arc_verts[-1], arc_verts[0], apex], cell_alt)
    b.face(list(reversed(arc_verts)), cell_gill)
    m = Matrix.Translation(Vector(base)) @ Matrix.Rotation(azimuth, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y")
    b.transform_since(mark, m)


# ---- export and preview -------------------------------------------------------------------------------------------

def export_fbx(objs, filepath):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=filepath, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
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


def render(objects, path, width=512, height=512, fill=1.0, direction=(0.263, -0.337, 0.139), ortho=False):
    """Same camera as the item icons: ~38 deg / 18 deg, framed on the bounding box of `objects`."""
    scene = bpy.context.scene
    bpy.context.view_layer.update()
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


def remove_default_cube():
    cube = bpy.data.objects.get("Cube")
    if cube is not None:
        bpy.data.objects.remove(cube, do_unlink=True)


# ---- small helpers shared by the plant scripts ----------------------------------------------------------------------


deg = math.radians
GOLDEN = 2.399963


def polar(radius, az, z=0.0):
    return Vector((math.cos(az) * radius, math.sin(az) * radius, z))


def bezier(p0, p1, p2, t):
    return p0 * ((1 - t) ** 2) + p1 * (2 * (1 - t) * t) + p2 * (t * t)


def bezier_pts(p0, p1, p2, steps):
    return [bezier(Vector(p0), Vector(p1), Vector(p2), i / steps) for i in range(steps + 1)]


def leaf(b, base, az, length, width, rise, droop, cell, under=None, thick=0.002):
    """A pointed lozenge leaf: 8 triangles."""
    blade(b, base, az, length, width, thick, rise, droop, cell, prof=(0.0, 1.0, 0.0), cell_under=under)
