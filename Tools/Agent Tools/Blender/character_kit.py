"""Modular humanoid character kit (docs/EXPLORATION_LOOP_PLAN.md, W5 / 8a.1): one skeleton and swappable clothing parts.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python character_kit.py
Exports Character.fbx into EXPORT_DIR (one armature, every part variant as its own skinned mesh) and previews into PREVIEW_DIR.

The skeleton is a Mixamo one (mixamorig:* bones, T-pose) like the player's, so Unity can build a Humanoid avatar from it and the
player's Humanoid clips (Idle, Walk, ...) play on every character. The figure is a chunky, big headed little person of 1 m (the player's
proportions) then stretched and slimmed by reshape(): a longer, narrower body under the same head; the game scales it by 0.3.

Three slots, each with variants (a part is one skinned mesh, rigidly weighted per bone with soft joints, no material of its own):
  Head   - head with hair, hat or scarf
  Torso  - the body: shirt or dress, arms, hands AND the trousers or tights (so the body piece decides the legs as well)
  Shoes  - boots or shoes
Colours are flat palette cells (Colorsheet Tree Normal.png) as everywhere else, each face with a colour role (skin, hair, top, ...) that
Unity can recolour per character. Two outfits for now: Orchardist and Herbalist.

Metres, Z up, the character faces -Y (it faces +Z in Unity), left hand at +X, feet on Z = 0.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from animal_common import clear_scene, face_out, palette_material, cell_uv  # noqa: E402

V = Vector
EXPORT_PATH = "E:/Repos/Hearthglade/Assets/Arts/Models/Characters/Character.fbx"
PREVIEW_DIR = os.environ.get("CHARACTER_PREVIEW_DIR", "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/785ab20c-a7ba-42ab-8eb1-22f3ad57df45/scratchpad")

# Palette cells (row, col), see harbor_props.py.
class Col:
    """A palette cell plus the colour role of the faces: a role can be recoloured per character in Unity (CharacterColorRole)."""

    def __init__(self, cell, role="Fixed"):
        self.cell = cell
        self.role = ROLES.index(role)


# Must match CharacterColorRole in Assets/Scripts/Gameplay/Characters/CharacterColorRole.cs.
ROLES = ["Fixed", "Skin", "Hair", "Top", "TopTrim", "Legs", "Hat", "HatBand", "Shoes"]

BLACK = Col((0, 0))
STRAP = Col((0, 1))
HAIR_DARK = Col((0, 1), "Hair")
HAIR_BROWN = Col((5, 4), "Hair")
SKIN_TAN = Col((0, 3), "Skin")
SKIN_PEACH = Col((1, 6), "Skin")
SHIRT_CREAM = Col((3, 0), "Top")
TRIM_CREAM = Col((3, 0), "TopTrim")
APRON_GREEN = Col((4, 1), "TopTrim")
TROUSERS_BROWN = Col((2, 5), "Legs")
TIGHTS_WHITE = Col((0, 7), "Legs")
BOOT = Col((3, 1), "Shoes")
SHOE_TAN = Col((6, 1), "Shoes")
STRAW = Col((5, 1), "Hat")
HAT_BAND = Col((4, 3), "HatBand")
DRESS_ORCHID = Col((3, 4), "Top")
SCARF_RED = Col((6, 0), "Hat")

BONES = [  # name, parent, head, tail (the left side is mirrored)
    ("Hips", None, (0, 0, 0.31), (0, 0, 0.36)),
    ("Spine", "Hips", (0, 0, 0.36), (0, 0, 0.43)),
    ("Spine1", "Spine", (0, 0, 0.43), (0, 0, 0.50)),
    ("Spine2", "Spine1", (0, 0, 0.50), (0, 0, 0.585)),
    ("Neck", "Spine2", (0, 0, 0.585), (0, 0, 0.625)),
    ("Head", "Neck", (0, 0, 0.625), (0, 0, 0.99)),
]
SIDED = [  # name, parent, head, tail for the +X side
    ("Shoulder", "Spine2", (0.04, 0, 0.545), (0.17, 0, 0.545)),
    ("Arm", "Shoulder", (0.17, 0, 0.545), (0.28, 0, 0.545)),
    ("ForeArm", "Arm", (0.28, 0, 0.545), (0.39, 0, 0.545)),
    ("Hand", "ForeArm", (0.39, 0, 0.545), (0.47, 0, 0.545)),
    # The knee sits a little in front of the line hip-ankle: Unity finds the hinge of the knee (and with it the axes of the whole leg)
    # from that bend, so a perfectly straight leg gives a Humanoid avatar whose leg muscles swing in odd directions.
    ("UpLeg", "Hips", (0.07, 0, 0.29), (0.07, -0.02, 0.15)),
    ("Leg", "UpLeg", (0.07, -0.02, 0.15), (0.07, 0, 0.05)),
    ("Foot", "Leg", (0.07, 0, 0.05), (0.07, -0.075, 0.02)),
    ("ToeBase", "Foot", (0.07, -0.075, 0.02), (0.07, -0.14, 0.02)),
]


def bone_list():
    result = [("mixamorig:" + n, ("mixamorig:" + p) if p else None, V(h), V(t)) for n, p, h, t in BONES]
    for n, p, h, t in SIDED:
        for side, sign in (("Left", 1), ("Right", -1)):
            mirror = lambda v: V((v[0] * sign, v[1], v[2]))
            parent = p if p in ("Spine2", "Hips") else side + p
            result.append(("mixamorig:" + side + n, "mixamorig:" + parent, mirror(h), mirror(t)))
    return result


BONE_LIST = bone_list()
BONE_INDEX = {name: i for i, (name, _, _, _) in enumerate(BONE_LIST)}


# ---- proportions ----
# Everything is modelled in the "design space" (the 1 m chunky figure; the skin weights are functions of it) and moved into the final
# shape at the moment a vertex or a bone is made, so the mesh and the skeleton always agree.
LEG_STRETCH = 1.65    # the legs (from the ankle to the hips) get longer by this factor: the design has stumps
TORSO_STRETCH = 1.0   # the torso (hips to neck) stays as it was; the head keeps its size
TORSO_SLIM = 0.88     # width and depth of the torso, the neck and the skirt
LEG_SLIM = 0.95
ARM_SLIM = 0.90       # thickness of the arms
ARM_LENGTH = 1.0
HEAD_SCALE = 0.94     # the head gets a little smaller so the narrower shoulders do not make it a balloon
FOOT_Z = 0.05         # below this the foot is not stretched
HIP_Z = 0.31          # the hips bone in the design space
NECK_Z = 0.625        # the head starts here in the design space
ARM_Z = 0.545         # the axis of the arms
SHOULDER_X = 0.13     # the arm starts here (inside the torso)

HEAD_BONES = {"Head"}
ARM_BONES = {"Shoulder", "Arm", "ForeArm", "Hand"}
LEG_BONES = {"UpLeg", "Leg", "Foot", "ToeBase"}
WEIGHT_KINDS = {"weights_arm": "arm", "weights_leg": "leg", "weights_leg_point": "leg", "weights_head": "head"}


def body_z(z):
    if z <= FOOT_Z:
        return z
    hips = FOOT_Z + (HIP_Z - FOOT_Z) * LEG_STRETCH
    if z <= HIP_Z:
        return FOOT_Z + (z - FOOT_Z) * LEG_STRETCH
    return hips + (z - HIP_Z) * TORSO_STRETCH


def reshape(kind, p):
    """The final position of a design space point p; kind is "head", "arm", "leg" or "torso" (torso, neck, skirt)."""
    x, y, z = p
    if kind == "head":
        return V((x * HEAD_SCALE, y * HEAD_SCALE, body_z(NECK_Z) + (z - NECK_Z) * HEAD_SCALE))
    if kind == "arm":
        sign = 1.0 if x >= 0 else -1.0
        ax = abs(x)
        ax = ax * TORSO_SLIM if ax <= SHOULDER_X else SHOULDER_X * TORSO_SLIM + (ax - SHOULDER_X) * ARM_LENGTH
        return V((sign * ax, y * ARM_SLIM, body_z(ARM_Z) + (z - ARM_Z) * ARM_SLIM))
    if kind == "leg":
        return V((x * LEG_SLIM, y * LEG_SLIM, body_z(z)))
    return V((x * TORSO_SLIM, y * TORSO_SLIM, body_z(z)))


def bone_kind(name):
    name = name.replace("mixamorig:", "").replace("Left", "").replace("Right", "")
    return "head" if name in HEAD_BONES else "arm" if name in ARM_BONES else "leg" if name in LEG_BONES else "torso"


def build_armature():
    data = bpy.data.armatures.new("Armature")
    obj = bpy.data.objects.new("Armature", data)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for name, parent, head, tail in BONE_LIST:
        bone = data.edit_bones.new(name)
        kind = bone_kind(name)
        bone.head, bone.tail = reshape(kind, head), reshape(kind, tail)
        bone.roll = 0.0
        if parent:
            bone.parent = made[parent]
        made[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


# ---- skin weights: a function of the rest position, soft near the joints ----

def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def ramp(t, a, b, t0, t1):
    """Bone a at t <= t0, bone b at t >= t1, a smooth blend between (t0 may be larger than t1: the axis runs downwards)."""
    s = smooth((t - t0) / (t1 - t0))
    return {a: 1.0 - s, b: s}


def torso_w(z):
    if z < 0.33:
        return {"Hips": 1.0}
    if z < 0.37:
        return ramp(z, "Hips", "Spine", 0.33, 0.37)
    if z < 0.41:
        return {"Spine": 1.0}
    if z < 0.45:
        return ramp(z, "Spine", "Spine1", 0.41, 0.45)
    if z < 0.49:
        return {"Spine1": 1.0}
    if z < 0.53:
        return ramp(z, "Spine1", "Spine2", 0.49, 0.53)
    if z < 0.575:
        return {"Spine2": 1.0}
    return ramp(z, "Spine2", "Neck", 0.575, 0.615)


def arm_w(side, x):
    x = abs(x)
    if x < 0.12:
        return {"Spine2": 1.0}
    if x < 0.20:
        return ramp(x, "Spine2", side + "Arm", 0.12, 0.20)
    if x < 0.25:
        return {side + "Arm": 1.0}
    if x < 0.31:
        return ramp(x, side + "Arm", side + "ForeArm", 0.25, 0.31)
    if x < 0.36:
        return {side + "ForeArm": 1.0}
    if x < 0.42:
        return ramp(x, side + "ForeArm", side + "Hand", 0.36, 0.42)
    return {side + "Hand": 1.0}


def leg_w(side, p):
    z = p.z
    if z > 0.32:
        w = {"Hips": 1.0}
    elif z > 0.26:
        w = ramp(z, "Hips", side + "UpLeg", 0.32, 0.26)
    elif z > 0.18:
        w = {side + "UpLeg": 1.0}
    elif z > 0.12:
        w = ramp(z, side + "UpLeg", side + "Leg", 0.18, 0.12)
    elif z > 0.08:
        w = {side + "Leg": 1.0}
    elif z > 0.02:
        w = ramp(z, side + "Leg", side + "Foot", 0.08, 0.02)
    else:
        w = {side + "Foot": 1.0}
    # The toes follow the toe bone.
    s = smooth((-p.y - 0.05) / 0.05)
    if s > 0 and side + "Foot" in w:
        moved = w[side + "Foot"] * s
        w[side + "Foot"] -= moved
        w[side + "ToeBase"] = moved
    return w


def weights_arm(p):
    return arm_w("Left" if p.x >= 0 else "Right", p.x)


def weights_leg(p):
    return leg_w("Left" if p.x >= 0 else "Right", p)


def weights_torso(p):
    return torso_w(p.z)


def weights_head(p):
    return {"Head": 1.0}


def weights_neck(p):
    return torso_w(p.z)


def weights_skirt(p):
    return {"Hips": 1.0} if p.z < 0.38 else torso_w(p.z)


# ---- geometry ----

class Part:
    def __init__(self):
        self.bm = bmesh.new()
        self.layer = self.bm.verts.layers.deform.verify()
        self.faces = []
        self.design = {}  # vertex -> its position in the design space

    def centre(self, face):
        """The centre of a face in the design space: the colour rules (hair from z up, ...) are written in it."""
        return sum((self.design[v] for v in face.verts), V()) / len(face.verts)

    def vert(self, co, wfn):
        v = self.bm.verts.new(reshape(WEIGHT_KINDS.get(wfn.__name__, "torso"), co))
        self.design[v] = V(co)
        weights = {("mixamorig:" + k): w for k, w in wfn(co).items() if w > 1e-4}
        total = sum(weights.values())
        for name, w in weights.items():
            v[self.layer][BONE_INDEX[name]] = w / total
        return v


def rect(cx, cy, hx, hy, cut=0.0):
    """A rectangle (chamfered when cut > 0) as points around (cx, cy), counter clockwise."""
    if cut <= 0:
        return [(cx - hx, cy - hy), (cx + hx, cy - hy), (cx + hx, cy + hy), (cx - hx, cy + hy)]
    return [(cx - hx + cut, cy - hy), (cx + hx - cut, cy - hy), (cx + hx, cy - hy + cut), (cx + hx, cy + hy - cut),
            (cx + hx - cut, cy + hy), (cx - hx + cut, cy + hy), (cx - hx, cy + hy - cut), (cx - hx, cy - hy + cut)]


def to3(axis, pos, a, b):
    if axis == "z":
        return V((a, b, pos))
    if axis == "x":
        return V((pos, a, b))
    return V((a, pos, b))


def loft(part, axis, stations, wfn, colour, seg_colours=None, caps=True):
    """A prism through stations = [(position along the axis, [(a, b), ...]), ...]; every station has the same number of points.

    colour is a palette cell or a function (face centre, normal) -> cell; seg_colours = {segment index: cell} overrides it.
    """
    rings = [[part.vert(to3(axis, pos, a, b), wfn) for a, b in poly] for pos, poly in stations]
    centres = [sum((v.co for v in ring), V()) / len(ring) for ring in rings]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        ref = (centres[i] + centres[i + 1]) / 2
        for k in range(n):
            face = part.bm.faces.new([rings[i][k], rings[i][(k + 1) % n], rings[i + 1][(k + 1) % n], rings[i + 1][k]])
            face_out(face, ref)
            if seg_colours and i in seg_colours:
                cell = seg_colours[i]
            elif callable(colour):
                cell = colour(part.centre(face), face.normal)
            else:
                cell = colour
            part.faces.append((face, cell))
    if caps:
        for ring, other in ((rings[0], centres[-1]), (rings[-1], centres[0])):
            face = part.bm.faces.new(ring)
            face_out(face, other)
            cap_cell = colour(part.centre(face), face.normal) if callable(colour) else colour
            part.faces.append((face, cap_cell))


def quad(part, corners, cell, wfn):
    face = part.bm.faces.new([part.vert(c, wfn) for c in corners])
    part.faces.append((face, cell))


def finish(part, name, armature):
    bm = part.bm
    uv_layer = bm.loops.layers.uv.new("UVMap")
    # The second UV set tells Unity which colour role a face has (u = (role + 0.5) / 16), so it can recolour per character.
    role_layer = bm.loops.layers.uv.new("Role")
    for face, col in part.faces:
        u, v = cell_uv(col.cell)
        for loop in face.loops:
            loop[uv_layer].uv = (u, v)
            loop[role_layer].uv = ((col.role + 0.5) / 16, 0.5)
        face.smooth = False
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(palette_material())
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for bone_name, _, _, _ in BONE_LIST:
        obj.vertex_groups.new(name=bone_name)
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature
    obj.parent = armature
    mesh.calc_loop_triangles()
    print(f"part {name}: {len(mesh.loop_triangles)} tris")
    return obj


# ---- the head (shared shape) ----

HEAD_RINGS = [  # z, half width, half depth, chamfer
    (0.625, 0.10, 0.10, 0.03), (0.685, 0.165, 0.165, 0.045), (0.74, 0.168, 0.167, 0.047), (0.85, 0.17, 0.168, 0.05),
    (0.91, 0.17, 0.168, 0.05), (0.99, 0.125, 0.125, 0.045),
]


def head_front_y(z):
    for (z0, _, y0, _), (z1, _, y1, _) in zip(HEAD_RINGS, HEAD_RINGS[1:]):
        if z0 <= z <= z1:
            return -(y0 + (y1 - y0) * (z - z0) / (z1 - z0))
    return -0.168


def add_head(part, colour, eyes=True):
    stations = [(z, rect(0, 0, hx, hy, cut)) for z, hx, hy, cut in HEAD_RINGS]
    loft(part, "z", stations, weights_head, colour)
    if eyes:
        for x in (-0.065, 0.065):
            y = head_front_y(0.79) - 0.002
            quad(part, [V((x - 0.022, y, 0.765)), V((x + 0.022, y, 0.765)), V((x + 0.022, y, 0.82)), V((x - 0.022, y, 0.82))], BLACK, weights_head)


def hair_rule(skin, hair, back_from, side_from, top_from, scarf=None, scarf_from=0.85):
    def rule(c, n):
        if scarf and c.z > scarf_from:
            return scarf
        if c.z > top_from:
            return hair
        if n.y > 0.4 and c.z > back_from:
            return hair
        if abs(n.x) > 0.4 and c.z > side_from:
            return hair
        return skin
    return rule


def head_orchardist(armature):
    part = Part()
    add_head(part, hair_rule(SKIN_TAN, HAIR_DARK, 0.74, 0.85, 0.91))
    # Straw hat: a wide brim and a crown with a ribbon.
    loft(part, "z", [(0.915, rect(0, 0, 0.27, 0.27, 0.09)), (0.93, rect(0, 0, 0.27, 0.27, 0.09))], weights_head, STRAW)
    loft(part, "z", [(0.925, rect(0, 0, 0.186, 0.186, 0.055)), (0.96, rect(0, 0, 0.19, 0.19, 0.056)), (1.04, rect(0, 0, 0.165, 0.165, 0.05))],
         weights_head, STRAW, seg_colours={0: HAT_BAND})
    return finish(part, "Head_Orchardist", armature)


def head_herbalist(armature):
    part = Part()
    add_head(part, hair_rule(SKIN_PEACH, HAIR_BROWN, 0.70, 0.78, 0.97, scarf=SCARF_RED, scarf_from=0.85))
    # The knot of the scarf at the back.
    loft(part, "z", [(0.86, rect(0, 0.185, 0.045, 0.035, 0.012)), (0.95, rect(0, 0.19, 0.055, 0.04, 0.015))], weights_head, SCARF_RED)
    return finish(part, "Head_Herbalist", armature)


# ---- the torso (body + trousers) ----

def add_neck(part, skin):
    loft(part, "z", [(0.58, rect(0, 0, 0.05, 0.05, 0.015)), (0.64, rect(0, 0, 0.05, 0.05, 0.015))], weights_neck, skin)


def add_arms(part, sleeve, skin, sleeve_end=0.385, sleeve_cell_end=None):
    """Both arms: a sleeve to sleeve_end (|x|), a bare forearm to the wrist when it ends early, then the hand."""
    for sign in (1, -1):
        def st(x, h):
            return (x * sign, rect(0, 0.545, h, h, 0.016))
        loft(part, "x", [st(0.13, 0.052), st(0.17, 0.056), st(0.28, 0.049), st(sleeve_end, 0.046)], weights_arm, sleeve)
        if sleeve_end < 0.385:
            loft(part, "x", [st(sleeve_end, 0.043), st(0.385, 0.041)], weights_arm, skin)
        loft(part, "x", [st(0.385, 0.041), st(0.43, 0.046), st(0.47, 0.036)], weights_arm, skin)


def add_legs(part, cell, z_end=0.04):
    for sign in (1, -1):
        def st(z, h):
            return (z, rect(0.07 * sign, 0, h, h - 0.004, 0.02))
        loft(part, "z", [st(0.32, 0.066), st(0.22, 0.066), st(0.15, 0.06), st(z_end, 0.054)], weights_leg, cell)


def torso_orchardist(armature):
    part = Part()
    add_neck(part, SKIN_TAN)
    loft(part, "z", [(0.30, rect(0, 0, 0.145, 0.105, 0.03)), (0.45, rect(0, 0, 0.15, 0.108, 0.03)), (0.56, rect(0, 0, 0.157, 0.11, 0.032)),
                     (0.60, rect(0, 0, 0.12, 0.085, 0.035))], weights_torso, SHIRT_CREAM)
    # Apron: a bib over the chest and a skirt panel over the belly, with a strap.
    loft(part, "y", [(-0.108, rect(0, 0.42, 0.095, 0.1)), (-0.121, rect(0, 0.42, 0.095, 0.1))], weights_torso, APRON_GREEN)
    loft(part, "y", [(-0.108, rect(0, 0.545, 0.1, 0.012)), (-0.123, rect(0, 0.545, 0.1, 0.012))], weights_torso, STRAP)
    add_arms(part, SHIRT_CREAM, SKIN_TAN)
    add_legs(part, TROUSERS_BROWN)
    return finish(part, "Torso_Orchardist", armature)


def torso_herbalist(armature):
    part = Part()
    add_neck(part, SKIN_PEACH)
    loft(part, "z", [(0.40, rect(0, 0, 0.148, 0.106, 0.03)), (0.56, rect(0, 0, 0.157, 0.11, 0.032)), (0.60, rect(0, 0, 0.12, 0.085, 0.035))],
         weights_torso, DRESS_ORCHID)
    # Belt and a flared skirt with a cream hem; it hangs from the hips and does not follow the legs.
    loft(part, "z", [(0.385, rect(0, 0, 0.152, 0.11, 0.03)), (0.42, rect(0, 0, 0.152, 0.11, 0.03))], weights_torso, TRIM_CREAM)
    loft(part, "z", [(0.40, rect(0, 0, 0.15, 0.108, 0.03)), (0.28, rect(0, 0, 0.185, 0.14, 0.045)), (0.215, rect(0, 0, 0.21, 0.163, 0.05)),
                     (0.17, rect(0, 0, 0.216, 0.168, 0.052))], weights_skirt, DRESS_ORCHID, seg_colours={2: TRIM_CREAM})
    add_arms(part, DRESS_ORCHID, SKIN_PEACH, sleeve_end=0.30)
    add_legs(part, TIGHTS_WHITE)
    return finish(part, "Torso_Herbalist", armature)


# ---- shoes ----

def add_boot(part, sign, sole, shaft_top, shaft_cell, toe_cell):
    cx = 0.07 * sign
    # The foot: heel to toe along Y, z from the ground to the instep.
    h = 0.04 if shaft_top else 0.025
    loft(part, "y", [(0.078, rect(cx, h, 0.064, h, 0.015)), (-0.04, rect(cx, h, 0.069, h, 0.015)), (-0.145, rect(cx, h * 0.9, 0.056, h * 0.9, 0.015))],
         weights_leg_point, toe_cell, seg_colours=None)
    if shaft_top:
        loft(part, "z", [(0.07, rect(cx, 0, 0.069, 0.068, 0.02)), (shaft_top, rect(cx, 0, 0.066, 0.064, 0.02))], weights_leg_point, shaft_cell)


def weights_leg_point(p):
    return weights_leg(p)


def shoes_boots(armature):
    part = Part()
    for sign in (1, -1):
        add_boot(part, sign, 0.0, 0.135, BOOT, BOOT)
    return finish(part, "Shoes_Boots", armature)


def shoes_low(armature):
    part = Part()
    for sign in (1, -1):
        add_boot(part, sign, 0.0, 0.0, SHOE_TAN, SHOE_TAN)
    return finish(part, "Shoes_Low", armature)


# ---- the player's plain clothes and the Traveller set (docs/EQUIPMENT_PLAN.md, phase 5) ----

COAT_LEATHER = Col((6, 1), "Top")
COAT_TRIM = Col((0, 3), "TopTrim")
CAP_WOOL = Col((2, 4), "Hat")
CAP_BAND = Col((0, 3), "HatBand")
BOOT_DARK = Col((0, 1), "Shoes")
BOOT_CUFF = Col((0, 3), "Shoes")


def head_plain(armature):
    """The player's head without anything on it: hair only."""
    part = Part()
    add_head(part, hair_rule(SKIN_TAN, HAIR_BROWN, 0.72, 0.82, 0.95))
    return finish(part, "Head_Plain", armature)


