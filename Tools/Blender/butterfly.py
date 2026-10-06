"""Low-poly butterflies for Hearthglade (three colours), built from rigid parts: a body and two wings that Unity flaps.

Run headless:   blender -b --python Tools/Blender/butterfly.py
The rig (Butterfly > Body > WingL/WingR) and the palette are described in animal_common.py. The butterfly faces -Y, its wings lie flat
in the XY plane and turn about the body axis (Y). About 0.14 m across.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import *  # noqa: E402,F401,F403

OUT_DIR = PROJECT + "/Assets/Arts/Models/Animals/Butterfly"
PREVIEW_DIR = os.environ.get("BUTTERFLY_PREVIEW_DIR", "C:/Temp/butterfly")
TRI_BUDGET = 80

# Palette cells (row, col) from the top-left of the palette image.
BODY = (0, 1)     # 513A31 dark brown
EDGE = (0, 0)     # 1F1513 wing tips
VARIANTS = {
    "Orange": (4, 3),   # C5721D
    "Blue": (1, 5),     # 60A3B6
    "Yellow": (5, 1),   # F5D65C
}


def wing(bm, side, inner, tip):
    """One wing as two flat polygons (the wing and its dark tip), each with a top and a bottom face so it shows from both sides."""
    def polygon(points, cell):
        faces = []
        for z, up in ((0.0012, True), (-0.0012, False)):
            verts = [bm.verts.new(V((x * side, y, z))) for x, y in (points if up else list(reversed(points)))]
            face = bm.faces.new(verts)
            face.normal_update()
            if (face.normal.z > 0) != up:
                face.normal_flip()
            faces.append((face, cell))
        return faces

    fore = polygon([(0.0, -0.006), (0.03, -0.036), (0.05, -0.005), (0.0, 0.004)], inner)
    fore_tip = polygon([(0.03, -0.036), (0.062, -0.03), (0.068, -0.004), (0.05, -0.005)], tip)
    hind = polygon([(0.0, 0.004), (0.05, -0.005), (0.046, 0.03), (0.016, 0.042), (0.0, 0.024)], inner)
    return fore + fore_tip + hind


def build_butterfly(colour):
    rig = Rig("Butterfly")
    part = rig.part
    body_joint = V((0, 0, 0))
    part("Body", lambda bm: tube(
        bm, [(V((0, -0.03, 0)), 0.004, 0.004), (V((0, -0.018, 0)), 0.007, 0.007), (V((0, 0.01, 0)), 0.006, 0.006), (V((0, 0.032, 0)), 0.002, 0.002)], 5,
        start_cap="flat", end_cap="flat", colour=BODY), body_joint)
    for side, name in ((1, "WingL"), (-1, "WingR")):
        joint = V((0.004 * side, 0, 0))
        part(name, lambda bm, side=side: wing(bm, side, colour, EDGE), joint, "Body")
    assert rig.tris <= TRI_BUDGET, f"{rig.tris} triangles"
    return rig


if __name__ == "__main__":
    for variant, colour in VARIANTS.items():
        clear_scene()
        rig = build_butterfly(colour)
        print("butterfly", variant, "tris", rig.tris)
        export_fbx(rig.root, f"{OUT_DIR}/Butterfly{variant}.fbx")
        if variant == "Orange":
            render_previews(PREVIEW_DIR, "butterfly", V((0, 0, 0)), 0.2)
