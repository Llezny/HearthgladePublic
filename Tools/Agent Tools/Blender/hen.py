"""Low-poly hen for Hearthglade, built from rigid parts that are animated in Unity (replaces the third party Meshtint chicken).

Run headless:   blender -b --python "Tools/Agent Tools/Blender/hen.py"
The rig (Hen > Body > Neck > Head > Beak/Comb/Wattle/eyes, Body > Tail/WingL/WingR/LegL > FootL, ...) and the palette are described in
animal_common.py. The hen faces -Y; in Unity it looks along -Z after import (HenAssetBuilder turns it around). About 0.42 m tall.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import *  # noqa: E402,F401,F403

FBX_OUT = PROJECT + "/Assets/Arts/Models/Animals/Hen/Hen.fbx"
PREVIEW_DIR = os.environ.get("HEN_PREVIEW_DIR", "C:/Temp/hen")
TRI_BUDGET = 300

# Palette cells (row, col) from the top-left of the palette image.
BODY = (3, 0)     # FFF9EF warm white
WING = (0, 3)     # D3B396 light tan
TAIL = (2, 5)     # 947157 brown
RED = (6, 0)      # E5484D comb and wattle
BEAK = (5, 1)     # F5D65C light gold
LEG = (5, 0)      # E0A81E gold
DARK = (0, 0)     # 1F1513 eyes


def build_hen():
    rig = Rig("Hen")
    part = rig.part

    # ---- body: breast at -Y, rump rising towards the tail at +Y -----------------------------------------------------------
    body_joint = V((0, 0, 0.24))
    part("Body", lambda bm: tube(
        bm,
        [(V((0, -0.17, 0.25)), 0.06, 0.07),
         (V((0, -0.08, 0.245)), 0.105, 0.11),
         (V((0, 0.05, 0.25)), 0.105, 0.11),
         (V((0, 0.17, 0.285)), 0.06, 0.075)],
        8, start_cap="flat", end_cap="flat", colour=BODY), body_joint)

    # ---- neck, head, beak, comb, wattle, eyes -----------------------------------------------------------------------------
    neck_joint = V((0, -0.13, 0.29))
    head_joint = V((0, -0.165, 0.395))
    part("Neck", lambda bm: tube(
        bm, [(neck_joint, 0.05, 0.055), (V((0, -0.155, 0.345)), 0.036, 0.04), (head_joint, 0.034, 0.036)], 6, colour=BODY), neck_joint, "Body")
    part("Head", lambda bm: tube(
        bm, [(V((0, -0.15, 0.405)), 0.036, 0.038), (V((0, -0.195, 0.405)), 0.034, 0.034)], 6, start_cap="flat", end_cap="flat", colour=BODY),
        head_joint, "Neck")
    beak_joint = V((0, -0.2, 0.4))
    part("Beak", lambda bm: tube(
        bm, [(V((0, -0.195, 0.4)), 0.016, 0.014), (V((0, -0.245, 0.395)), 0.003, 0.003)], 4, start_cap="flat", colour=BEAK), beak_joint, "Head")
    comb_joint = V((0, -0.17, 0.435))
    part("Comb", lambda bm: tube(
        bm, [(V((0, -0.195, 0.437)), 0.008, 0.02), (V((0, -0.17, 0.445)), 0.008, 0.03), (V((0, -0.145, 0.437)), 0.008, 0.02)], 4,
        start_cap="flat", end_cap="flat", colour=RED), comb_joint, "Head")
    wattle_joint = V((0, -0.198, 0.385))
    part("Wattle", lambda bm: tube(
        bm, [(wattle_joint, 0.008, 0.008), (V((0, -0.2, 0.36)), 0.006, 0.006)], 4, start_cap="flat", end_cap="flat", colour=RED),
        wattle_joint, "Head")
    for side, name in ((1, "EyeL"), (-1, "EyeR")):
        eye = V((0.033 * side, -0.175, 0.408))
        part(name, lambda bm, eye=eye, side=side: diamond(bm, eye, side, DARK, 0.008), eye, "Head")

    # ---- tail and wings -----------------------------------------------------------------------------------------------------
    tail_joint = V((0, 0.17, 0.29))
    part("Tail", lambda bm: tube(
        bm, [(tail_joint, 0.012, 0.03), (V((0, 0.21, 0.34)), 0.012, 0.06), (V((0, 0.235, 0.4)), 0.008, 0.04)], 4, start_cap="flat", end_cap="flat",
        colour=TAIL), tail_joint, "Body")
    for side, name in ((1, "WingL"), (-1, "WingR")):
        joint = V((0.108 * side, -0.06, 0.268))
        part(name, lambda bm, side=side: tube(
            bm, [(V((0.112 * side, -0.07, 0.27)), 0.008, 0.06), (V((0.115 * side, 0.03, 0.265)), 0.008, 0.065), (V((0.1 * side, 0.14, 0.27)), 0.004, 0.035)],
            4, start_cap="flat", end_cap="flat", colour=WING, ring_colours={1: TAIL}), joint, "Body")

    # ---- legs and feet -------------------------------------------------------------------------------------------------------
    for side, leg, foot in ((1, "LegL", "FootL"), (-1, "LegR", "FootR")):
        hip = V((0.045 * side, 0.0, 0.16))
        ankle = V((0.045 * side, 0.0, 0.014))
        part(leg, lambda bm, hip=hip, ankle=ankle: tube(bm, [(hip, 0.016, 0.016), (ankle, 0.011, 0.011)], 4, start_cap="flat", colour=LEG), hip, "Body")
        part(foot, lambda bm, side=side: tube(
            bm, [(V((0.045 * side, 0.03, 0.008)), 0.02, 0.008), (V((0.045 * side, -0.07, 0.008)), 0.018, 0.006)], 4, start_cap="flat", end_cap="flat",
            colour=LEG), ankle, leg)

    print("hen tris", rig.tris)
    assert rig.tris <= TRI_BUDGET, f"{rig.tris} triangles"
    return rig.root


if __name__ == "__main__":
    clear_scene()
    export_fbx(build_hen(), FBX_OUT)
    render_previews(PREVIEW_DIR, "hen", V((0, 0, 0.22)), 0.7)