def head_traveller(armature):
    """A knitted cap pulled over the hair, with a turned-up band."""
    part = Part()
    add_head(part, hair_rule(SKIN_TAN, HAIR_BROWN, 0.72, 0.82, 0.95))
    loft(part, "z", [(0.875, rect(0, 0, 0.184, 0.182, 0.056)), (0.935, rect(0, 0, 0.186, 0.184, 0.057))], weights_head, CAP_BAND)
    loft(part, "z", [(0.93, rect(0, 0, 0.18, 0.178, 0.054)), (0.99, rect(0, 0, 0.176, 0.174, 0.052)), (1.05, rect(0, 0, 0.12, 0.118, 0.04))],
         weights_head, CAP_WOOL)
    return finish(part, "Head_Traveller", armature)


def torso_plain(armature):
    """The player's own clothes: a cream shirt and brown trousers."""
    part = Part()
    add_neck(part, SKIN_TAN)
    loft(part, "z", [(0.30, rect(0, 0, 0.145, 0.105, 0.03)), (0.45, rect(0, 0, 0.15, 0.108, 0.03)), (0.56, rect(0, 0, 0.157, 0.11, 0.032)),
                     (0.60, rect(0, 0, 0.12, 0.085, 0.035))], weights_torso, SHIRT_CREAM)
    add_arms(part, SHIRT_CREAM, SKIN_TAN, sleeve_end=0.30)
    add_legs(part, TROUSERS_BROWN)
    return finish(part, "Torso_Plain", armature)


