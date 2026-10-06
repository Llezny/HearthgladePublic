"""Crop batch 1: Rye, Potato, Cabbage, Lingonberry, Raspberry, Pear, OysterMushroom, Peas, Sunflower, Corn (2026-10-04).

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python "Tools/Agent Tools/Blender/crops_batch1.py"

Per plant one FBX in Assets/Arts/Models/Environment/Farming/Crops/<Plant>.fbx holding separate objects named
<Plant>_Stage0..N (growth stages), <Plant>_Fruit0..N (hidden one by one as they are picked, perennials only) and
<Plant>_Wild (the world object the biomes spawn). The Unity builder (a temporary editor script) picks the meshes by
those names. Stage meshes have their base at z = 0, the builder lifts them onto the soil. Fruit meshes are already
placed in the plant's space (pivot at the plant origin), so the builder gives them the same transform as the stage.
Also renders the item icons (produce, pear sapling) and a preview sheet. Seed pouches: seed_pouch.py.

Sizes are chosen against the bed (0.30 m across, wheat is 0.24 m tall): stay under ~0.28 m across.
"""
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from crop_lib import *  # noqa: E402,F401,F403
from crop_trees import build_tree, collect_fronds, crown_spots  # noqa: E402


PREVIEW_DIR = "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/048c5bb8-968d-436a-97cb-3a7f1306ec78/scratchpad"

# ---- Rye -------------------------------------------------------------------------------------------------------------

def rye(stage, wild=False):
    b = Builder()
    r = random.Random(5)
    count = (7, 11, 11)[stage]
    for i in range(count):
        az = i * GOLDEN + r.uniform(-0.2, 0.2)
        radius = 0.012 + 0.055 * math.sqrt((i + 0.5) / count)
        base = polar(radius, az)
        if stage == 0:
            leaf_len = r.uniform(0.05, 0.08)
            blade(b, base, az, leaf_len, 0.013, 0.002, deg(78), -deg(40), OLIVE_LIGHT if i % 2 else LIME,
                  prof=(0.9, 1.0, 0.0))
            continue
        height = r.uniform(0.12, 0.19) if stage == 1 else r.uniform(0.22, 0.28)
        lean = r.uniform(0.008, 0.02) if stage == 1 else r.uniform(0.012, 0.03)
        mid = base + polar(lean * 0.3, az, height * 0.55)
        top = base + polar(lean, az, height)
        prism_path(b, [base, mid, top], [0.0058, 0.0042, 0.0032], 3, OLIVE_LIGHT if stage == 1 else STRAW)
        if stage == 2:
            d = (top - mid).normalized() + polar(0.22, az) + Vector((0, 0, -0.1))
            d.normalize()
            ear_len = r.uniform(0.07, 0.085)
            ear_pts = [top - d * 0.004, top + d * ear_len * 0.3, top + d * ear_len * 0.65, top + d * ear_len]
            prism_path(b, ear_pts, [0.0048, 0.0085, 0.0075, 0.0], 4, GOLD if i % 2 else GOLD_LIGHT, cell_end=GOLD_LIGHT)
            prism_path(b, [top + d * ear_len * 0.9, top + d * (ear_len + 0.032)], [0.002, 0.0], 3, STRAW)
        if i % 2 == 0:
            leaf_len = height * r.uniform(0.45, 0.6)
            cell = (OLIVE_MID, LIME)[i % 4 == 0] if stage == 1 else (STRAW, LEAF_YG)[i % 4 == 0]
            blade(b, base + polar(0.003, az, height * 0.25), az, leaf_len, 0.015, 0.002, deg(68), -deg(62), cell,
                  prof=(0.8, 1.0, 0.0))
    return b


# ---- Potato ----------------------------------------------------------------------------------------------------------

def potato_leaf(b, base, az, size, cell_a, cell_b, rise):
    tip = base + polar(size * 0.85, az, math.sin(rise) * size * 0.7)
    prism_path(b, [base, tip], [0.003, 0.002], 3, OLIVE, cap_end=False)
    leaf(b, tip, az, size * 0.8, size * 0.5, rise * 0.4, -0.5, cell_a)
    mid = base + (tip - base) * 0.6
    for sign in (-1, 1):
        leaf(b, mid, az + sign * 1.15, size * 0.55, size * 0.38, rise * 0.4, -0.45, cell_b)


