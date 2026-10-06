"""Low-poly roe deer (a doe: no antlers) for Hearthglade, built from rigid parts that are animated in Unity.

Run headless:   blender -b --python "Tools/Agent Tools/Blender/deer.py"
The rig (Deer > Body > Neck > Head > EarL/EarR, Body > Tail, Body > FL_Up > FL_Low, ...) and the palette are described in
animal_common.py. The deer faces -Y; in Unity it looks along -Z after import (DeerAssetBuilder turns it around).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import *  # noqa: E402,F401,F403

FBX_OUT = PROJECT + "/Assets/Arts/Models/Animals/Deer/Deer.fbx"
PREVIEW_DIR = os.environ.get("DEER_PREVIEW_DIR", "C:/Temp/deer")
TRI_BUDGET = 340

# Palette cells (row, col) from the top-left of the palette image.
FUR = (6, 1)        # B5763F fawn (added 2026-09-21)
FUR_DARK = (5, 4)   # 7A3B22 dark reddish brown
CREAM = (3, 0)      # FFF9EF warm white: belly, rump patch, tail
DARK = (0, 0)       # 1F1513 nearly black: hooves, nose, eyes


def build_deer():
    rig = Rig("Deer")
    part = rig.part

    # ---- body: chest at -Y, rump at +Y ------------------------------------------------------------------------------------
    body_joint = V((0, 0, 0.62))
    part("Body", lambda bm: tube(
        bm,
        [(V((0, -0.40, 0.64)), 0.075, 0.09),
         (V((0, -0.25, 0.63)), 0.125, 0.165),
         (V((0, 0.02, 0.62)), 0.125, 0.155),
         (V((0, 0.27, 0.64)), 0.125, 0.16),
         (V((0, 0.41, 0.64)), 0.085, 0.11)],
        8, start_cap="flat", end_cap="flat", colour=FUR, belly=CREAM,
        ring_colours={"cap_start": FUR, "cap_end": CREAM}), body_joint)

    # ---- neck and head ---------------------------------------------------------------------------------------------------
    neck_joint = V((0, -0.27, 0.72))
    head_joint = V((0, -0.46, 0.99))
    part("Neck", lambda bm: tube(
        bm, [(neck_joint + V((0, 0.03, -0.02)), 0.085, 0.10), (neck_joint.lerp(head_joint, 0.5), 0.065, 0.075), (head_joint, 0.05, 0.055)],
        6, colour=FUR), neck_joint, "Body")

    part("Head", lambda bm: tube(
        bm, [(head_joint + V((0, 0.03, 0.0)), 0.058, 0.065), (head_joint + V((0, -0.06, -0.03)), 0.05, 0.055), (V((0, -0.66, 0.93)), 0.028, 0.03)],
        6, start_cap="flat", end_cap="flat", colour=FUR, ring_colours={"cap_end": DARK, 1: FUR_DARK}), head_joint, "Neck")
    for side, name in ((1, "EyeL"), (-1, "EyeR")):
        eye = V((0.052 * side, -0.525, 0.985))
        part(name, lambda bm, eye=eye, side=side: diamond(bm, eye, side, DARK), eye, "Head")

    # ---- ears ---------------------------------------------------------------------------------------------------------------
    for side, name in ((1, "EarL"), (-1, "EarR")):
        base = V((0.05 * side, -0.455, 1.04))
        tip = base + V((0.085 * side, 0.02, 0.125))
        part(name, lambda bm, base=base, tip=tip: tube(
            bm, [(base, 0.03, 0.012), (tip, 0.008, 0.005)], 4, start_cap="flat", colour=FUR), base, "Head")

    # ---- tail ---------------------------------------------------------------------------------------------------------------
    tail_joint = V((0, 0.41, 0.70))
    part("Tail", lambda bm: tube(
        bm, [(tail_joint, 0.03, 0.035), (tail_joint + V((0, 0.035, -0.03)), 0.022, 0.03)], 5, start_cap="flat", end_cap=tail_joint + V((0, 0.06, -0.075)),
        colour=CREAM), tail_joint, "Body")

    # ---- legs: shoulder / hip joint, knee joint, hoof on the ground --------------------------------------------------------
    legs = {
        "FL": (0.085, V((0, -0.24, 0.52)), V((0, -0.215, 0.29)), V((0, -0.235, 0.0))),
        "FR": (-0.085, V((0, -0.24, 0.52)), V((0, -0.215, 0.29)), V((0, -0.235, 0.0))),
        "BL": (0.09, V((0, 0.27, 0.53)), V((0, 0.31, 0.30)), V((0, 0.275, 0.0))),
        "BR": (-0.09, V((0, 0.27, 0.53)), V((0, 0.31, 0.30)), V((0, 0.275, 0.0))),
    }
    for leg, (x, hip, knee, hoof) in legs.items():
        hip, knee, hoof = hip + V((x, 0, 0)), knee + V((x, 0, 0)), hoof + V((x, 0, 0))
        hind = leg.startswith("B")
        part(leg + "_Up", lambda bm, hip=hip, knee=knee, hind=hind: tube(
            bm, [(hip, 0.05, 0.06 if hind else 0.05), (hip.lerp(knee, 0.5), 0.042, 0.05), (knee, 0.03, 0.032)], 4, start_cap="flat",
            colour=FUR), hip, "Body")
        part(leg + "_Low", lambda bm, knee=knee, hoof=hoof: tube(
            bm, [(knee, 0.03, 0.032), (hoof + V((0, 0, 0.045)), 0.02, 0.023), (hoof + V((0, -0.006, 0.0)), 0.026, 0.033)],
            4, start_cap="flat", end_cap="flat", colour=FUR_DARK, ring_colours={1: DARK}), knee, leg + "_Up")

    print("deer tris", rig.tris)
    assert rig.tris <= TRI_BUDGET, f"{rig.tris} triangles"
    return rig.root


if __name__ == "__main__":
    clear_scene()
    export_fbx(build_deer(), FBX_OUT)
    render_previews(PREVIEW_DIR, "deer", V((0, 0, 0.55)), 1.6)