def torso_traveller(armature):
    """A long leather coat with a belt and a fur collar over brown trousers."""
    part = Part()
    add_neck(part, SKIN_TAN)
    loft(part, "z", [(0.36, rect(0, 0, 0.158, 0.116, 0.032)), (0.50, rect(0, 0, 0.164, 0.118, 0.034)), (0.57, rect(0, 0, 0.166, 0.118, 0.034)),
                     (0.60, rect(0, 0, 0.125, 0.09, 0.036))], weights_torso, COAT_LEATHER)
    # The skirt of the coat hangs from the belt and does not follow the legs.
    loft(part, "z", [(0.37, rect(0, 0, 0.16, 0.117, 0.032)), (0.27, rect(0, 0, 0.18, 0.136, 0.042)), (0.20, rect(0, 0, 0.19, 0.145, 0.046))],
         weights_skirt, COAT_LEATHER, seg_colours={1: COAT_TRIM})
    loft(part, "z", [(0.385, rect(0, 0, 0.168, 0.122, 0.034)), (0.425, rect(0, 0, 0.168, 0.122, 0.034))], weights_torso, STRAP)
    loft(part, "z", [(0.565, rect(0, 0, 0.098, 0.09, 0.03)), (0.64, rect(0, 0, 0.088, 0.082, 0.028))], weights_torso, COAT_TRIM)
    add_arms(part, COAT_LEATHER, SKIN_TAN, sleeve_end=0.355)
    add_legs(part, TROUSERS_BROWN)
    return finish(part, "Torso_Traveller", armature)


