"""Crop batch 2: Tomato, Rice, Cranberry, WatermelonPlant, DatePalm, Oats, Turnip, Blueberry, Cherry, Beans, Flax, Cotton,
Chili, Cattail, Citrus (2026-10-04). Same layout and conventions as crops_batch1.py (read its docstring).

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python "Tools/Agent Tools/Blender/crops_batch2.py"

The three trees (Cherry, Citrus via crop_trees.py, DatePalm hand-built) are 0.70 m tall like the apple and fit the 2x2 orchard
plot. The watermelon crop has no wild mesh: the wild watermelon of the Meadow already exists.
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

BLUE = (1, 5)    # 60A3B6
NAVY = (2, 4)    # 30495D
LEAVES_A = (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)


# ---- Tomato (trellis) -----------------------------------------------------------------------------------------------

def tomato_vine(b, base, top, plane_az, rng, stage, tomato_seed=0):
    base, top = Vector(base), Vector(top)
    wig = polar(0.01, plane_az)
    pts = [base, base + (top - base) * 0.33 + wig, base + (top - base) * 0.66 - wig, top]
    prism_path(b, pts, [0.0048, 0.0042, 0.0036, 0.0028], 3, OLIVE_DARK, cell_end=OLIVE)
    n = 2 if stage == 0 else 4
    size = 0.042 if stage == 0 else 0.062
    for k in range(n):
        t = (0.3 + 0.7 * k / max(1, n - 1)) if stage else 0.7
        point = base + (top - base) * t
        side = 1.0 if k % 2 == 0 else -1.0
        az = plane_az + (0.0 if side > 0 else math.pi) + rng.uniform(-0.3, 0.3)
        tip = point + polar(size * 0.7, az, size * 0.25)
        prism_path(b, [point, tip], [0.0028, 0.002], 3, OLIVE, cap_end=False)
        leaf(b, tip, az, size * 0.62, size * 0.42, deg(15), -0.4, LEAVES_A[k % 3], under=OLIVE_DARK)
        for sg in (-1, 1):
            leaf(b, point + (tip - point) * 0.55, az + sg * 1.2, size * 0.5, size * 0.32, deg(18), -0.4,
                 LEAVES_A[(k + 1) % 3], under=OLIVE_DARK)
    if stage == 1:
        for k in range(3):
            point = base + (top - base) * (0.4 + 0.18 * k) + wig * (0.5 if k % 2 else -0.5)
            box(b, point + Vector((0, 0, 0.008)), (0.014, 0.014, 0.005), GOLD_LIGHT, rot=(0, 0, k))
    if stage == 2:
        for k in range(2):
            t = 0.34 + 0.24 * k
            side = 1.0 if (k + tomato_seed) % 2 == 0 else -1.0
            fruit = base + (top - base) * t + polar(0.028 * side, plane_az, -0.016)
            prism_path(b, [base + (top - base) * t, fruit + Vector((0, 0, 0.018))], [0.0025, 0.002], 3, OLIVE, cap_end=False)
            blob(b, fruit, 0.021, (RED, RED_DARK), scale=(1.0, 1.0, 0.88), subdiv=1, seed=k + tomato_seed)
            box(b, fruit + Vector((0, 0, 0.019)), (0.022, 0.022, 0.004), GREEN_MID, rot=(0, 0, 0.5 + k))


def tomato(stage, wild=False):
    b = Builder()
    rng = random.Random(71)
    if not wild:
        heights = (0.075, 0.3, 0.44)
        for i, x in enumerate((-0.06, 0.06)):
            h = heights[stage] * rng.uniform(0.92, 1.04)
            tomato_vine(b, Vector((x, 0.0, 0.0)), Vector((x + rng.uniform(-0.01, 0.01), 0.0, h)), math.pi / 2, rng, stage, i)
        return b
    for i in range(4):
        az = i * (math.pi / 2) + 0.4
        tomato_vine(b, polar(0.02, az), polar(0.1, az, 0.2), az + math.pi / 2, rng, 2, i)
    return b


# ---- Rice (a paddy bed: the crop meshes carry a thin sheet of water) --------------------------------------------------

def paddy_water(b):
    cylinder(b, Vector((0, 0, 0.006)), 0.118, 0.01, 10, BLUE, cell_top=BLUE)


def rice(stage, paddy=True):
    b = Builder()
    r = random.Random(75)
    if paddy:
        paddy_water(b)
    count = (7, 11, 10)[stage]
    for i in range(count):
        az = i * GOLDEN + r.uniform(-0.2, 0.2)
        radius = 0.012 + 0.05 * math.sqrt((i + 0.5) / count)
        base = polar(radius, az, 0.004)
        if stage == 0:
            blade(b, base, az, r.uniform(0.05, 0.08), 0.012, 0.002, deg(76), -deg(40), LIME if i % 2 else GREEN_LIGHT, prof=(0.9, 1.0, 0.0))
            continue
        height = r.uniform(0.12, 0.17) if stage == 1 else r.uniform(0.17, 0.23)
        lean = r.uniform(0.01, 0.03)
        top = base + polar(lean, az, height)
        mid = base + polar(lean * 0.3, az, height * 0.55)
        if stage == 1:
            blade(b, base, az, height * 0.95, 0.014, 0.002, deg(74), -deg(60), (LIME, GREEN_LIGHT, LEAF_YG)[i % 3], prof=(0.8, 1.0, 0.8, 0.0))
            continue
        prism_path(b, [base, mid, top], [0.005, 0.004, 0.003], 3, LEAF_YG)
        d = polar(1.0, az)
        pan = [top, top + d * 0.03 + Vector((0, 0, 0.022)), top + d * 0.058 + Vector((0, 0, 0.012)), top + d * 0.075 + Vector((0, 0, -0.025))]
        prism_path(b, pan, [0.0045, 0.0055, 0.0048, 0.0], 4, GOLD_LIGHT if i % 2 else STRAW, cell_end=GOLD)
        if i % 2 == 0:
            blade(b, base + Vector((0, 0, height * 0.2)), az, height * 0.8, 0.014, 0.002, deg(66), -deg(75), LIME, prof=(0.8, 1.0, 0.7, 0.0))
    return b


# ---- Cranberry (a creeping evergreen vine) ----------------------------------------------------------------------------

CRAN_RUNNERS = [(0.3, 0.12), (1.5, 0.13), (2.6, 0.115), (3.7, 0.125), (4.9, 0.11)]


def cranberry_runner(b, az, length, leaves, rng):
    base = polar(0.01, az, 0.006)
    mid = polar(length * 0.5, az, 0.022)
    tip = polar(length, az, 0.014)
    prism_path(b, [base, mid, tip], [0.0035, 0.003, 0.002], 3, STEM_DARK, cell_end=OLIVE_DARK)
    for k in range(leaves):
        t = 0.25 + 0.75 * k / max(1, leaves - 1)
        p = _bz(base, mid, tip, t)
        la = az + (1.0 if k % 2 else -1.0) * (1.0 + rng.uniform(-0.2, 0.2))
        leaf(b, p, la, 0.03, 0.017, deg(25), -0.4, (GREEN_DARK, GREEN_MID, OLIVE_MID)[k % 3], under=OLIVE_DARK)
    return tip


def _bz(p0, p1, p2, t):
    return bezier(p0, p1, p2, t)


def cranberry(stage, fruits=None):
    b = Builder()
    r = random.Random(81)
    tips = []
    if stage == 0:
        for az, length in CRAN_RUNNERS[:2]:
            cranberry_runner(b, az, length * 0.45, 2, r)
        return b, tips
    for az, length in CRAN_RUNNERS:
        tips.append(cranberry_runner(b, az, length, 3, r))
    if fruits:
        for k, pos in enumerate(fruits):
            cran_cluster(b, pos, k)
    return b, tips


def cran_cluster(b, pos, seed):
    for i, o in enumerate(((0, 0, 0), (0.016, 0.004, 0.003), (-0.006, 0.015, 0.002))):
        blob(b, Vector(pos) + Vector(o), 0.0135, (RED_DARK, RED), subdiv=0, seed=seed + i)


def cran_fruit_positions():
    _, tips = cranberry(1)
    return [tips[i] + Vector((0, 0, 0.012)) for i in (0, 1, 2, 3)]


# ---- Watermelon (a creeping vine with striped melons) ---------------------------------------------------------------

def melon_leaf(b, base, az, size, cell):
    blade(b, base, az, size, size * 0.85, 0.002, deg(18), -0.35, cell, prof=(0.3, 0.9, 0.55, 1.0, 0.45, 0.0), cell_under=OLIVE_DARK)


def watermelon(stage):
    b = Builder()
    r = random.Random(85)
    if stage == 0:
        for i in range(2):
            az = i * math.pi + 0.4
            blade(b, polar(0.006, az, 0.01), az, 0.045, 0.03, 0.002, deg(30), -deg(40), GREEN_LIGHT, prof=(0.3, 1.0, 0.7, 0.0), cell_under=GREEN_MID)
        prism_path(b, [Vector((0, 0, 0)), Vector((0, 0, 0.02))], [0.005, 0.004], 4, GREEN_MID)
        return b
    runners = 3 if stage == 1 else 3
    for i in range(runners):
        az = i * (2 * math.pi / 3) + 0.5
        length = 0.12 if stage == 1 else 0.125
        pts = [polar(0.01, az, 0.008), polar(length * 0.5, az, 0.02), polar(length, az, 0.012)]
        prism_path(b, pts, [0.0045, 0.0038, 0.0028], 3, OLIVE_DARK, cell_end=OLIVE)
        for k in range(2):
            t = 0.35 + 0.5 * k
            p = _bz(pts[0], pts[1], pts[2], t)
            melon_leaf(b, p + Vector((0, 0, 0.004)), az + (0.9 if k else -0.9), 0.07 if stage == 2 else 0.062, (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[(i + k) % 3])
        melon_leaf(b, pts[2], az, 0.06, LEAVES_A[i % 3])
    if stage == 2:
        for i, (x, y, rot) in enumerate(((0.055, 0.045, 0.5), (-0.06, -0.055, 2.3))):
            blob(b, Vector((x, y, 0.046)), 0.05, (GREEN_DARK, GREEN_LIGHT, GREEN_DARK, OLIVE_MID), scale=(1.25, 1.0, 0.95), subdiv=1, seed=i, rot=(0, 0, rot))
            box(b, Vector((x, y, 0.094)), (0.012, 0.012, 0.012), OLIVE_DARK)
    return b


# ---- Date palm ---------------------------------------------------------------------------------------------------------

PALM_TOP = 0.46


def palm_trunk(b):
    pts = [Vector((0, 0, 0)), Vector((0.004, 0, 0.12)), Vector((0.012, 0.004, 0.26)), Vector((0.012, 0.01, 0.38)), Vector((0.008, 0.012, PALM_TOP))]
    prism_path(b, pts, [0.034, 0.03, 0.027, 0.0255, 0.024], 8, BARK_A, cell_end=BARK_B)
    for z in (0.07, 0.14, 0.21, 0.28, 0.35, 0.42):
        t = z / PALM_TOP
        c = Vector((0.012 * t, 0.012 * t * t, z))
        cylinder(b, c, 0.0285 - 0.007 * t, 0.014, 8, BARK_B, cell_top=BARK_B)


def palm_crown(b):
    top = Vector((0.008, 0.012, PALM_TOP))
    for i in range(10):
        az = i * (2 * math.pi / 10) + 0.2
        upright = i % 3 == 2
        blade(b, top + polar(0.01, az, 0.0), az, 0.27 if not upright else 0.22, 0.075, 0.0025, deg(75) if upright else deg(48),
              -deg(80) if upright else -deg(115), (GREEN_MID, OLIVE_MID, GREEN_DARK)[i % 3],
              prof=(0.12, 0.5, 0.75, 0.65, 0.5, 0.25, 0.0), cell_under=OLIVE_DARK)
    blade(b, top, 0.0, 0.2, 0.04, 0.0025, deg(88), -deg(5), OLIVE_LIGHT, prof=(0.2, 0.7, 0.6, 0.3, 0.0))
    return top


def date_cluster(b, pos, seed):
    p = Vector(pos)
    prism_path(b, [p + Vector((0, 0, 0.02)), p], [0.003, 0.0022], 3, STRAW, cap_end=False)
    for i, o in enumerate(((0.0, 0.0, 0.0), (0.012, 0.004, -0.004), (-0.011, 0.006, -0.002), (0.004, -0.012, -0.006), (-0.004, 0.012, -0.012))):
        blob(b, p + Vector(o) + Vector((0, 0, -0.012)), 0.0105, (ORANGE, TAN), scale=(0.7, 0.7, 1.35), subdiv=0, seed=seed + i)


def date_positions():
    top = Vector((0.008, 0.012, PALM_TOP))
    out = []
    for i in range(4):
        az = i * (math.pi / 2) + 0.7
        out.append(top + polar(0.05, az, -0.03))
    return out


def datepalm_tree(fruits=None):
    b = Builder()
    palm_trunk(b)
    palm_crown(b)
    if fruits:
        for k, pos in enumerate(fruits):
            date_cluster(b, pos, k)
    return b


# ---- Oats -------------------------------------------------------------------------------------------------------------

def oats(stage):
    b = Builder()
    r = random.Random(91)
    count = (7, 10, 9)[stage]
    for i in range(count):
        az = i * GOLDEN + r.uniform(-0.2, 0.2)
        radius = 0.012 + 0.055 * math.sqrt((i + 0.5) / count)
        base = polar(radius, az)
        if stage == 0:
            blade(b, base, az, r.uniform(0.05, 0.08), 0.014, 0.002, deg(76), -deg(42), LEAF_YG if i % 2 else LIME_LIGHT, prof=(0.9, 1.0, 0.0))
            continue
        height = r.uniform(0.12, 0.19) if stage == 1 else r.uniform(0.2, 0.26)
        lean = r.uniform(0.008, 0.02)
        mid = base + polar(lean * 0.3, az, height * 0.55)
        top = base + polar(lean, az, height)
        prism_path(b, [base, mid, top], [0.0055, 0.004, 0.003], 3, LEAF_YG if stage == 1 else STRAW)
        if stage == 2:
            for j in range(4):
                ba = az + j * (math.pi / 2) + 0.3
                d = polar(1.0, ba)
                pts = [top, top + d * 0.022 + Vector((0, 0, 0.01)), top + d * 0.036 + Vector((0, 0, -0.012))]
                prism_path(b, pts, [0.0026, 0.0024, 0.0016], 3, PALE_YELLOW if j % 2 else STRAW, cell_end=GOLD_LIGHT)
        if i % 2 == 0:
            blade(b, base + polar(0.003, az, height * 0.25), az, height * r.uniform(0.45, 0.6), 0.018, 0.002, deg(68), -deg(62),
                  LEAF_YG if stage == 1 else STRAW, prof=(0.8, 1.0, 0.0))
    return b


# ---- Turnip -----------------------------------------------------------------------------------------------------------

def turnip(stage):
    b = Builder()
    if stage == 0:
        for i in range(3):
            az = i * 2.1 + 0.3
            blade(b, polar(0.008, az, 0.005), az, 0.05, 0.022, 0.002, deg(45), -deg(40), GREEN_LIGHT, prof=(0.4, 1.0, 0.5, 0.0), cell_under=GREEN_MID)
        return b
    leaves = 6 if stage == 1 else 8
    for i in range(leaves):
        az = i * (2 * math.pi / leaves) + 0.2
        blade(b, polar(0.012, az, 0.03 if stage == 2 else 0.018), az, 0.1 if stage == 1 else 0.115, 0.04, 0.002, deg(58), -deg(40),
              (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[i % 3], prof=(0.25, 0.8, 1.0, 0.5, 0.0), cell_under=OLIVE_DARK)
    radius = 0.028 if stage == 1 else 0.052
    blob(b, Vector((0, 0, radius * 0.55)), radius, (LILAC, CREAM), scale=(1, 1, 0.92), subdiv=1, jitter=0.002, seed=3, by_normal=True)
    return b


# ---- shrubs with berries: Blueberry ----------------------------------------------------------------------------------

BLUE_TWIGS = [(0.3, 0.2, 0.06), (1.4, 0.22, 0.07), (2.6, 0.19, 0.065), (3.7, 0.21, 0.06), (4.9, 0.17, 0.055), (0.9, 0.13, 0.04)]


def shrub_twig(b, az, height, out, leaves, rng, leaf_size, cells):
    base = polar(0.012, az)
    top = polar(out, az, height)
    mid = polar(out * 0.3, az, height * 0.58)
    prism_path(b, [base, mid, top], [0.0052, 0.0038, 0.0024], 3, STEM_DARK, cell_end=OLIVE_DARK)
    for k in range(leaves):
        t = 0.3 + 0.7 * k / max(1, leaves - 1)
        point = _bz(base, mid, top, t)
        la = az + (1.0 if k % 2 else -1.0) * (1.1 + rng.uniform(-0.25, 0.25))
        leaf(b, point, la, leaf_size, leaf_size * 0.55, deg(35), -0.5, cells[k % 3], under=OLIVE_DARK)
    return top


def blueberry(stage, fruits=None):
    b = Builder()
    r = random.Random(95)
    tips = []
    cells = (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)
    if stage == 0:
        for az, height, out in BLUE_TWIGS[:3]:
            shrub_twig(b, az, height * 0.5, out * 0.5, 3, r, 0.04, cells)
        return b, tips
    for az, height, out in BLUE_TWIGS:
        tips.append(shrub_twig(b, az, height, out, 4, r, 0.046, cells))
    if fruits:
        for k, pos in enumerate(fruits):
            blue_cluster(b, pos, k)
    return b, tips


def blue_cluster(b, pos, seed):
    for i, o in enumerate(((0, 0, 0), (0.019, 0.004, 0.005), (-0.009, 0.018, 0.003), (0.008, -0.014, 0.008))):
        blob(b, Vector(pos) + Vector(o), 0.0125, (NAVY, BLUE), subdiv=0, seed=seed + i)


def blue_fruit_positions():
    _, tips = blueberry(1)
    return [tips[i] + Vector((0, 0, -0.016)) for i in (0, 1, 2, 3)]


# ---- trees from the Pine sprays: Cherry, Citrus ---------------------------------------------------------------------

CHERRY = dict(seed=21, trunk_top=0.235, limb_angles=[18.0, 104.0, 192.0, 283.0], cz=0.60, rx=0.35, rz=0.275,
              layers=[-0.225, -0.125, -0.02, 0.08, 0.172], target_height=0.70, limb_reach=(0.12, 0.16), limb_top=(0.44, 0.53),
              bias=0.0, secondary_r=(0.15, 0.22))
CHERRY_LEAVES = (GREEN_MID, OLIVE_MID, LEAF_YG)
CITRUS = dict(seed=33, trunk_top=0.2, limb_angles=[40.0, 130.0, 220.0, 310.0], cz=0.52, rx=0.30, rz=0.27,
              layers=[-0.2, -0.1, 0.0, 0.1, 0.18], target_height=0.62, limb_reach=(0.1, 0.14), limb_top=(0.38, 0.46),
              bias=0.05, secondary_r=(0.12, 0.19))
CITRUS_LEAVES = (GREEN_DARK, GREEN_MID, OLIVE_MID)


def cherry_fruit(center, seed):
    b = Builder()
    c = Vector(center)
    for sign in (-1, 1):
        stalk_end = c + Vector((sign * 0.011, 0, -0.022))
        prism_path(b, [c + Vector((0, 0, 0.008)), stalk_end + Vector((0, 0, 0.012))], [0.0022, 0.0018], 3, OLIVE_DARK, cap_end=False)
        blob(b, stalk_end, 0.0155, (RED, RED_DARK), subdiv=0, seed=seed + sign)
    return b


def lemon_fruit(center, seed):
    b = Builder()
    c = Vector(center)
    blob(b, c, 0.022, (GOLD_LIGHT, PALE_YELLOW, GOLD_LIGHT), scale=(1.15, 1.0, 1.0), subdiv=1, seed=seed, rot=(0, 0, seed))
    prism_path(b, [c + Vector((0.025, 0, 0)), c + Vector((0.034, 0, 0))], [0.006, 0.0], 4, GOLD_LIGHT)
    return b


# ---- Beans (trellis) --------------------------------------------------------------------------------------------------

def bean_vine(b, base, top, plane_az, rng, stage):
    base, top = Vector(base), Vector(top)
    wig = polar(0.014, plane_az)
    pts = [base, base + (top - base) * 0.33 + wig, base + (top - base) * 0.66 - wig, top]
    prism_path(b, pts, [0.0045, 0.004, 0.0034, 0.0026], 3, OLIVE_MID, cell_end=OLIVE)
    n = 2 if stage == 0 else 4
    size = 0.045 if stage == 0 else 0.068
    for k in range(n):
        t = (0.28 + 0.72 * k / max(1, n - 1)) if stage else 0.7
        point = base + (top - base) * t
        side = 1.0 if k % 2 == 0 else -1.0
        az = plane_az + (0.0 if side > 0 else math.pi) + rng.uniform(-0.3, 0.3)
        tip = point + polar(size * 0.55, az, size * 0.2)
        prism_path(b, [point, tip], [0.0025, 0.002], 3, OLIVE, cap_end=False)
        blade(b, tip, az, size * 0.85, size * 0.8, 0.002, deg(14), -0.45, GREEN_LIGHT if k % 2 else GREEN_MID,
              prof=(0.3, 1.0, 0.5, 0.0), cell_under=GREEN_MID)
        for sg in (-1, 1):
            leaf(b, point + (tip - point) * 0.5, az + sg * 1.25, size * 0.7, size * 0.6, deg(18), -0.4, GREEN_MID, under=OLIVE_DARK)
    if stage == 1:
        for k in range(3):
            point = base + (top - base) * (0.4 + 0.18 * k)
            box(b, point + Vector((0, 0, 0.008)), (0.012, 0.012, 0.01), RED, rot=(0, 0, k))
    if stage == 2:
        for k in range(3):
            t = 0.3 + 0.22 * k
            point = base + (top - base) * t
            side = 1.0 if (k + int(base.x * 100)) % 2 == 0 else -1.0
            az = plane_az + (0.0 if side > 0 else math.pi)
            blade(b, point + polar(0.012, az), az + rng.uniform(-0.3, 0.3), 0.13, 0.017, 0.006, -deg(70), deg(30), GREEN_LIGHT,
                  prof=(0.5, 1.0, 0.8, 0.0), cell_under=GREEN_MID)


def beans(stage, wild=False):
    b = Builder()
    rng = random.Random(99)
    if not wild:
        heights = (0.075, 0.32, 0.45)
        for x in (-0.1, 0.0, 0.1):
            h = heights[stage] * rng.uniform(0.9, 1.05)
            bean_vine(b, Vector((x, 0.0, 0.0)), Vector((x + rng.uniform(-0.01, 0.01), 0.0, h)), math.pi / 2, rng, stage)
        return b
    apex = Vector((0.0, 0.0, 0.36))
    for i in range(3):
        az = i * (2 * math.pi / 3) + 0.4
        foot = polar(0.1, az)
        prism_path(b, [foot, foot + (apex - foot) * 0.5, apex], [0.007, 0.006, 0.004], 4, BARK_A, cell_end=BARK_B)
        bean_vine(b, foot + polar(0.012, az + 1.4), apex * 0.92 + polar(0.014, az + 1.4), az + math.pi / 2, rng, 2)
    return b


# ---- Flax -------------------------------------------------------------------------------------------------------------

def flax(stage):
    b = Builder()
    r = random.Random(103)
    count = (8, 12, 14)[stage]
    for i in range(count):
        az = i * GOLDEN + r.uniform(-0.2, 0.2)
        radius = 0.01 + 0.07 * math.sqrt((i + 0.5) / count)
        base = polar(radius, az)
        if stage == 0:
            blade(b, base, az, r.uniform(0.04, 0.065), 0.008, 0.002, deg(78), -deg(30), LIME if i % 2 else GREEN_LIGHT, prof=(0.9, 1.0, 0.0))
            continue
        height = r.uniform(0.12, 0.19) if stage == 1 else r.uniform(0.22, 0.3)
        lean = r.uniform(0.005, 0.025)
        top = base + polar(lean, az, height)
        mid = base + polar(lean * 0.3, az, height * 0.55)
        prism_path(b, [base, mid, top], [0.0034, 0.0028, 0.002], 3, OLIVE_LIGHT)
        for k in range(2):
            p = base + (top - base) * (0.35 + 0.3 * k)
            blade(b, p, az + k * 2.5, 0.032, 0.006, 0.0015, deg(55), -deg(25), LEAF_YG, prof=(0.9, 1.0, 0.0))
        if stage == 2:
            f = top + Vector((0, 0, 0.004))
            box(b, f, (0.017, 0.017, 0.004), BLUE if i % 3 else NAVY, rot=(0.2, 0.0, az))
            box(b, f + Vector((0, 0, 0.003)), (0.006, 0.006, 0.004), GOLD_LIGHT)
    return b


# ---- Cotton -----------------------------------------------------------------------------------------------------------

def cotton_boll(b, pos, seed):
    p = Vector(pos)
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        blade(b, p + polar(0.006, a, -0.004), a, 0.026, 0.014, 0.002, deg(60), -deg(10), TAN, prof=(0.7, 1.0, 0.0), cell_under=BROWN_DARK)
    for i, o in enumerate(((0, 0, 0.012), (0.011, 0.003, 0.013), (-0.006, 0.01, 0.014))):
        blob(b, p + Vector(o), 0.0155, (CREAM, PALE_YELLOW), subdiv=0, seed=seed + i)


COTTON_STEMS = [(0.2, 0.2, 0.05), (1.4, 0.17, 0.06), (2.5, 0.21, 0.05), (3.6, 0.16, 0.065), (4.8, 0.19, 0.055)]


def cotton(stage):
    b = Builder()
    r = random.Random(107)
    stems = COTTON_STEMS[:2] if stage == 0 else COTTON_STEMS
    boll_spots = []
    for si, (az, height, out) in enumerate(stems):
        h = height * (0.4 if stage == 0 else 1.0)
        base, top = polar(0.012, az), polar(out * (0.5 if stage == 0 else 1), az, h)
        mid = polar(out * 0.3, az, h * 0.58)
        prism_path(b, [base, mid, top], [0.0052, 0.0042, 0.003], 3, OLIVE_DARK, cell_end=OLIVE)
        for k in range(3 if stage else 2):
            p = _bz(base, mid, top, 0.4 + 0.3 * k)
            la = az + (1.0 if k % 2 else -1.0) * 1.0
            blade(b, p, la, 0.058 if stage else 0.04, 0.05 if stage else 0.035, 0.002, deg(28), -0.5, (OLIVE_MID, GREEN_MID, OLIVE_LIGHT)[k % 3],
                  prof=(0.3, 0.9, 0.5, 1.0, 0.0), cell_under=OLIVE_DARK)
        boll_spots.append(top)
    if stage == 1:
        for k, t in enumerate(boll_spots[:3]):
            box(b, t + Vector((0, 0, 0.006)), (0.015, 0.015, 0.006), PALE_YELLOW, rot=(0, 0, k))
    if stage == 2:
        for k, t in enumerate(boll_spots[:4]):
            cotton_boll(b, t + Vector((0, 0, 0.002)), k)
    return b


# ---- Chili ------------------------------------------------------------------------------------------------------------

CHILI_STEMS = [(0.3, 0.16, 0.05), (1.5, 0.18, 0.055), (2.6, 0.15, 0.05), (3.8, 0.17, 0.06), (5.0, 0.14, 0.045)]


def chili_pepper(b, pos, az, green):
    p = Vector(pos)
    out = polar(0.016, az)
    pts = [p, p + out * 0.4 + Vector((0, 0, -0.024)), p + out + Vector((0, 0, -0.06))]
    prism_path(b, pts, [0.0105, 0.0095, 0.0], 5, GREEN_MID if green else RED, cell_end=GREEN_MID if green else RED_DARK)
    box(b, p + Vector((0, 0, 0.002)), (0.014, 0.014, 0.005), GREEN_DEEP)


def chili(stage):
    b = Builder()
    r = random.Random(111)
    stems = CHILI_STEMS[:2] if stage == 0 else CHILI_STEMS
    for si, (az, height, out) in enumerate(stems):
        h = height * (0.4 if stage == 0 else 1.0)
        base, top = polar(0.012, az), polar(out * (0.5 if stage == 0 else 1), az, h)
        mid = polar(out * 0.3, az, h * 0.58)
        prism_path(b, [base, mid, top], [0.0052, 0.004, 0.0028], 3, OLIVE_DARK, cell_end=OLIVE)
        for k in range(3 if stage else 2):
            p = _bz(base, mid, top, 0.35 + 0.3 * k)
            la = az + (1.0 if k % 2 else -1.0) * 1.1
            leaf(b, p, la, 0.05 if stage else 0.036, 0.024, deg(32), -0.45, (GREEN_DARK, OLIVE_MID, GREEN_MID)[k % 3], under=OLIVE_DARK)
        if stage == 2:
            chili_pepper(b, top + polar(0.012, az, -0.012), az + 0.3, si % 3 == 0)
    if stage == 1:
        for az, height, out in CHILI_STEMS[:3]:
            p = polar(out * 0.5, az, height * 0.7)
            box(b, p, (0.012, 0.012, 0.004), CREAM, rot=(0, 0, az))
    return b


# ---- Cattail ----------------------------------------------------------------------------------------------------------

def cattail(stage):
    b = Builder()
    r = random.Random(115)
    count = (4, 8, 8)[stage]
    for i in range(count):
        az = i * GOLDEN + r.uniform(-0.2, 0.2)
        radius = 0.012 + 0.05 * math.sqrt((i + 0.5) / count)
        base = polar(radius, az)
        length = (0.07, 0.22, 0.27)[stage] * r.uniform(0.85, 1.1)
        blade(b, base, az, length, 0.017 if stage else 0.012, 0.002, deg(80), -deg(30 if stage else 20), (OLIVE_LIGHT, LEAF_YG, OLIVE_MID)[i % 3],
              prof=(0.8, 1.0, 1.0, 0.7, 0.0) if stage else (0.9, 1.0, 0.0), cell_under=OLIVE)
    if stage == 2:
        for i in range(4):
            az = i * (2 * math.pi / 4) + 0.6
            height = r.uniform(0.27, 0.33)
            base = polar(0.02 + 0.015 * (i % 2), az)
            top = base + polar(0.01, az, height)
            prism_path(b, [base, base + (top - base) * 0.5, top], [0.0042, 0.0036, 0.003], 4, OLIVE)
            cylinder(b, top + Vector((0, 0, -0.045)), 0.0118, 0.056, 6, [BROWN_DARK, STEM_DARK], cell_top=BROWN_DARK)
            prism_path(b, [top + Vector((0, 0, 0.0)), top + Vector((0, 0, 0.03))], [0.0034, 0.0], 3, STRAW)
    return b


# ---- item icons -------------------------------------------------------------------------------------------------------

def stalk_bundle(panicle):
    b = Builder()
    for i, az in enumerate((-0.3, 0.0, 0.3)):
        s = math.sin(az)
        top = Vector((s * 0.22, 0.0, 0.2))
        prism_path(b, [Vector((0, 0, 0)), Vector((s * 0.11, 0, 0.1)), top], [0.0085, 0.0075, 0.006], 4, STRAW)
        panicle(b, top, i)
    box(b, Vector((0, 0, 0.07)), (0.06, 0.04, 0.016), TAN)
    return b


def rice_panicle(b, top, i):
    pts = [top, top + Vector((0.025, 0, 0.03)), top + Vector((0.06, 0, 0.02)), top + Vector((0.088, 0, -0.03))]
    prism_path(b, pts, [0.008, 0.0095, 0.008, 0.0], 4, GOLD_LIGHT if i % 2 else STRAW, cell_end=GOLD)


def oats_panicle(b, top, i):
    for j in range(4):
        d = polar(1.0, j * (math.pi / 2) + 0.3)
        prism_path(b, [top, top + d * 0.035 + Vector((0, 0, 0.02)), top + d * 0.06 + Vector((0, 0, -0.02))], [0.005, 0.0045, 0.003], 3,
                   PALE_YELLOW if j % 2 else STRAW, cell_end=GOLD_LIGHT)


def icon_rice():
    return stalk_bundle(rice_panicle)


def icon_oats():
    return stalk_bundle(oats_panicle)


def icon_tomato():
    b = Builder()
    blob(b, Vector((0, 0, 0.05)), 0.055, (RED, RED_DARK), scale=(1.0, 1.0, 0.85), subdiv=1, seed=2)
    for i in range(5):
        a = i * (2 * math.pi / 5)
        blade(b, polar(0.008, a, 0.093), a, 0.03, 0.016, 0.003, deg(8), -deg(15), GREEN_MID, prof=(0.7, 1.0, 0.0), cell_under=GREEN_DARK)
    box(b, Vector((0, 0, 0.098)), (0.008, 0.008, 0.016), OLIVE_DARK)
    return b


def icon_cranberry():
    b = Builder()
    for i, o in enumerate(((0, 0), (0.04, 0.01), (-0.04, 0.012), (0.02, -0.038), (-0.02, -0.034), (0.0, 0.042), (0.055, -0.03))):
        blob(b, Vector((o[0], o[1], 0.026)), 0.025, (RED_DARK, RED), subdiv=1, seed=i + 7)
    leaf(b, Vector((0.01, 0.0, 0.05)), 0.9, 0.08, 0.045, deg(22), -0.5, GREEN_DARK, under=OLIVE_DARK, thick=0.004)
    return b


def icon_date():
    b = Builder()
    for i, (x, y, rot) in enumerate(((-0.035, 0.0, 0.5), (0.03, -0.01, -0.4), (0.0, 0.04, 1.2))):
        blob(b, Vector((x, y, 0.04)), 0.035, (ORANGE, TAN), scale=(0.72, 0.72, 1.45), subdiv=1, jitter=0.002, seed=i, rot=(0.2, 0, rot))
        box(b, Vector((x, y, 0.094)), (0.008, 0.008, 0.012), STEM_DARK)
    blade(b, Vector((0.0, 0.05, 0.02)), 1.6, 0.17, 0.05, 0.003, deg(40), -deg(40), GREEN_MID, prof=(0.2, 0.7, 0.8, 0.5, 0.0), cell_under=OLIVE_DARK)
    return b


def icon_turnip():
    b = Builder()
    blob(b, Vector((0, 0, 0.055)), 0.062, (LILAC, CREAM), scale=(1, 1, 0.95), subdiv=1, jitter=0.003, seed=3, by_normal=True)
    prism_path(b, [Vector((0, 0, 0.0)), Vector((0, 0, -0.02))], [0.01, 0.0], 5, CREAM)
    for i in range(5):
        az = i * (2 * math.pi / 5) + 0.3
        blade(b, polar(0.01, az, 0.1), az, 0.1, 0.04, 0.003, deg(55), -deg(35), (GREEN_MID, OLIVE_MID)[i % 2], prof=(0.3, 0.9, 0.8, 0.0), cell_under=OLIVE_DARK)
    return b


def icon_blueberry():
    b = Builder()
    for i, o in enumerate(((0, 0), (0.045, 0.01), (-0.045, 0.012), (0.022, -0.04), (-0.022, -0.036), (0.0, 0.045))):
        blob(b, Vector((o[0], o[1], 0.028)), 0.028, (NAVY, BLUE), subdiv=1, seed=i + 3)
    leaf(b, Vector((0.01, 0.0, 0.055)), 0.8, 0.09, 0.05, deg(22), -0.5, GREEN_MID, under=OLIVE_DARK, thick=0.004)
    return b


def icon_cherry():
    b = Builder()
    for sign in (-1, 1):
        prism_path(b, [Vector((0, 0, 0.12)), Vector((sign * 0.03, 0, 0.1)), Vector((sign * 0.045, 0, 0.065))], [0.004, 0.0035, 0.003], 3, OLIVE_DARK)
        blob(b, Vector((sign * 0.05, 0, 0.04)), 0.04, (RED, RED_DARK), subdiv=1, seed=3 + sign)
    leaf(b, Vector((0.0, 0.0, 0.12)), 0.5, 0.09, 0.05, deg(18), -0.4, GREEN_MID, under=OLIVE_DARK, thick=0.004)
    return b


def icon_beans():
    b = Builder()
    for i, (az, off) in enumerate(((0.15, 0.0), (-0.1, 0.03), (-0.35, 0.065))):
        blade(b, Vector((-0.1, off, 0.02 + 0.006 * i)), az, 0.2, 0.034, 0.012, deg(10), -deg(25), (GREEN_LIGHT, GREEN_MID, GREEN_LIGHT)[i],
              prof=(0.5, 1.0, 1.0, 0.9, 0.5, 0.0), cell_under=GREEN_MID)
    return b


def icon_flax():
    b = Builder()
    for i in range(6):
        az = (i - 2.5) * 0.12
        s = math.sin(az)
        top = Vector((s * 0.2, (i % 2) * 0.01, 0.19 + 0.01 * (i % 3)))
        prism_path(b, [Vector((0, 0, 0)), Vector((s * 0.1, 0, 0.1)), top], [0.005, 0.0045, 0.0035], 3, OLIVE_LIGHT)
        box(b, top + Vector((0, 0, 0.004)), (0.036, 0.036, 0.008), BLUE if i % 2 else NAVY, rot=(0.2, 0.0, az))
        box(b, top + Vector((0, 0, 0.01)), (0.012, 0.012, 0.008), GOLD_LIGHT)
    box(b, Vector((0, 0, 0.065)), (0.08, 0.05, 0.016), TAN)
    return b


def icon_cotton():
    b = Builder()
    for i, (x, y) in enumerate(((0, 0), (0.07, 0.01), (0.035, -0.06))):
        cotton_boll_big(b, Vector((x, y, 0.0)), i)
    return b


def cotton_boll_big(b, p, seed):
    for i in range(4):
        a = i * math.pi / 2 + 0.4 + seed
        blade(b, p + polar(0.012, a, 0.0), a, 0.05, 0.026, 0.004, deg(58), -deg(8), TAN, prof=(0.7, 1.0, 0.0), cell_under=BROWN_DARK)
    for i, o in enumerate(((0, 0, 0.032), (0.026, 0.006, 0.034), (-0.015, 0.024, 0.036))):
        blob(b, p + Vector(o), 0.034, (CREAM, PALE_YELLOW), subdiv=1, seed=seed + i)


def icon_chili():
    b = Builder()
    for i, (x, y, az) in enumerate(((0.0, 0.0, 0.3), (0.05, -0.02, 0.9))):
        p = Vector((x, y, 0.1))
        out = polar(0.05, az)
        prism_path(b, [p, p + out * 0.4 + Vector((0, 0, -0.04)), p + out + Vector((0, 0, -0.1))], [0.02, 0.019, 0.0], 5, RED if i == 0 else RED_DARK, cell_end=RED_DARK)
        box(b, p + Vector((0, 0, 0.004)), (0.034, 0.034, 0.012), GREEN_DEEP)
        prism_path(b, [p, p + Vector((0, 0, 0.03))], [0.004, 0.003], 3, OLIVE_DARK)
    return b


def icon_cattail_root():
    b = Builder()
    blob(b, Vector((-0.02, 0, 0.03)), 0.055, (CUT_WOOD, CREAM), scale=(1.5, 0.55, 0.6), subdiv=1, jitter=0.003, seed=2, rot=(0, 0, 0.3))
    blob(b, Vector((0.05, 0.03, 0.025)), 0.045, (CREAM, CUT_WOOD), scale=(1.4, 0.55, 0.6), subdiv=1, jitter=0.003, seed=4, rot=(0, 0, -0.5))
    for i in range(5):
        a = -0.6 + i * 0.3
        prism_path(b, [Vector((-0.08, 0, 0.03)), Vector((-0.11, a * 0.05, 0.03))], [0.003, 0.0], 3, STRAW)
    leaf(b, Vector((0.0, 0.0, 0.05)), 1.6, 0.12, 0.03, deg(25), -0.4, OLIVE_LIGHT, under=OLIVE, thick=0.004)
    return b


def icon_lemon():
    b = lemon_fruit(Vector((0, 0, 0.04)), 1)
    b2 = b
    leaf(b2, Vector((0.0, 0.0, 0.075)), 0.3, 0.075, 0.042, deg(15), -0.4, GREEN_DARK, under=OLIVE_DARK, thick=0.004)
    return b2


def icon_sapling(leaf_cells, tag_cell, palm=False):
    b = Builder()
    blob(b, Vector((0, 0, 0.012)), 0.06, (SOIL, STEM_DARK), scale=(1.0, 1.0, 0.45), subdiv=1)
    if palm:
        prism_path(b, [Vector((0, 0, 0.012)), Vector((0, 0, 0.06))], [0.012, 0.01], 6, BARK_A, cell_end=BARK_B)
        for i in range(5):
            a = i * (2 * math.pi / 5)
            blade(b, Vector((0, 0, 0.06)), a, 0.12, 0.034, 0.003, deg(55), -deg(70), leaf_cells[i % 3], prof=(0.2, 0.7, 0.7, 0.4, 0.0), cell_under=OLIVE_DARK)
        blade(b, Vector((0, 0, 0.06)), 0.0, 0.09, 0.02, 0.003, deg(85), 0.0, leaf_cells[0], prof=(0.3, 0.8, 0.5, 0.0))
    else:
        prism_path(b, [Vector((0, 0, 0.012)), Vector((0.004, 0, 0.1)), Vector((0, 0, 0.2))], [0.009, 0.008, 0.006], 5, BARK_A, cell_end=BARK_B)
        for z, az, size, cell in ((0.09, 0.2, 0.07, leaf_cells[0]), (0.13, 3.4, 0.07, leaf_cells[1]), (0.17, 1.8, 0.06, leaf_cells[0]), (0.2, 4.9, 0.055, leaf_cells[2])):
            leaf(b, Vector((0, 0, z)), az, size, size * 0.65, deg(25), -0.5, cell, under=OLIVE_DARK, thick=0.003)
    box(b, Vector((0.012, 0, 0.12)), (0.02, 0.008, 0.026), tag_cell, rot=(0, 0, 0.2))
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


def stage_objs(name, builder, budgets):
    return [builder(s).finish(f"{name}_Stage{s}", budgets[s]) for s in range(len(budgets))]


add_plant("Tomato", stage_objs("Tomato", tomato, (160, 520, 900)), [], tomato(2, wild=True).finish("Tomato_Wild", 1100))
add_plant("Rice", stage_objs("Rice", rice, (200, 360, 700)), [], rice(2, paddy=False).finish("Rice_Wild", 600))

cran_pos = cran_fruit_positions()
add_plant("Cranberry", [cranberry(s)[0].finish(f"Cranberry_Stage{s}", (160, 420)[s]) for s in range(2)],
          [fruit_object(cran_cluster, p, i, f"Cranberry_Fruit{i}", 100) for i, p in enumerate(cran_pos)],
          cranberry(1, fruits=cran_pos)[0].finish("Cranberry_Wild", 700))

add_plant("WatermelonPlant", stage_objs("WatermelonPlant", watermelon, (80, 320, 640)))

palm_pos = date_positions()
add_plant("DatePalm", [datepalm_tree().finish("DatePalm_Tree", 1100)],
          [fruit_object(date_cluster, p, i, f"DatePalm_Fruit{i}", 160) for i, p in enumerate(palm_pos)])

add_plant("Oats", stage_objs("Oats", oats, (150, 300, 700)))
add_plant("Turnip", stage_objs("Turnip", turnip, (80, 260, 420)))

blue_pos = blue_fruit_positions()
add_plant("Blueberry", [blueberry(s)[0].finish(f"Blueberry_Stage{s}", (200, 520)[s]) for s in range(2)],
          [fruit_object(blue_cluster, p, i, f"Blueberry_Fruit{i}", 120) for i, p in enumerate(blue_pos)],
          blueberry(1, fruits=blue_pos)[0].finish("Blueberry_Wild", 900))

protos = collect_fronds()
cherry_b, cherry_k = build_tree(protos, CHERRY, CHERRY_LEAVES)
cherry_obj = cherry_b.finish("Cherry_Tree", 2800)
cherry_spots = crown_spots(CHERRY, cherry_k, (-0.2, -0.12, -0.04, 0.04, -0.16, 0.0), 41)
add_plant("Cherry", [cherry_obj], [cherry_fruit(p, i).finish(f"Cherry_Fruit{i}", 120) for i, p in enumerate(cherry_spots)])

citrus_b, citrus_k = build_tree(protos, CITRUS, CITRUS_LEAVES)
citrus_obj = citrus_b.finish("Citrus_Tree", 2800)
citrus_spots = crown_spots(CITRUS, citrus_k, (-0.16, -0.08, 0.0, 0.08, -0.12), 43)
add_plant("Citrus", [citrus_obj], [lemon_fruit(p, i).finish(f"Citrus_Fruit{i}", 160) for i, p in enumerate(citrus_spots)])

add_plant("Beans", stage_objs("Beans", beans, (180, 500, 820)), [], beans(2, wild=True).finish("Beans_Wild", 1000))
add_plant("Flax", stage_objs("Flax", flax, (150, 320, 700)))
add_plant("Cotton", stage_objs("Cotton", cotton, (120, 380, 600)))
add_plant("Chili", stage_objs("Chili", chili, (100, 340, 520)))
add_plant("Cattail", stage_objs("Cattail", cattail, (100, 300, 500)))

icons = {
    "Tomato": (icon_tomato().finish("Icon_Tomato", 300), ICON_USABLE),
    "Rice": (icon_rice().finish("Icon_Rice", 300), ICON_USABLE),
    "Cranberry": (icon_cranberry().finish("Icon_Cranberry", 700), ICON_USABLE),
    "Date": (icon_date().finish("Icon_Date", 400), ICON_USABLE),
    "Oats": (icon_oats().finish("Icon_Oats", 400), ICON_USABLE),
    "Turnip": (icon_turnip().finish("Icon_Turnip", 400), ICON_USABLE),
    "Blueberry": (icon_blueberry().finish("Icon_Blueberry", 700), ICON_USABLE),
    "Cherry": (icon_cherry().finish("Icon_Cherry", 400), ICON_USABLE),
    "Beans": (icon_beans().finish("Icon_Beans", 300), ICON_USABLE),
    "Flax": (icon_flax().finish("Icon_Flax", 400), ICON_USABLE),
    "Cotton": (icon_cotton().finish("Icon_Cotton", 600), ICON_USABLE),
    "Chili": (icon_chili().finish("Icon_Chili", 300), ICON_USABLE),
    "CattailRoot": (icon_cattail_root().finish("Icon_CattailRoot", 400), ICON_USABLE),
    "Lemon": (icon_lemon().finish("Icon_Lemon", 300), ICON_USABLE),
    "DatePalmSapling": (icon_sapling((GREEN_MID, OLIVE_MID, GREEN_DARK), TAN, palm=True).finish("Icon_DatePalmSapling", 400), ICON_RESOURCES),
    "CherrySapling": (icon_sapling((GREEN_MID, OLIVE_MID, LEAF_YG), RED).finish("Icon_CherrySapling", 300), ICON_RESOURCES),
    "LemonSapling": (icon_sapling((GREEN_DARK, GREEN_MID, OLIVE_MID), GOLD_LIGHT).finish("Icon_LemonSapling", 300), ICON_RESOURCES),
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