def potato(stage, wild=False):
    b = Builder()
    r = random.Random(11)
    if stage == 0:
        for i in range(4):
            az = i * 1.6 + 0.3
            potato_leaf(b, polar(0.012, az, 0.012), az, 0.045, OLIVE_MID, GREEN_MID, deg(40))
        prism_path(b, [Vector((0, 0, 0)), Vector((0, 0, 0.03))], [0.004, 0.003], 3, OLIVE)
        return b
    stems = 5 if stage == 1 else 6
    a_cell, b_cell = (OLIVE_MID, GREEN_MID) if stage == 1 else (OLIVE_LIGHT, LEAF_YG)
    for i in range(stems):
        az = i * (2 * math.pi / stems) + r.uniform(-0.2, 0.2)
        height = r.uniform(0.09, 0.12) if stage == 1 else r.uniform(0.12, 0.16)
        out = 0.038 if stage == 1 else 0.048
        base = polar(0.012, az)
        top = polar(out, az, height)
        mid = polar(out * 0.4, az, height * 0.55)
        prism_path(b, [base, mid, top], [0.0055, 0.0042, 0.003], 3, OLIVE)
        potato_leaf(b, mid, az + 0.35, 0.06, a_cell, b_cell, deg(35))
        potato_leaf(b, top, az - 0.2, 0.055, b_cell, a_cell, deg(28))
        if stage == 2 and i % 2 == 0:
            f = top + Vector((0, 0, 0.012))
            box(b, f, (0.02, 0.02, 0.008), CREAM, rot=(0, 0, az))
            box(b, f + Vector((0, 0, 0.005)), (0.008, 0.008, 0.006), GOLD_LIGHT)
    if stage == 2:
        for i, (az, rad, size) in enumerate(((0.4, 0.058, 0.03), (2.3, 0.052, 0.027), (4.2, 0.064, 0.029))):
            blob(b, polar(rad, az, 0.016), size, (TAN, CUT_WOOD), scale=(1.0, 0.8, 0.62), subdiv=1, jitter=0.003,
                 seed=i + 3, rot=(0, 0, az))
    return b


# ---- Cabbage ---------------------------------------------------------------------------------------------------------

