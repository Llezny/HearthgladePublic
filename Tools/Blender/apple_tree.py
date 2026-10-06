"""Apple tree: a deciduous crown built from the Pine mesh's own foliage sprays (2026-10-03).

The apple tree used to be the Oak mesh squashed by the prefab (scale 0.70/0.41/0.70), which read as a
slim conifer, not a fruit tree. The user asked for a broad rounded crown like a stylised low-poly apple
tree "but with the leaves we already have on the spruce".

The spruce in this game is a recoloured Pine (see tree_species_from_pine.py), so its "leaves" are the
Pine mesh's foliage: 45-odd loose islands of ~24 verts / 14 quads each, a jagged drooping spray. This
script lifts those islands straight out of Pine.fbx and re-plants them, so the foliage is literally the
same geometry the rest of the tree cast uses - no new leaf shapes were invented.

Shape: a short forked trunk with 4 limbs (reference silhouette), then the sprays stacked in 5 horizontal
layers whose radius follows an oblate dome, plus a flat apex cap and a few inner sprays so the crown is
not hollow from above. Two things learned while fitting this:
  - sprays placed radially on a sphere with a random roll look like confetti; they only read as foliage
    stacked in horizontal layers the way they sit on the Pine.
  - layers alone leave a V-shaped notch at the top, because a layer stack never reaches the pole. The
    apex cap fixes it by starting each spray BEHIND the centre and running it across.

Output: 1520 tris, 0.63 x 0.62 x 0.70 m - the mature stage, sized to fit the 2x2 orchard plot (0.735 m)
with a margin. Two material slots (bark, leaf); Unity gets flat-colour materials per submesh, and
submesh order is matched by index count, not slot number (see [[workflow-new-tree-species]]).

Run through the Blender MCP (execute_blender_code) or Blender's Scripting tab. Safe mode applies: no
lambdas, no open(), plain defs only.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

PINE_FBX = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Trees/Pine.fbx"
OUT_FBX = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Farming/AppleTree.fbx"
TARGET_HEIGHT = 0.70

SEED = 5
TRUNK_TOP = 0.235
LIMB_ANGLES = [18.0, 104.0, 192.0, 283.0]
CZ, RX, RZ = 0.60, 0.355, 0.275          # crown centre and radii, in pre-normalised design units
LAYERS = [-0.225, -0.125, -0.02, 0.08, 0.172]

BARK, LEAF = 0, 1

# how far a spray is pulled onto the branch network (fraction of the gap kept / cap) and the twig that bridges the rest
HOOK_KEEP = 0.5
HOOK_MAX = 0.07
TWIG_R0, TWIG_R1 = 0.0085, 0.006
SECONDARY_HEIGHTS = [CZ - 0.17, CZ - 0.03, CZ + 0.09, CZ + 0.17]


def islands_of(bm):
    """Loose connected components - Pine's foliage sprays are separate shells, one per branch."""
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


def proto_cost(p):
    return len(p[0])


def collect_fronds(pine_mesh):
    """Each spray, normalised: inner end at the origin, pointing +X, centred vertically."""
    sbm = bmesh.new()
    sbm.from_mesh(pine_mesh)
    sbm.verts.ensure_lookup_table()
    sbm.faces.ensure_lookup_table()
    protos = []
    for comp in islands_of(sbm):
        if not (18 <= len(comp) <= 40):          # skips the trunk shell and the stray stubs
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
    protos.sort(key=proto_cost)
    return protos


def add_tube(bm, pts, radii, sides, mat_index):
    rings = []
    n = len(pts)
    for i in range(n):
        if i == 0:
            d = (pts[1] - pts[0]).normalized()
        elif i == n - 1:
            d = (pts[-1] - pts[-2]).normalized()
        else:
            d = (pts[i + 1] - pts[i - 1]).normalized()
        q = d.to_track_quat('Z', 'Y')
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            ring.append(bm.verts.new(pts[i] + q @ Vector((math.cos(a) * radii[i], math.sin(a) * radii[i], 0))))
        rings.append(ring)
    for i in range(n - 1):
        for s in range(sides):
            s2 = (s + 1) % sides
            f = bm.faces.new([rings[i][s], rings[i][s2], rings[i + 1][s2], rings[i + 1][s]])
            f.material_index = mat_index
    bm.faces.new(list(reversed(rings[0]))).material_index = mat_index
    bm.faces.new(rings[-1]).material_index = mat_index