def shoes_traveller(armature):
    """Tall dark boots with a turned cuff."""
    part = Part()
    for sign in (1, -1):
        add_boot(part, sign, 0.0, 0.15, BOOT_DARK, BOOT_DARK)
        cx = 0.07 * sign
        loft(part, "z", [(0.135, rect(cx, 0, 0.076, 0.075, 0.022)), (0.16, rect(cx, 0, 0.076, 0.075, 0.022))], weights_leg_point, BOOT_CUFF)
    return finish(part, "Shoes_Traveller", armature)


# ---- preview ----

def pose_rotate(armature, bone, axis, degrees):
    pb = armature.pose.bones["mixamorig:" + bone]
    joint = pb.head.copy()
    rotation = Matrix.Translation(joint) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-joint)
    pb.matrix = rotation @ pb.matrix
    bpy.context.view_layer.update()


def reset_pose(armature):
    for pb in armature.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def idle_pose(armature):
    pose_rotate(armature, "LeftArm", "Y", 68)
    pose_rotate(armature, "RightArm", "Y", -68)
    pose_rotate(armature, "LeftForeArm", "Y", 12)
    pose_rotate(armature, "RightForeArm", "Y", -12)


def walk_pose(armature):
    idle_pose(armature)
    pose_rotate(armature, "LeftUpLeg", "X", 28)
    pose_rotate(armature, "LeftLeg", "X", -18)
    pose_rotate(armature, "RightUpLeg", "X", -24)
    pose_rotate(armature, "RightArm", "X", 20)
    pose_rotate(armature, "LeftArm", "X", -20)


