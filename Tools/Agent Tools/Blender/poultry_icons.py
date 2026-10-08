"""Item icons of the hunted food (docs/EQUIPMENT_PLAN.md, phase 7): a raw and a roasted poultry drumstick.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python poultry_icons.py
Writes RawPoultry.png and RoastedPoultry.png (512x512, transparent) into ICON_DIR, rendered the way stone_tools.py renders the tool icons
(Workbench, flat palette cells, a dark outline, tilted so the drumstick lies diagonally with the meat to the upper right).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import cell_uv, face_out, palette_material  # noqa: E402
import stone_tools  # noqa: E402

ICON_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Usable"

BONE = (3, 0)         # FFF9EF cream
RAW = (1, 6)          # F5CFB7 pale pink
RAW_DARK = (4, 2)     # 9F3F3F the darker flesh
ROAST = (4, 3)        # C5721D golden brown
ROAST_DARK = (5, 4)   # 7A3B22 the crisp skin
ROAST_LIGHT = (5, 0)  # E0A81E the glaze


def drumstick(name, flesh, flesh_spots):
    """A bone along +Z with a knuckle at the bottom and a teardrop of meat on top; flesh_spots: (cell, every n-th face) accents."""
    bm = bmesh.new()
    coloured = []
    # The bone.
    sides = 6
    rings = []
    for z in (0.0, 0.14):
        rings.append([bm.verts.new((0.017 * math.cos(2 * math.pi * i / sides), 0.017 * math.sin(2 * math.pi * i / sides), z)) for i in range(sides)])
    ref = Vector((0, 0, 0.07))
    for i in range(sides):
        j = (i + 1) % sides
        f = bm.faces.new([rings[0][i], rings[0][j], rings[1][j], rings[1][i]])
        face_out(f, ref)
        coloured.append((f, BONE))
    # The knuckle: two small balls side by side.
    for dx in (-0.016, 0.016):
        ball = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.022)
        for v in ball["verts"]:
            v.co += Vector((dx, 0, -0.005))
        coloured += [(f, BONE) for f in {f for v in ball["verts"] for f in v.link_faces}]
    # The meat: a sphere pulled into a teardrop that narrows down onto the bone.
    meat = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in meat["verts"]:
        x, y, z = v.co
        taper = 0.55 + 0.45 * (z + 1) / 2  # narrower at the bottom
        v.co = Vector((x * 0.072 * taper, y * 0.066 * taper, 0.215 + z * 0.1))
    faces = sorted({f for v in meat["verts"] for f in v.link_faces}, key=lambda f: f.index)
    for n, f in enumerate(faces):
        cell = flesh
        for spot, every in flesh_spots:
            if n % every == 0:
                cell = spot
        coloured.append((f, cell))
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
    return obj


def main():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    raw = drumstick("RawPoultry", RAW, [(RAW_DARK, 7)])
    roasted = drumstick("RoastedPoultry", ROAST, [(ROAST_DARK, 4), (ROAST_LIGHT, 9)])
    for obj in (raw, roasted):
        stone_tools.render(obj, f"{ICON_DIR}/{obj.name}.png", (1, 0, 0), -45, 512)


if __name__ == "__main__":
    main()