def rot_to_x(dirv, roll):
    x = dirv.normalized()
    up = Vector((0, 0, 1))
    if abs(x.dot(up)) > 0.95:
        up = Vector((0, 1, 0))
    y = up.cross(x).normalized()
    z = x.cross(y).normalized()
    m = Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z))).to_4x4()
    return Matrix.Rotation(roll, 4, x) @ m


def place_frond(bm, proto, origin, dirv, scale, roll):
    coords, faces, _ = proto
    rot = rot_to_x(dirv, roll)
    vs = []
    for c in coords:
        vs.append(bm.verts.new(origin + (rot @ (c * scale))))
    for f in faces:
        try:
            bm.faces.new([vs[i] for i in f]).material_index = LEAF
        except ValueError:          # a duplicate face from an overlapping spray, harmless
            pass


def curve_pts(start, end, bow, steps):
    out = []
    mid = (start + end) * 0.5 + bow
    for i in range(steps + 1):
        t = i / steps
        out.append(start * ((1 - t) ** 2) + mid * (2 * (1 - t) * t) + end * (t * t))
    return out


def bez(start, end, bow, t):
    mid = (start + end) * 0.5 + bow
    return start * ((1 - t) ** 2) + mid * (2 * (1 - t) * t) + end * (t * t)


def add_branch(bm, samples, start, end, bow, radii, sides, steps=2):
    """A bark tube along curve_pts, remembering points on it so sprays can hook onto the branch network."""
    add_tube(bm, curve_pts(start, end, bow, steps), radii, sides, BARK)
    for i in range(7):
        samples.append(bez(start, end, bow, i / 6))


def hook_to_branch(bm, samples, attach):
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
    keep = min(best * HOOK_KEEP, HOOK_MAX)
    new = near + gap.normalized() * keep
    add_tube(bm, [near, new], [TWIG_R0, TWIG_R1], 3, BARK)
    return new


def flat_material(name, rgb):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    node = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    node.inputs["Base Color"].default_value = rgb
    return mat


