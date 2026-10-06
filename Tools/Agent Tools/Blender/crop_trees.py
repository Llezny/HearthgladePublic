"""Fruit trees built from the Pine mesh's own foliage sprays (the technique of apple_tree.py), shared by the crop scripts.

The spruce/oak/apple in this game are recoloured Pine derivatives, and the user rejected icosphere canopies, so every
tree here lifts the loose foliage islands out of Pine.fbx and re-plants them on a branch network. Faces get a flat
palette cell (bark / leaf cells), so the mesh needs no material of its own.

build_tree(protos, P, leaf_cells) -> (Builder, k): P is a dict of crown parameters (see PEAR in crops_batch1.py),
k the scale that fitted the tree to P["target_height"]. crown_spots() gives fruit positions on the crown shell.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

from crop_lib import *  # noqa: F401,F403

PINE_FBX = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Trees/Pine.fbx"


def _islands(bm):
    seen = set()
    out = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        comp = []
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.link_edges:
                ov = e.other_vert(cur)
                if ov.index not in seen:
                    seen.add(ov.index)
                    stack.append(ov)
        out.append(comp)
    return out


def collect_fronds():
    """Each foliage spray of the Pine, normalised: inner end at the origin, pointing +X, centred vertically."""
    before = set(o.name for o in bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=PINE_FBX)
    new = [o for o in bpy.data.objects if o.name not in before]
    obj = next(o for o in new if o.type == "MESH")
    for o in bpy.data.objects:
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    sbm = bmesh.new()
    sbm.from_mesh(obj.data)
    sbm.verts.ensure_lookup_table()
    sbm.faces.ensure_lookup_table()
    protos = []
    for comp in _islands(sbm):
        if not (18 <= len(comp) <= 40):
            continue
        cen = Vector((0, 0, 0))
        for v in comp:
            cen = cen + v.co
        cen = cen / len(comp)
        if cen.z < 0.45 or math.sqrt(cen.x * cen.x + cen.y * cen.y) < 0.04:
            continue
        rot = Matrix.Rotation(-math.atan2(cen.y, cen.x), 4, Vector((0, 0, 1)))
        idx = {}
        coords = []
        for v in comp:
            idx[v.index] = len(coords)
            coords.append(rot @ (v.co - Vector((0, 0, cen.z))))
        minx = min(c.x for c in coords)
        zc = sum(c.z for c in coords) / len(coords)
        coords = [Vector((c.x - minx, c.y, c.z - zc)) for c in coords]
        faces = []
        done = set()
        for v in comp:
            for f in v.link_faces:
                if f.index in done:
                    continue
                done.add(f.index)
                if all(fv.index in idx for fv in f.verts):
                    faces.append([idx[fv.index] for fv in f.verts])
        protos.append((coords, faces, max(c.x for c in coords)))
    sbm.free()
    bpy.data.objects.remove(obj, do_unlink=True)
    protos.sort(key=lambda p: len(p[0]))
    return protos


def rot_to_x(dirv, roll):
    x = dirv.normalized()
    up = Vector((0, 0, 1))
    if abs(x.dot(up)) > 0.95:
        up = Vector((0, 1, 0))
    y = up.cross(x).normalized()
    z = x.cross(y).normalized()
    m = Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z))).to_4x4()
    return Matrix.Rotation(roll, 4, x) @ m


def place_frond(b, proto, origin, dirv, scale, roll, cell):
    coords, faces, _ = proto
    rot = rot_to_x(dirv, roll)
    verts = [b.bm.verts.new(origin + (rot @ (c * scale))) for c in coords]
    for f in faces:
        try:
            b.face([verts[i] for i in f], cell)
        except ValueError:
            pass


def hook_to_branch(b, samples, attach):
    """Moves a spray's inner end onto the nearest branch and bridges the rest with a thin twig, so no spray floats."""
    near = samples[0]
    best = (attach - near).length
    for p in samples:
        d = (attach - p).length
        if d < best:
            best = d
            near = p
    gap = attach - near
    if best < 0.012:
        return attach
    keep = min(best * 0.5, 0.07)
    new = near + gap.normalized() * keep
    prism_path(b, [near, new], [0.0085, 0.006], 3, BARK_A)
    return new


def _bezier(p0, p1, p2, t):
    return p0 * ((1 - t) ** 2) + p1 * (2 * (1 - t) * t) + p2 * (t * t)


def _bezier_pts(p0, p1, p2, steps):
    return [_bezier(Vector(p0), Vector(p1), Vector(p2), i / steps) for i in range(steps + 1)]


def shell_fraction(P, dz):
    """Crown radius at height dz as a fraction of rx (an oblate dome, optionally fuller at the bottom)."""
    return max(0.34, math.sqrt(max(0.0, 1 - (dz / P["rz"]) ** 2))) * (1.0 + P.get("bias", 0.0) * (-dz / P["rz"]))