def cabbage(stage, wild=False):
    b = Builder()
    r = random.Random(23)
    if stage == 0:
        for i in range(4):
            az = i * math.pi / 2 + 0.4
            blade(b, polar(0.01, az, 0.01), az, 0.055, 0.05, 0.002, deg(35), -deg(55), OLIVE_MID,
                  prof=(0.3, 1.0, 0.8, 0.0), cell_under=OLIVE)
        return b
    if stage == 1:
        for i in range(7):
            az = i * (2 * math.pi / 7)
            blade(b, polar(0.012, az, 0.012), az, 0.1, 0.08, 0.002, deg(38), -deg(60),
                  (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[i % 3], prof=(0.3, 1.0, 0.9, 0.0), cell_under=OLIVE)
        blob(b, Vector((0, 0, 0.032)), 0.03, (LEAF_YG, LIME), scale=(1, 1, 0.9), subdiv=1)
        return b
    # mature: a round head wrapped by outer leaves
    blob(b, Vector((0, 0, 0.062)), 0.072, (LIME_LIGHT, LIME, LEAF_YG), scale=(1, 1, 0.9), subdiv=1, jitter=0.002, seed=7)
    for i in range(7):
        az = i * (2 * math.pi / 7) + 0.2
        blade(b, polar(0.03, az, 0.02), az, 0.105, 0.095, 0.003, deg(20), -deg(35),
              (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[i % 3], prof=(0.4, 1.0, 0.95, 0.0), cell_under=OLIVE)
    for i in range(6):
        az = i * (2 * math.pi / 6) + 0.55
        blade(b, polar(0.02, az, 0.045), az, 0.1, 0.085, 0.003, deg(62), -deg(65),
              (LIME, LIME_LIGHT, LEAF_YG)[i % 3], prof=(0.4, 1.0, 0.9, 0.0), cell_under=OLIVE_LIGHT)
    return b


# ---- Lingonberry -----------------------------------------------------------------------------------------------------

LINGON_TWIGS = [(0.3, 0.14, 0.05), (1.45, 0.16, 0.06), (2.6, 0.13, 0.055), (3.75, 0.15, 0.05), (4.9, 0.12, 0.045),
                (0.9, 0.1, 0.03)]


def lingon_twig(b, az, height, out, leaves, rng):
    base = polar(0.012, az)
    top = polar(out, az, height)
    mid = polar(out * 0.3, az, height * 0.58)
    prism_path(b, [base, mid, top], [0.0048, 0.0036, 0.0022], 3, STEM_DARK, cell_end=OLIVE_DARK)
    for k in range(leaves):
        t = 0.3 + 0.7 * k / max(1, leaves - 1)
        point = bezier(base, mid, top, t)
        leaf_az = az + (1.0 if k % 2 else -1.0) * (1.1 + rng.uniform(-0.25, 0.25))
        leaf(b, point, leaf_az, 0.036, 0.02, deg(35), -0.5, (GREEN_DARK, GREEN_MID, OLIVE_MID)[k % 3], under=OLIVE_DARK)
    return top


def lingonberry(stage, wild=False, fruits=None):
    """Stage 0 young shrub, stage 1 the full one. fruits = None: no berries; else the cluster positions to bake in."""
    b = Builder()
    r = random.Random(31)
    tips = []
    if stage == 0:
        for az, height, out in LINGON_TWIGS[:3]:
            lingon_twig(b, az, height * 0.5, out * 0.5, 3, r)
        return b, tips
    for az, height, out in LINGON_TWIGS:
        tips.append(lingon_twig(b, az, height, out, 4, r))
    if fruits:
        for k, pos in enumerate(fruits):
            berry_cluster(b, pos, k)
    return b, tips


def berry_cluster(b, pos, seed):
    offsets = ((0.0, 0.0, 0.0), (0.018, 0.004, 0.006), (-0.008, 0.017, 0.004))
    for i, o in enumerate(offsets):
        blob(b, Vector(pos) + Vector(o), 0.0125, (RED, RED_DARK), subdiv=0, seed=seed + i)


def lingon_fruit_positions():
    _, tips = lingonberry(1)
    picks = (0, 1, 2, 3)
    out = []
    for i in picks:
        t = tips[i]
        out.append(t + Vector((0.0, 0.0, -0.016)))
    return out


# ---- Raspberry -------------------------------------------------------------------------------------------------------

RASP_CANES = [(0.2, 0.30, 0.095), (1.9, 0.28, 0.09), (3.5, 0.31, 0.1), (5.0, 0.26, 0.085)]


def raspberry_cane(b, az, height, out, rng, leaf_groups):
    base = polar(0.014, az)
    mid = polar(out * 0.25, az, height * 0.78)
    top = polar(out, az, height)
    pts = bezier_pts(base, mid, top, 3)
    prism_path(b, pts, [0.0065, 0.0055, 0.0042, 0.003], 3, OLIVE_DARK, cell_end=STEM_DARK)
    for k in range(leaf_groups):
        t = 0.38 + 0.62 * k / max(1, leaf_groups - 1)
        point = bezier(base, mid, top, t)
        g = az + (0.75 if k % 2 else -0.75)
        size = 0.058 - 0.008 * k
        leaf(b, point, az + 0.0, size, size * 0.62, deg(32), -0.45, (OLIVE_MID, GREEN_MID, GREEN_DARK)[k % 3], under=OLIVE_DARK)
        for sign in (-1, 1):
            leaf(b, point, az + sign * 1.25, size * 0.8, size * 0.5, deg(28), -0.4,
                 (GREEN_MID, OLIVE_MID, GREEN_DARK)[(k + 1) % 3], under=OLIVE_DARK)
    return top


def raspberry(stage, wild=False, fruits=None):
    b = Builder()
    r = random.Random(37)
    tips = []
    if stage == 0:
        for az, height, out in RASP_CANES[:2]:
            raspberry_cane(b, az, height * 0.38, out * 0.4, r, 2)
        return b, tips
    for az, height, out in RASP_CANES:
        tips.append(raspberry_cane(b, az, height, out, r, 3))
    if fruits:
        for k, pos in enumerate(fruits):
            raspberry_berry(b, pos, k)
    return b, tips


def raspberry_berry(b, pos, seed):
    blob(b, Vector(pos), 0.021, (RED_DARK, MAGENTA, RED), scale=(1.0, 1.0, 1.15), subdiv=1, jitter=0.0025, seed=seed + 4)
    box(b, Vector(pos) + Vector((0, 0, 0.024)), (0.026, 0.026, 0.005), GREEN_MID, rot=(0, 0, 0.4 + seed))


def raspberry_fruit_positions():
    _, tips = raspberry(1)
    out = []
    for i in (0, 1, 2):
        t = tips[i]
        out.append(t + Vector((0.0, 0.0, -0.032)))
    return out


# ---- Pear tree (the Pine's own foliage sprays, like apple_tree.py, but taller and narrower) -------------------------

PEAR = dict(seed=9, trunk_top=0.25, limb_angles=[30.0, 118.0, 205.0, 295.0], cz=0.62, rx=0.29, rz=0.36,
            layers=[-0.30, -0.20, -0.10, 0.0, 0.10, 0.20], target_height=0.70, limb_reach=(0.09, 0.13), limb_top=(0.46, 0.56),
            bias=0.16)
PEAR_LEAVES = (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)


def pear_fruit_positions(k):
    return crown_spots(PEAR, k, (-0.26, -0.16, -0.05, 0.05, -0.2, 0.0), 15)


def pear_fruit(center=Vector((0, 0, 0)), seed=0):
    """One tapered pear, bulb at the bottom, origin at the middle of the bulb."""
    b = Builder()
    c = Vector(center)
    zs = (-0.026, -0.018, 0.0, 0.018, 0.034, 0.048)
    rs = (0.0, 0.02, 0.029, 0.021, 0.0135, 0.0095)
    prism_path(b, [c + Vector((0, 0, z)) for z in zs], rs, 7, LIME_LIGHT if seed % 2 == 0 else LEAF_YG, cell_end=STEM_DARK)
    prism_path(b, [c + Vector((0, 0, 0.046)), c + Vector((0.003, 0, 0.068))], [0.0035, 0.0028], 3, STEM_DARK)
    return b


# ---- Oyster mushroom: a log with shelf caps -------------------------------------------------------------------------

LOG_R = 0.05
LOG_H = 0.13


def oyster_log(b):
    cylinder(b, Vector((0, 0, LOG_H / 2)), LOG_R, LOG_H, 8, [BARK_A, BARK_B], cell_top=CUT_WOOD)


def oyster_cap(b, az, z, radius, height=None, tilt=-0.18):
    cap(b, polar(LOG_R * 0.92, az, z), az, radius, height or radius * 0.55, GREY_MID, SAND, GREY_LIGHT, arc=6, tilt=tilt)


OYSTER_CAPS = [(0.4, 0.075, 0.052), (0.55, 0.043, 0.04), (2.3, 0.085, 0.048), (2.1, 0.052, 0.036),
               (4.1, 0.065, 0.05), (4.3, 0.1, 0.034)]


def oyster(stage, wild=False):
    b = Builder()
    oyster_log(b)
    if stage == 0:
        for i, (az, z) in enumerate(((0.5, 0.07), (2.4, 0.09), (4.2, 0.055))):
            blob(b, polar(LOG_R * 0.98, az, z), 0.01, (CREAM, GREY_LIGHT), subdiv=0, seed=i)
        return b
    if wild:
        for az, z, radius in OYSTER_CAPS:
            oyster_cap(b, az, z, radius)
        return b
    for az, z, radius in ((1.3, 0.085, 0.02), (3.4, 0.05, 0.018)):
        oyster_cap(b, az, z, radius)
    return b


def oyster_fruit(index):
    b = Builder()
    pairs = [OYSTER_CAPS[0], OYSTER_CAPS[2], OYSTER_CAPS[4]]
    az, z, radius = pairs[index]
    oyster_cap(b, az, z, radius)
    return b


# ---- Peas ------------------------------------------------------------------------------------------------------------

def pea_vine(b, base, top, plane_az, rng, stage, wild=False):
    """One climbing stem. plane_az: heading of the leaves, perpendicular to the trellis plane (+/-)."""
    base, top = Vector(base), Vector(top)
    mid = (base + top) / 2
    wiggle = Vector((math.cos(plane_az), math.sin(plane_az), 0)) * 0.012
    pts = [base, base + (top - base) * 0.33 + wiggle, base + (top - base) * 0.66 - wiggle, top]
    prism_path(b, pts, [0.0045, 0.004, 0.0034, 0.0026], 3, GREEN_MID, cell_end=OLIVE)
    n_leaves = 2 if stage == 0 else 5
    for k in range(n_leaves):
        t = 0.3 + 0.7 * k / max(1, n_leaves - 1) if stage else 0.7
        point = base + (top - base) * t
        side = 1.0 if k % 2 == 0 else -1.0
        az = plane_az + (0.0 if side > 0 else math.pi) + rng.uniform(-0.3, 0.3)
        size = 0.044 if stage == 0 else 0.056
        leaf(b, point, az, size, size * 0.72, deg(20), -0.5, (GREEN_LIGHT, OLIVE_LIGHT, GREEN_MID)[k % 3], under=GREEN_MID)
        leaf(b, point + Vector((0, 0, 0.012)), az + 0.7 * side, size * 0.8, size * 0.55, deg(28), -0.45, (OLIVE_LIGHT, GREEN_LIGHT)[k % 2], under=GREEN_MID)
    if stage >= 1:
        for k in range(3 if stage == 1 else 2):
            t = 0.45 + 0.17 * k
            point = base + (top - base) * t + wiggle * (0.6 if k % 2 else -0.6)
            box(b, point + Vector((0, 0, 0.01)), (0.016, 0.016, 0.012), CREAM, rot=(0, 0, plane_az + k))
    if stage == 2:
        for k in range(3):
            t = 0.36 + 0.2 * k
            point = base + (top - base) * t
            side = 1.0 if (k + int(base.x * 100)) % 2 == 0 else -1.0
            az = plane_az + (0.0 if side > 0 else math.pi)
            blade(b, point + Vector((math.cos(az), math.sin(az), 0)) * 0.014, az + rng.uniform(-0.3, 0.3), 0.095, 0.03, 0.01,
                  -deg(55), deg(30), GREEN_LIGHT, prof=(0.35, 1.0, 0.9, 0.0), cell_under=GREEN_MID)


def peas(stage, wild=False):
    b = Builder()
    rng = random.Random(53)
    if not wild:
        heights = (0.075, 0.3, 0.44)
        for x in (-0.1, 0.0, 0.1):
            h = heights[stage] * rng.uniform(0.9, 1.05)
            pea_vine(b, Vector((x, 0.0, 0.0)), Vector((x + rng.uniform(-0.01, 0.01), 0.0, h)), math.pi / 2, rng, stage)
        return b
    # wild: three leaning sticks with the vines twisted around them
    apex = Vector((0.0, 0.0, 0.36))
    for i in range(3):
        az = i * (2 * math.pi / 3) + 0.4
        foot = polar(0.1, az)
        prism_path(b, [foot, foot + (apex - foot) * 0.5, apex], [0.007, 0.006, 0.004], 4, BARK_A, cell_end=BARK_B)
        pea_vine(b, foot + polar(0.012, az + 1.4), apex * 0.92 + polar(0.014, az + 1.4), az + math.pi / 2, rng, 2)
    return b


# ---- Sunflower -------------------------------------------------------------------------------------------------------

def sunflower_head(b, neck, az, tilt, size=1.0, open_flower=True):
    mark = b.mark()
    if open_flower:
        cylinder(b, Vector((0, 0, 0)), 0.062 * size, 0.02 * size, 10, BROWN_DARK, cell_top=STEM_DARK)
        cylinder(b, Vector((0, 0, 0.012 * size)), 0.04 * size, 0.008 * size, 8, BROWN_DARK, cell_top=ORANGE)
        for i in range(14):
            phi = 2 * math.pi * i / 14
            blade(b, polar(0.052 * size, phi, 0.002), phi, 0.06 * size, 0.04 * size, 0.003, 0.0, -deg(8),
                  GOLD_LIGHT if i % 2 else GOLD, prof=(0.45, 1.0, 0.6, 0.0), cell_under=GOLD)
        for i in range(7):
            phi = 2 * math.pi * (i + 0.5) / 7
            blade(b, polar(0.05 * size, phi, -0.004), phi, 0.04 * size, 0.022 * size, 0.003, -deg(25), -deg(10),
                  GREEN_MID, prof=(0.6, 1.0, 0.0), cell_under=GREEN_DARK)
    else:
        blob(b, Vector((0, 0, 0.012)), 0.034, (GREEN_MID, GREEN_LIGHT), scale=(1, 1, 0.85), subdiv=1)
        for i in range(8):
            phi = 2 * math.pi * i / 8
            blade(b, polar(0.016, phi, 0.0), phi, 0.045, 0.026, 0.003, deg(58), -deg(40), GREEN_MID if i % 2 else GREEN_DARK,
                  prof=(0.7, 1.0, 0.0), cell_under=GREEN_DARK)
        box(b, Vector((0, 0, 0.044)), (0.02, 0.02, 0.01), GOLD)
    b.transform_since(mark, Matrix.Translation(Vector(neck)) @ Matrix.Rotation(az, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y"))


def sunflower(stage, wild=False):
    b = Builder()
    r = random.Random(61)
    if stage == 0:
        for i in range(2):
            az = i * math.pi + 0.5
            blade(b, polar(0.006, az, 0.012), az, 0.05, 0.034, 0.002, deg(28), -deg(40), GREEN_LIGHT, prof=(0.3, 1.0, 0.7, 0.0),
                  cell_under=GREEN_MID)
        prism_path(b, [Vector((0, 0, 0)), Vector((0, 0, 0.022))], [0.005, 0.004], 4, GREEN_MID)
        return b
    height = 0.2 if stage == 1 else 0.4
    az_head = 0.6
    lean = 0.02 if stage == 1 else 0.05
    pts = bezier_pts(Vector((0, 0, 0)), polar(0.0, az_head, height * 0.6), polar(lean, az_head, height), 3)
    prism_path(b, pts, [0.0135, 0.0115, 0.0095, 0.0085], 6, OLIVE_MID, cell_end=OLIVE_DARK)
    leaf_total = 4 if stage == 1 else 5
    for k in range(leaf_total):
        t = 0.18 + 0.6 * k / (leaf_total - 1)
        point = bezier(pts[0], pts[1], pts[3], t)
        az = k * GOLDEN + 0.4
        length = (0.085 if stage == 1 else 0.11) * (1.0 - 0.25 * k / leaf_total)
        blade(b, point, az, length, length * 0.85, 0.003, deg(24), -deg(48), (GREEN_MID, OLIVE_MID, GREEN_DARK)[k % 3],
              prof=(0.2, 0.85, 1.0, 0.45, 0.0), cell_under=OLIVE_DARK)
    sunflower_head(b, pts[3], az_head, deg(36) if stage == 2 else deg(10), open_flower=(stage == 2))
    return b


# ---- Corn ------------------------------------------------------------------------------------------------------------

def corn(stage, wild=False):
    b = Builder()
    r = random.Random(67)
    if stage == 0:
        for i in range(3):
            az = i * 2.1 + 0.2
            blade(b, polar(0.008, az, 0.0), az, 0.07, 0.016, 0.002, deg(72), -deg(45), LIME if i % 2 else OLIVE_LIGHT,
                  prof=(0.9, 1.0, 0.0))
        return b
    height = 0.2 if stage == 1 else 0.4
    pts = bezier_pts(Vector((0, 0, 0)), Vector((0.004, 0.0, height * 0.55)), Vector((0.012, 0.004, height)), 3)
    prism_path(b, pts, [0.0125, 0.0105, 0.0088, 0.0075], 6, OLIVE_LIGHT, cell_end=OLIVE)
    leaves = 5 if stage == 1 else 8
    for k in range(leaves):
        t = 0.12 + 0.72 * k / (leaves - 1)
        point = bezier(pts[0], pts[1], pts[3], t)
        az = k * GOLDEN + 0.3
        length = (0.11 if stage == 1 else 0.19) * (1.0 - 0.3 * abs(t - 0.45))
        blade(b, point, az, length, 0.034 if stage == 2 else 0.028, 0.0025, deg(52), -deg(105), (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[k % 3],
              prof=(0.35, 1.0, 1.0, 0.7, 0.0), cell_under=OLIVE)
    if stage == 2:
        for az, z in ((0.9, 0.23), (4.2, 0.17)):
            start = pts[1] + Vector((0, 0, z - pts[1].z))
            start = Vector((polar(0.012, az).x, polar(0.012, az).y, z))
            out = polar(0.05, az, z + 0.07)
            cob_pts = [start, start + (out - start) * 0.4, start + (out - start) * 0.8, out]
            prism_path(b, cob_pts, [0.012, 0.0215, 0.0195, 0.0125], 6, LIME, cell_end=GOLD_LIGHT)
            for sign in (-1, 1):
                blade(b, out, az + sign * 0.5, 0.03, 0.012, 0.002, deg(60), -0.5, TAN, prof=(0.5, 1.0, 0.0))
        top = pts[3]
        for i in range(6):
            phi = i * (2 * math.pi / 6)
            blade(b, top, phi, 0.07, 0.008, 0.0015, deg(66), -deg(28), STRAW if i % 2 else TAN, prof=(0.6, 1.0, 0.0))
        blade(b, top, 0.0, 0.07, 0.008, 0.0015, deg(88), 0.0, TAN, prof=(0.6, 1.0, 0.0))
    return b


# ---- item icons -------------------------------------------------------------------------------------------------------

def icon_rye():
    b = Builder()
    for i, az in enumerate((-0.3, 0.0, 0.3)):
        s = math.sin(az)
        top = Vector((s * 0.22, 0.0, 0.2))
        prism_path(b, [Vector((0, 0, 0)), Vector((s * 0.11, 0, 0.1)), top], [0.0085, 0.0075, 0.006], 4, STRAW)
        d = Vector((s * 0.5, 0, 1)).normalized()
        prism_path(b, [top - d * 0.004, top + d * 0.024, top + d * 0.052, top + d * 0.08], [0.008, 0.0145, 0.0125, 0.0], 4,
                   GOLD if i % 2 else GOLD_LIGHT, cell_end=GOLD_LIGHT)
        prism_path(b, [top + d * 0.07, top + d * 0.115], [0.0028, 0.0], 3, STRAW)
    box(b, Vector((0, 0, 0.07)), (0.06, 0.04, 0.016), TAN)
    return b


def icon_potato():
    b = Builder()
    blob(b, Vector((-0.04, 0.005, 0.034)), 0.06, (TAN, CUT_WOOD), scale=(1.0, 0.8, 0.66), subdiv=1, jitter=0.004, seed=2, rot=(0, 0, 0.5))
    blob(b, Vector((0.05, -0.02, 0.03)), 0.055, (CUT_WOOD, TAN), scale=(1.0, 0.82, 0.66), subdiv=1, jitter=0.004, seed=4, rot=(0, 0, -0.6))
    blob(b, Vector((0.005, 0.045, 0.026)), 0.044, (TAN, CUT_WOOD), scale=(1.0, 0.8, 0.66), subdiv=1, jitter=0.004, seed=6, rot=(0, 0, 1.1))
    return b


def icon_lingonberry():
    b = Builder()
    spots = ((0, 0, 0.0), (0.04, 0.01, 0.004), (-0.04, 0.012, 0.0), (0.02, -0.036, 0.004), (-0.02, -0.032, 0.002),
             (0.0, 0.04, 0.012))
    for i, o in enumerate(spots):
        blob(b, Vector(o) + Vector((0, 0, 0.026)), 0.026, (RED, RED_DARK), subdiv=1, seed=i)
    leaf(b, Vector((0.01, 0.0, 0.05)), 0.7, 0.09, 0.05, deg(25), -0.5, GREEN_DARK, under=OLIVE_DARK, thick=0.004)
    leaf(b, Vector((-0.01, 0.0, 0.05)), 2.4, 0.08, 0.045, deg(20), -0.5, GREEN_MID, under=OLIVE_DARK, thick=0.004)
    return b


def icon_raspberry():
    b = Builder()
    for i, o in enumerate(((0, 0, 0), (0.07, 0.0, 0.0), (0.035, -0.06, 0.0))):
        blob(b, Vector(o) + Vector((0, 0, 0.04)), 0.04, (RED_DARK, MAGENTA, RED), scale=(1, 1, 1.15), subdiv=1, jitter=0.005, seed=i + 5)
        box(b, Vector(o) + Vector((0, 0, 0.086)), (0.05, 0.05, 0.008), GREEN_MID, rot=(0, 0, 0.5 + i))
    leaf(b, Vector((0.035, 0.02, 0.07)), 1.2, 0.1, 0.06, deg(15), -0.45, GREEN_DARK, under=OLIVE_DARK, thick=0.004)
    return b


def icon_pear():
    b = pear_fruit(Vector((0, 0, 0.03)), 0)
    leaf(b, Vector((0.004, 0.0, 0.09)), 0.4, 0.07, 0.04, deg(14), -0.3, GREEN_MID, under=OLIVE_DARK, thick=0.004)
    return b


def icon_oyster():
    b = Builder()
    cylinder(b, Vector((0, 0, 0.055)), 0.04, 0.11, 8, [BARK_A, BARK_B], cell_top=CUT_WOOD)
    for az, z, radius in ((0.3, 0.07, 0.075), (2.6, 0.092, 0.065), (4.5, 0.04, 0.068)):
        cap(b, polar(0.036, az, z), az, radius, radius * 0.55, GREY_MID, SAND, GREY_LIGHT, arc=6, tilt=-0.18)
    return b


def icon_peas():
    b = Builder()
    blade(b, Vector((-0.09, 0.0, 0.016)), 0.0, 0.18, 0.055, 0.014, 0.0, 0.0, GREEN_LIGHT,
          prof=(0.35, 1.0, 1.0, 0.7, 0.0), cell_under=GREEN_MID)
    for i, x in enumerate((-0.05, -0.012, 0.026, 0.062)):
        blob(b, Vector((x, 0.0, 0.034)), 0.0215, (LIME, GREEN_LIGHT), subdiv=1, seed=i)
    return b


def icon_sunflower():
    b = Builder()
    sunflower_head(b, Vector((0, 0, 0.0)), -0.9, deg(72), size=1.4)
    return b


def icon_corn():
    b = Builder()
    for i in range(6):
        cylinder(b, Vector((0, 0, 0.034 + i * 0.026)), 0.03 - 0.0014 * abs(i - 2), 0.026, 8, [GOLD_LIGHT, GOLD], cell_top=GOLD_LIGHT,
                 twist=0.2 * (i % 2))
    for az in (0.4, 2.5, 4.6):
        blade(b, polar(0.01, az, 0.0), az, 0.12, 0.06, 0.004, deg(42), -deg(28), LIME, prof=(0.3, 1.0, 1.0, 0.0),
              cell_under=LEAF_YG)
    return b


def icon_cabbage():
    return cabbage(2)


def icon_pear_sapling():
    b = Builder()
    blob(b, Vector((0, 0, 0.012)), 0.06, (SOIL, STEM_DARK), scale=(1.0, 1.0, 0.45), subdiv=1)
    prism_path(b, [Vector((0, 0, 0.012)), Vector((0.004, 0, 0.1)), Vector((0, 0, 0.2))], [0.009, 0.008, 0.006], 5, BARK_A, cell_end=BARK_B)
    for z, az, size, cell in ((0.09, 0.2, 0.07, GREEN_MID), (0.13, 3.4, 0.07, OLIVE_MID), (0.17, 1.8, 0.06, GREEN_MID),
                              (0.2, 4.9, 0.055, OLIVE_LIGHT)):
        leaf(b, Vector((0, 0, z)), az, size, size * 0.65, deg(25), -0.5, cell, under=OLIVE_DARK, thick=0.003)
    box(b, Vector((0.04, 0.0, 0.05)), (0.008, 0.008, 0.14), STEM_DARK, rot=(0, deg(10), 0))
    return b


# ---- assemble ---------------------------------------------------------------------------------------------------------

remove_default_cube()
plants = {}


def add_plant(name, stages, fruits=(), wild=None):
    plants[name] = dict(stages=stages, fruits=list(fruits), wild=wild)


def fruit_object(draw, pos, index, name, budget):
    b = Builder()
    draw(b, pos, index)
    return b.finish(name, budget)


add_plant("Rye", [rye(s).finish(f"Rye_Stage{s}", (150, 320, 560)[s]) for s in range(3)])
add_plant("Potato", [potato(s).finish(f"Potato_Stage{s}", (120, 380, 700)[s]) for s in range(3)])
add_plant("Cabbage", [cabbage(s).finish(f"Cabbage_Stage{s}", (120, 330, 520)[s]) for s in range(3)])

lingon_pos = lingon_fruit_positions()
add_plant("Lingonberry",
          [lingonberry(s)[0].finish(f"Lingonberry_Stage{s}", (160, 420)[s]) for s in range(2)],
          [fruit_object(berry_cluster, p, i, f"Lingonberry_Fruit{i}", 90) for i, p in enumerate(lingon_pos)],
          lingonberry(1, fruits=lingon_pos)[0].finish("Lingonberry_Wild", 760))

rasp_pos = raspberry_fruit_positions()
add_plant("Raspberry",
          [raspberry(s)[0].finish(f"Raspberry_Stage{s}", (160, 520)[s]) for s in range(2)],
          [fruit_object(raspberry_berry, p, i, f"Raspberry_Fruit{i}", 120) for i, p in enumerate(rasp_pos)],
          raspberry(1, fruits=rasp_pos)[0].finish("Raspberry_Wild", 900))

protos = collect_fronds()
pear_builder, pear_k = build_tree(protos, PEAR, PEAR_LEAVES)
pear_obj = pear_builder.finish("Pear_Tree", 2600)
pear_fruits = []
for i, pos in enumerate(pear_fruit_positions(pear_k)):
    pear_fruits.append(pear_fruit(pos, i).finish(f"Pear_Fruit{i}", 120))
add_plant("Pear", [pear_obj], pear_fruits)

add_plant("OysterMushroom", [oyster(s).finish(f"OysterMushroom_Stage{s}", (120, 140)[s]) for s in range(2)],
          [oyster_fruit(i).finish(f"OysterMushroom_Fruit{i}", 40) for i in range(3)],
          oyster(1, wild=True).finish("OysterMushroom_Wild", 220))
add_plant("Peas", [peas(s).finish(f"Peas_Stage{s}", (160, 420, 720)[s]) for s in range(3)], [],
          peas(2, wild=True).finish("Peas_Wild", 900))
add_plant("Sunflower", [sunflower(s).finish(f"Sunflower_Stage{s}", (80, 300, 640)[s]) for s in range(3)])
add_plant("Corn", [corn(s).finish(f"Corn_Stage{s}", (100, 280, 760)[s]) for s in range(3)])

icons = {
    "Rye": (icon_rye().finish("Icon_Rye", 200), ICON_USABLE),
    "Potato": (icon_potato().finish("Icon_Potato", 320), ICON_USABLE),
    "Cabbage": (icon_cabbage().finish("Icon_Cabbage", 520), ICON_USABLE),
    "Lingonberry": (icon_lingonberry().finish("Icon_Lingonberry", 600), ICON_USABLE),
    "Raspberry": (icon_raspberry().finish("Icon_Raspberry", 600), ICON_USABLE),
    "Pear": (icon_pear().finish("Icon_Pear", 200), ICON_USABLE),
    "OysterMushroom": (icon_oyster().finish("Icon_OysterMushroom", 200), ICON_USABLE),
    "Peas": (icon_peas().finish("Icon_Peas", 400), ICON_USABLE),
    "Sunflower": (icon_sunflower().finish("Icon_Sunflower", 400), ICON_USABLE),
    "Corn": (icon_corn().finish("Icon_Corn", 500), ICON_USABLE),
    "PearSapling": (icon_pear_sapling().finish("Icon_PearSapling", 300), ICON_RESOURCES),
}
for obj, _ in icons.values():
    obj.hide_render = True

if bpy.app.background:
    os.makedirs(MODEL_DIR, exist_ok=True)
    for name, data in plants.items():
        objs = list(data["stages"]) + list(data["fruits"]) + ([data["wild"]] if data["wild"] else [])
        export_fbx(objs, f"{MODEL_DIR}/{name}.fbx")

    setup_render()
    for name, (obj, folder) in icons.items():
        hide_all_but([obj])
        render([obj], f"{folder}/{name}.png", fill=0.62)

    # Previews: one image per plant, stages left to right, the wild one next, fruits at the end (scratchpad only).
    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.72, 0.82, 0.86)
    bpy.context.scene.world = world
    bpy.context.scene.render.film_transparent = False
    for name, data in plants.items():
        shown = list(data["stages"])
        if data["wild"]:
            shown.append(data["wild"])
        placed = shown + list(data["fruits"])
        for col, obj in enumerate(placed):
            obj.location = (col * 0.4, 0.0, 0.0)
        hide_all_but(placed)
        render(placed, PREVIEW_DIR + f"/prev_{name}.png", 1400, 520, fill=0.62, direction=(0.2, -0.75, 0.55))