def render_outfits(armature, outfits):
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene = bpy.context.scene
    render = scene.render
    shading = scene.display.shading
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 1.5
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    render.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    render.resolution_x = render.resolution_y = 560
    render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.55, 0.75, 0.55)
    target = V((0, 0, 0.6))
    views = {"front": V((0, -1, 0.15)), "tq": V((0.75, -0.65, 0.3)), "side": V((1, 0, 0.1))}
    poses = {"tpose": lambda a: None, "idle": idle_pose, "walk": walk_pose}
    for outfit, parts in outfits.items():
        for obj in bpy.data.objects:
            if obj.type == "MESH":
                obj.hide_render = obj.name not in parts
        for pose_name, apply_pose in poses.items():
            reset_pose(armature)
            apply_pose(armature)
            for view_name, direction in views.items():
                if pose_name != "tpose" and view_name == "side" and False:
                    continue
                cam.location = target + direction.normalized() * 3.0
                cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
                render.filepath = f"{PREVIEW_DIR}/char_{outfit}_{pose_name}_{view_name}.png"
                bpy.ops.render.render(write_still=True)
    reset_pose(armature)
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.hide_render = False


ICON_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Equipment"
# name -> (part, look-at point, ortho scale): an icon shows the piece on the body it belongs to, framed on the piece.
ICONS = {
    "TravellerCap": ("Head_Traveller", V((0, 0, 0.97)), 0.50),
    "TravellerCoat": ("Torso_Traveller", V((0, 0, 0.60)), 0.95),
    "TravellerBoots": ("Shoes_Traveller", V((0, 0, 0.11)), 0.42),
}