def import_pine():
    old = bpy.data.objects.get("PineBase")
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    before = set(o.name for o in bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=PINE_FBX)
    new = set(o.name for o in bpy.data.objects) - before
    obj = bpy.data.objects[next(iter(new))]
    obj.name = "PineBase"
    for o in bpy.data.objects:
        o.select_set(o.name == "PineBase")
    bpy.context.view_layer.objects.active = obj
    # the re-imported FBX carries an unapplied 90 deg rotation - bake it or the sprays come out sideways
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def build():
    random.seed(SEED)
    protos = collect_fronds(bpy.data.objects["PineBase"].data)
    small = protos[:max(8, len(protos) * 2 // 3)]

    old = bpy.data.objects.get("AppleTree")
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)

    bm = bmesh.new()

    add_tube(bm, [Vector((0, 0, 0)), Vector((0, 0, 0.07)), Vector((0.004, 0.003, 0.16)), Vector((0, 0.003, TRUNK_TOP))],
             [0.058, 0.044, 0.038, 0.035], 7, BARK)

    # the spray layout below draws from `random`; the branches use their own generator so adding or changing
    # branches never reshuffles where the leaves are
    branch_rng = random.Random(77)
    samples = []
    limbs = []

    # central leader up to the crown apex, so the inner and apex sprays have something to sit on
    add_branch(bm, samples, Vector((0, 0.003, TRUNK_TOP - 0.02)), Vector((0.0, 0.0, CZ + RZ * 0.78)),
               Vector((0.012, 0.0, 0.0)), [0.026, 0.019, 0.012, 0.007], 5, 3)

    for deg in LIMB_ANGLES:
        a = math.radians(deg + random.uniform(-10, 10))
        reach = random.uniform(0.12, 0.16)
        top = random.uniform(0.44, 0.53)
        start = Vector((0, 0.003, TRUNK_TOP - 0.012))
        end = Vector((math.cos(a) * reach, math.sin(a) * reach, top))
        bow = Vector((math.cos(a) * -0.032, math.sin(a) * -0.032, 0.03))
        pts = curve_pts(start, end, bow, 3)
        add_tube(bm, pts, [0.032, 0.024, 0.017, 0.012], 6, BARK)
        for i in range(7):
            samples.append(bez(start, end, bow, i / 6))
        limbs.append((start, end, bow, a))
        a2 = a + random.uniform(-0.8, 0.8)
        rr = reach + random.uniform(0.05, 0.1)
        e2 = Vector((math.cos(a2) * rr, math.sin(a2) * rr, top + random.uniform(0.04, 0.11)))
        add_branch(bm, samples, pts[2], e2, Vector((0, 0, 0.025)), [0.014, 0.010, 0.007], 5)

    # secondary branches: three per limb, each climbing out to one of the crown layers
    for li, (start, end, bow, a) in enumerate(limbs):
        for k, tf in enumerate([0.55, 0.8, 1.0]):
            s0 = bez(start, end, bow, tf)
            a2 = a + branch_rng.uniform(-0.9, 0.9) + (0.6 if k == 1 else (-0.6 if k == 0 else 0.0))
            r2 = branch_rng.uniform(0.15, 0.22)          # ends stay under the sprays, no bare stubs poking out
            h2 = SECONDARY_HEIGHTS[(li + k * 2) % len(SECONDARY_HEIGHTS)] + branch_rng.uniform(-0.02, 0.02)
            e2 = Vector((math.cos(a2) * r2, math.sin(a2) * r2, h2))
            add_branch(bm, samples, s0, e2, Vector((0, 0, 0.03)), [0.017, 0.012, 0.007], 5)

    phase = 0.0
    for dz in LAYERS:
        frac = max(0.34, math.sqrt(max(0.0, 1 - (dz / RZ) ** 2)))
        lr = RX * frac
        count = max(6, int(round(lr * 25)))
        phase += 0.8
        for i in range(count):
            a = phase + 2 * math.pi * i / count + random.uniform(-0.13, 0.13)
            dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.1, 0.06) + dz * 0.5))
            proto = small[random.randrange(len(small))]
            flen = lr * random.uniform(0.62, 0.78)
            ar = lr - flen * random.uniform(0.66, 0.86)
            attach = Vector((math.cos(a) * ar, math.sin(a) * ar, CZ + dz + random.uniform(-0.018, 0.018)))
            roll = random.uniform(-0.28, 0.28)
            attach = hook_to_branch(bm, samples, attach)
            place_frond(bm, proto, attach, dirv, flen / proto[2], roll)

    # apex cap - starts behind the centre so the sprays run across the pole the layers never reach
    for i in range(5):
        a = 2 * math.pi * i / 5 + 0.35
        proto = small[random.randrange(len(small))]
        flen = RX * random.uniform(0.48, 0.6)
        attach = Vector((-math.cos(a) * flen * 0.44, -math.sin(a) * flen * 0.44, CZ + RZ * random.uniform(0.76, 0.9)))
        dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.03, 0.1)))
        roll = random.uniform(-0.18, 0.18)
        attach = hook_to_branch(bm, samples, attach)
        place_frond(bm, proto, attach, dirv, flen / proto[2], roll)

    for i in range(6):
        a = random.uniform(0, math.tau)
        rr = random.uniform(0.02, 0.1)
        proto = small[random.randrange(len(small))]
        attach = Vector((math.cos(a) * rr, math.sin(a) * rr, CZ + random.uniform(-0.1, 0.12)))
        dirv = Vector((math.cos(a), math.sin(a), random.uniform(-0.12, 0.2)))
        flen = RX * random.uniform(0.36, 0.48)
        roll = random.uniform(-0.28, 0.28)
        attach = hook_to_branch(bm, samples, attach)
        place_frond(bm, proto, attach, dirv, flen / proto[2], roll)

    me = bpy.data.meshes.new("AppleTree")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("AppleTree", me)
    bpy.context.scene.collection.objects.link(obj)
    me.materials.append(flat_material("AppleTree_Bark", (0.42, 0.30, 0.22, 1)))
    me.materials.append(flat_material("AppleTree_Leaf", (0.30, 0.49, 0.21, 1)))

    zs = [v.co.z for v in me.vertices]
    k = TARGET_HEIGHT / (max(zs) - min(zs))
    zmin = min(zs)
    for v in me.vertices:
        v.co = Vector((v.co.x * k, v.co.y * k, (v.co.z - zmin) * k))
    me.update()
    print("AppleTree verts", len(me.vertices), "polys", len(me.polygons),
          "tris", sum(len(p.vertices) - 2 for p in me.polygons))
    print("dims", [round(d, 3) for d in obj.dimensions])
    return obj


def export(obj):
    for o in bpy.data.objects:
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    # bake_space_transform is what keeps the tree upright in Unity - without it the Z-up mesh lies on its side
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             global_scale=1.0, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False)
    print("exported", OUT_FBX)


import_pine()
export(build())