def build_tree(protos, P, leaf_cells):
    random.seed(P["seed"])
    small = protos[:max(8, len(protos) * 2 // 3)]
    b = Builder()
    tt, cz, rx, rz = P["trunk_top"], P["cz"], P["rx"], P["rz"]
    prism_path(b, [Vector((0, 0, 0)), Vector((0, 0, 0.07)), Vector((0.004, 0.003, tt * 0.68)), Vector((0, 0.003, tt))],
               [0.056, 0.042, 0.036, 0.033], 7, BARK_A, cell_end=BARK_B)
    branch_rng = random.Random(77)
    samples = []
    limbs = []

    def add_branch(start, end, bow, radii, sides, steps=2):
        pts = _bezier_pts(start, (start + end) * 0.5 + bow, end, steps)
        prism_path(b, pts, radii, sides, BARK_A, cell_end=BARK_B)
        for i in range(7):
            samples.append(_bezier(start, (start + end) * 0.5 + bow, end, i / 6))

    add_branch(Vector((0, 0.003, tt - 0.02)), Vector((0.0, 0.0, cz + rz * 0.78)), Vector((0.012, 0.0, 0.0)),
               [0.026, 0.019, 0.012, 0.007], 5, 3)
    reach_lo, reach_hi = P["limb_reach"]
    top_lo, top_hi = P["limb_top"]
    for deg_a in P["limb_angles"]:
        a = math.radians(deg_a + random.uniform(-10, 10))
        reach = random.uniform(reach_lo, reach_hi)
        top = random.uniform(top_lo, top_hi)
        start = Vector((0, 0.003, tt - 0.012))
        end = Vector((math.cos(a) * reach, math.sin(a) * reach, top))
        bow = Vector((math.cos(a) * -0.03, math.sin(a) * -0.03, 0.03))
        pts = _bezier_pts(start, (start + end) * 0.5 + bow, end, 3)
        prism_path(b, pts, [0.032, 0.024, 0.017, 0.012], 6, BARK_A, cell_end=BARK_B)
        for i in range(7):
            samples.append(_bezier(start, (start + end) * 0.5 + bow, end, i / 6))
        limbs.append((start, end, bow, a))
        a2 = a + random.uniform(-0.8, 0.8)
        e2 = Vector((math.cos(a2) * (reach + random.uniform(0.04, 0.08)), math.sin(a2) * (reach + random.uniform(0.04, 0.08)),
                     top + random.uniform(0.05, 0.12)))
        add_branch(pts[2], e2, Vector((0, 0, 0.025)), [0.014, 0.010, 0.007], 5)
    heights = [cz + f * rz for f in (-0.61, -0.28, 0.05, 0.39, 0.61)]
    sec_lo, sec_hi = P.get("secondary_r", (0.12, 0.2))
    for li, (start, end, bow, a) in enumerate(limbs):
        for k, tf in enumerate([0.55, 0.8, 1.0]):
            s0 = _bezier(start, (start + end) * 0.5 + bow, end, tf)
            a2 = a + branch_rng.uniform(-0.9, 0.9) + (0.6 if k == 1 else (-0.6 if k == 0 else 0.0))
            r2 = branch_rng.uniform(sec_lo, sec_hi)
            h2 = heights[(li + k * 2) % len(heights)] + branch_rng.uniform(-0.02, 0.02)
            add_branch(s0, Vector((math.cos(a2) * r2, math.sin(a2) * r2, h2)), Vector((0, 0, 0.03)), [0.017, 0.012, 0.007], 5)

    phase = 0.0
    for dz in P["layers"]:
        lr = rx * shell_fraction(P, dz)
        count = max(6, int(round(lr * 25)))
        phase += 0.8
        for i in range(count):
            a = phase + 2 * math.pi * i / count + random.uniform(-0.13, 0.13)
            dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.1, 0.06) + dz * 0.5))
            proto = small[random.randrange(len(small))]
            flen = lr * random.uniform(0.62, 0.78)
            ar = lr - flen * random.uniform(0.66, 0.86)
            attach = Vector((math.cos(a) * ar, math.sin(a) * ar, cz + dz + random.uniform(-0.018, 0.018)))
            attach = hook_to_branch(b, samples, attach)
            place_frond(b, proto, attach, dirv, flen / proto[2], random.uniform(-0.28, 0.28), leaf_cells[(i + len(samples)) % 3])
    for i in range(5):
        a = 2 * math.pi * i / 5 + 0.35
        proto = small[random.randrange(len(small))]
        flen = rx * random.uniform(0.46, 0.58)
        attach = Vector((-math.cos(a) * flen * 0.44, -math.sin(a) * flen * 0.44, cz + rz * random.uniform(0.74, 0.88)))
        dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.03, 0.1)))
        attach = hook_to_branch(b, samples, attach)
        place_frond(b, proto, attach, dirv, flen / proto[2], random.uniform(-0.18, 0.18), leaf_cells[i % 3])
    for i in range(6):
        a = random.uniform(0, math.tau)
        rr = random.uniform(0.02, 0.1)
        proto = small[random.randrange(len(small))]
        attach = Vector((math.cos(a) * rr, math.sin(a) * rr, cz + random.uniform(-0.12, 0.14)))
        dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.12, 0.2)))
        attach = hook_to_branch(b, samples, attach)
        place_frond(b, proto, attach, dirv, rx * random.uniform(0.34, 0.46) / proto[2], random.uniform(-0.28, 0.28), leaf_cells[i % 3])

    zs = [v.co.z for v in b.bm.verts]
    k = P["target_height"] / (max(zs) - min(zs))
    zmin = min(zs)
    b.transform_since(0, Matrix.Scale(k, 4) @ Matrix.Translation(Vector((0, 0, -zmin))))
    return b, k


def crown_spots(P, k, dzs, seed):
    """One spot per entry of `dzs` (height offsets from the crown centre) on the crown shell, final-scale coordinates."""
    rng = random.Random(seed)
    count = len(dzs)
    spots = []
    for i, dz in enumerate(dzs):
        a = i * (2 * math.pi / count) + 0.5 + rng.uniform(-0.2, 0.2)
        radius = P["rx"] * shell_fraction(P, dz) * 0.92
        spots.append(Vector((math.cos(a) * radius * k, math.sin(a) * radius * k, (P["cz"] + dz) * k)))
    return spots