def render_icons(armature):
    """512x512 transparent item icons of the Traveller pieces, the same Workbench look as the tool icons."""
    os.makedirs(ICON_DIR, exist_ok=True)
    scene = bpy.context.scene
    render = scene.render
    shading = scene.display.shading
    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    render.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    shading.object_outline_color = (0.10, 0.06, 0.04)
    render.resolution_x = render.resolution_y = 512
    render.film_transparent = True
    render.image_settings.file_format = "PNG"
    render.image_settings.color_mode = "RGBA"
    reset_pose(armature)
    idle_pose(armature)
    for name, (part, target, scale) in ICONS.items():
        for obj in bpy.data.objects:
            if obj.type == "MESH":
                obj.hide_render = obj.name != part
        cam_data.ortho_scale = scale
        cam.location = target + V((0.55, -0.8, 0.25)).normalized() * 3.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        render.filepath = f"{ICON_DIR}/{name}.png"
        bpy.ops.render.render(write_still=True)
        print("rendered", render.filepath)
    reset_pose(armature)
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.hide_render = False
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def export(armature, meshes):
    os.makedirs(os.path.dirname(EXPORT_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(filepath=EXPORT_PATH, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, mesh_smooth_type="FACE",
                             add_leaf_bones=False, object_types={"ARMATURE", "MESH"}, use_armature_deform_only=False,
                             bake_anim=False)
    print("exported", EXPORT_PATH)


def main():
    clear_scene()
    armature = build_armature()
    meshes = [
        head_orchardist(armature), torso_orchardist(armature), shoes_boots(armature),
        head_herbalist(armature), torso_herbalist(armature), shoes_low(armature),
        head_plain(armature), torso_plain(armature), head_traveller(armature), torso_traveller(armature), shoes_traveller(armature),
    ]
    outfits = {
        "Orchardist": {"Head_Orchardist", "Torso_Orchardist", "Shoes_Boots"},
        "Herbalist": {"Head_Herbalist", "Torso_Herbalist", "Shoes_Low"},
        "Plain": {"Head_Plain", "Torso_Plain", "Shoes_Low"},
        "Traveller": {"Head_Traveller", "Torso_Traveller", "Shoes_Traveller"},
    }
    export(armature, meshes)
    render_outfits(armature, outfits)
    render_icons(armature)


if __name__ == "__main__":
    main()
