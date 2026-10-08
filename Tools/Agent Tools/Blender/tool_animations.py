"""Work animations of the hand tools (docs/EQUIPMENT_PLAN.md, phase 3): axe chop, pickaxe strike, sickle reap, spear thrust.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tool_animations.py
Env: TOOL_ANIM_ONLY=axe,pickaxe (default: all), TOOL_ANIM_PREVIEW_DIR (contact sheets), TOOL_ANIM_EXPORT=0 to only preview.

The skeleton of the character kit (character_kit.py) is animated by the TOOL: every clip is a few keys of where the tool is (the grip, the way
the shaft points and the way the edge faces) plus the posture of the body. The hands are put on the shaft by IK (the right hand at the grip,
the left lower on the shaft for the two handed tools) and the feet stay planted by IK, then every frame is baked to plain bone keys and the
armature alone is exported, one FBX per clip, for Unity to import as Humanoid with the kit's avatar.

The tool sits in the right hand exactly as GRIP says; ToolSocket.cs in Unity carries the same grip, so what is seen here is what the game shows.
Each cycle strikes at its middle (STRIKE), where the game shakes the target.
"""
import math
import os
import sys
from array import array

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import character_kit as kit  # noqa: E402
import stone_tools as tools  # noqa: E402
from animal_common import clear_scene  # noqa: E402

V = Vector
FPS = 30
STRIKE = 0.5
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Animation/Tools"
PREVIEW_DIR = os.environ.get("TOOL_ANIM_PREVIEW_DIR", "C:/Temp/tool_anim")

# The grip, in the frame of the hand bone (Y from the wrist to the fingers, Z the back of the hand): the shaft crosses the palm a little past
# the middle of the hand, the head of the tool on the thumb side, the edge facing the way the fingers point (the way a hammer is held).
HAND_GRIP_ALONG = 0.55   # share of the hand bone from the wrist
PALM_DEPTH = 0.012       # how far under the bone the shaft runs

# Each foot stands this much further out than at rest (the rest stance is 0.13 m between the ankles) and turns its toes out by TOE_OUT degrees.
STANCE_WIDTH = 0.045
TOE_OUT = 12.0


def P(name):
    return "mixamorig:" + name


# ---- poses of the tool ----

class Key:
    """One key of a clip. grip: where the right hand holds the tool; shaft: the way to the head; edge: the way the edge or point faces
    (made square to the shaft). twist: the trunk turned to the right (degrees), bend: bent forward, side: leant to the right,
    drop: how far the hips sink (metres), nod: the head down. left: where the left hand is when it is not on the shaft (None = on it).
    motion: how the body passes this key: flow (keeps moving through it), stop (comes to rest here: the top of a swing, a hold),
    hit (arrives fast and stops dead: the blow)."""

    def __init__(self, phase, grip, shaft, edge, twist=0.0, bend=0.0, side=0.0, drop=0.0, nod=0.0, left=None, motion="flow"):
        self.phase = phase
        self.grip = V(grip)
        self.shaft = V(shaft).normalized()
        self.edge = V(edge)
        self.twist, self.bend, self.side, self.drop, self.nod = twist, bend, side, drop, nod
        self.left = V(left) if left is not None else None
        self.motion = motion

    def values(self):
        result = list(self.grip) + list(self.shaft) + list(self.edge) + [self.twist, self.bend, self.side, self.drop, self.nod]
        return result + (list(self.left) if self.left is not None else [])

    @staticmethod
    def from_values(phase, v):
        return Key(phase, v[0:3], v[3:6], v[6:9], *v[9:14], left=v[14:17] if len(v) > 14 else None)


class Clip:
    def __init__(self, name, tool, length, keys, left_on_shaft=None, stance=0.0, elbow_r=(-0.5, 0.25, -0.35), elbow_l=(0.5, 0.25, -0.35),
                 prefix="Tool"):
        self.name = name
        self.tool = tool              # builder of stone_tools; None for work done with bare hands
        self.prefix = prefix          # the FBX is <prefix>@<name>.fbx: Tool for a tool, Hands for bare hands
        self.length = length          # seconds of one cycle
        # The first and the last key are the same pose (a loop); the last one is put at the end of the cycle whatever its phase says,
        # so a clip can open and close with the same Key.
        first = keys[0]
        self.keys = keys[:-1] + [Key.from_values(1.0, keys[-1].values())]
        self.keys[-1].motion = keys[-1].motion
        assert keys[-1].values() == first.values(), f"{name}: the loop is not closed"
        assert all(a.phase < b.phase for a, b in zip(self.keys, self.keys[1:])), f"{name}: the keys are not in order"
        self.left_on_shaft = left_on_shaft  # where along the shaft the left hand holds (tool Z), None = one handed
        self.stance = stance          # the left foot steps forward by this much (metres)
        self.elbow_r = V(elbow_r)     # which way the elbows point, in the frame of the chest
        self.elbow_l = V(elbow_l)


HIT_SPEED = 1.8  # how fast a blow arrives, against an even move over its segment (below 3, so it does not overshoot)


def tangent(keys, i, side):
    """The rate of change of every value at key i, leaving it (side "out") or arriving (side "in"). The loop is closed: the first and the
    last key are the same pose, so their neighbours wrap around and the motion flows through the seam instead of pausing there."""
    key = keys[i]
    count = len(keys[0].values())
    if key.motion == "stop" or (key.motion == "hit" and side == "out"):
        return [0.0] * count
    last = len(keys) - 1
    if key.motion == "hit":
        before = keys[i - 1] if i > 0 else keys[last - 1]
        span = key.phase - before.phase if i > 0 else 1.0 - before.phase
        return [(b - a) / span * HIT_SPEED for a, b in zip(before.values(), key.values())]
    prev_key, prev_phase = (keys[i - 1], keys[i - 1].phase) if i > 0 else (keys[last - 1], keys[last - 1].phase - 1.0)
    next_key, next_phase = (keys[i + 1], keys[i + 1].phase) if i < last else (keys[1], keys[1].phase + 1.0)
    return [(b - a) / (next_phase - prev_phase) for a, b in zip(prev_key.values(), next_key.values())]


def sample(clip, phase):
    """The pose at `phase`: a cubic Hermite curve through the keys."""
    keys = clip.keys
    for i in range(len(keys) - 1):
        a, b = keys[i], keys[i + 1]
        if phase <= b.phase:
            span = max(1e-4, b.phase - a.phase)
            s = (phase - a.phase) / span
            h00, h10, h01, h11 = 2 * s ** 3 - 3 * s ** 2 + 1, s ** 3 - 2 * s ** 2 + s, -2 * s ** 3 + 3 * s ** 2, s ** 3 - s ** 2
            m0, m1 = tangent(keys, i, "out"), tangent(keys, i + 1, "in")
            values = [h00 * p0 + h10 * span * t0 + h01 * p1 + h11 * span * t1
                      for p0, p1, t0, t1 in zip(a.values(), b.values(), m0, m1)]
            return Key.from_values(phase, values)
    return keys[-1]


def tool_matrix(key):
    """The world matrix of the tool: origin at the grip, +Z along the shaft, -Y the edge."""
    z = key.shaft.normalized()
    edge = (key.edge - z * key.edge.dot(z)).normalized()
    y = -edge
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = key.grip
    return m


def hand_in_tool(left, along, hand_length):
    """The matrix of a hand bone in the frame of the tool when the hand holds the shaft at `along` (tool Z). Both hands have the fingers
    towards the edge and the thumb towards the head; the palms face each other across the shaft."""
    if left:
        x, y, z = V((0, 0, 1)), V((0, -1, 0)), V((1, 0, 0))
    else:
        x, y, z = V((0, 0, -1)), V((0, -1, 0)), V((-1, 0, 0))
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = V((0, 0, along)) - y * (HAND_GRIP_ALONG * hand_length) + z * PALM_DEPTH
    return m


# ---- the clips ----
# The character faces -Y, its right hand is at -X; the shoulders are at z 0.71, the hips at 0.48, the head ends at 1.14 and the arms are
# short (0.22 from the shoulder to the wrist), so the tools are held close to the chest.

def axe_chop():
    # Two handed, the left hand just below the right on the haft. The arms are short, so the swing stays close to the chest: wound up
    # over the right shoulder with the trunk turned away, a diagonal blow into a trunk in front at the height of the belly with the whole
    # body turning into it, a tug to free the blade, and on through the ready pose into the next swing without a pause.
    ready = Key(0.00, (-0.1, -0.17, 0.6), (-0.2, -0.35, 1.0), (0, -1, 0), twist=8, bend=6, drop=0.01)
    return Clip("AxeChop", tools.build_axe, 1.1, [
        ready,
        Key(0.30, (-0.13, -0.05, 0.82), (-0.4, 0.6, 0.7), (0.2, -0.6, 0.7), twist=38, bend=-6, side=-6, nod=-4, motion="stop"),
        Key(0.40, (-0.14, -0.04, 0.83), (-0.42, 0.62, 0.68), (0.2, -0.6, 0.7), twist=40, bend=-7, side=-7, nod=-4, motion="stop"),
        Key(STRIKE, (0.0, -0.22, 0.56), (0.35, -0.9, 0.05), (1, 0.2, -0.5), twist=-20, bend=16, side=4, drop=0.03, nod=8, motion="hit"),
        Key(0.60, (0.0, -0.21, 0.55), (0.35, -0.9, 0.02), (1, 0.2, -0.5), twist=-22, bend=17, side=5, drop=0.032, nod=8, motion="stop"),
        Key(0.78, (-0.06, -0.2, 0.6), (0.1, -0.7, 0.7), (0.4, -0.9, 0), twist=-4, bend=10, drop=0.02, nod=4),
        ready,
    ], left_on_shaft=-0.08, stance=0.06, elbow_r=(-0.5, 0.2, -0.4), elbow_l=(0.5, 0.1, -0.5))


def pickaxe_strike():
    # Two handed. The arms cannot get above the head, so the pick is raised beside the right shoulder with its head high behind it, then
    # brought down in front onto a rock on the ground, the trunk bending into the blow and the knees giving.
    ready = Key(0.00, (-0.12, -0.16, 0.6), (-0.1, -0.3, 1.0), (0, -1, 0.3), twist=10, bend=8, drop=0.015)
    return Clip("PickaxeStrike", tools.build_pickaxe, 1.3, [
        ready,
        Key(0.32, (-0.16, 0.0, 0.84), (-0.25, 0.55, 0.8), (0, -0.8, 0.55), twist=30, bend=-8, side=-4, nod=-6, motion="stop"),
        Key(0.42, (-0.17, 0.01, 0.85), (-0.27, 0.58, 0.78), (0, -0.8, 0.55), twist=32, bend=-9, side=-5, nod=-6, motion="stop"),
        Key(STRIKE, (-0.06, -0.24, 0.45), (0, -0.8, -0.55), (0, -0.3, -1), twist=-5, bend=38, drop=0.07, nod=16, motion="hit"),
        Key(0.64, (-0.06, -0.24, 0.44), (0, -0.8, -0.58), (0, -0.3, -1), twist=-5, bend=40, drop=0.075, nod=16, motion="stop"),
        Key(0.82, (-0.1, -0.22, 0.55), (-0.05, -0.6, 0.6), (0, -0.8, -0.4), twist=4, bend=24, drop=0.045, nod=8),
        ready,
    ], left_on_shaft=-0.085, stance=0.05, elbow_r=(-0.6, 0.1, -0.5), elbow_l=(0.6, 0.1, -0.5))


def sickle_reap():
    # Leaning in a little over the crop, the body almost still: the arm does the work. The sickle arm opens wide out to the right, then
    # sweeps in to the middle (and a little towards the body), where the left hand holds the stalks, and opens out again, over and over.
    # The crescent opens towards the handle and its inner side is the edge, so it cuts by moving towards its handle end: the handle points
    # out to the right and forward, the crescent lies flat in front of the hand and its hollow leads the sweep.
    def flat(shaft, tip=-0.12):
        # Square to the shaft on the forward side: the crescent lies nearly flat in front of the hand.
        a, b = (shaft[1], -shaft[0]), (-shaft[1], shaft[0])
        side = a if a[1] < b[1] else b
        return (side[0], side[1], tip)

    def key(phase, grip, shaft, left, **body):
        return Key(phase, grip, shaft, flat(shaft), left=left, **body)

    bunch = (0.05, -0.28, 0.49)
    ready = key(0.00, (-0.14, -0.13, 0.55), (-0.5, -0.85, 0.12), (0.06, -0.26, 0.5), twist=1, bend=19, drop=0.03, nod=14)
    return Clip("SickleReap", tools.build_sickle, 1.0, [
        ready,
        key(0.30, (-0.25, -0.16, 0.5), (-0.72, -0.68, -0.04), bunch, twist=4, bend=20, drop=0.033, nod=14, motion="stop"),
        key(0.36, (-0.252, -0.162, 0.498), (-0.73, -0.67, -0.05), bunch, twist=4, bend=20, drop=0.033, nod=14, motion="stop"),
        key(STRIKE, (-0.03, -0.2, 0.48), (-0.55, -0.83, -0.05), bunch, twist=-4, bend=22, drop=0.035, nod=16, motion="hit"),
        key(0.62, (0.0, -0.18, 0.49), (-0.5, -0.86, -0.02), (0.06, -0.27, 0.52), twist=-5, bend=22, drop=0.035, nod=16, motion="stop"),
        key(0.82, (-0.08, -0.12, 0.55), (-0.45, -0.88, 0.15), (0.06, -0.26, 0.51), twist=-2, bend=20, drop=0.032, nod=15),
        ready,
    ], stance=0.08, elbow_r=(-0.6, 0.3, -0.2), elbow_l=(0.6, 0.0, -0.4))


def spear_thrust():
    # Held low at the right hip, point forward; drawn back, then driven forward and down with a lunge and pulled back.
    ready = Key(0.00, (-0.12, 0.0, 0.6), (0.1, -1, 0.12), (0, 0, 1), twist=24, bend=4, drop=0.02)
    return Clip("SpearThrust", tools.build_spear, 1.0, [
        ready,
        Key(0.32, (-0.15, 0.06, 0.62), (0.08, -1, 0.15), (0, 0, 1), twist=32, bend=-2, drop=0.03, motion="stop"),
        Key(STRIKE, (-0.06, -0.22, 0.56), (0.06, -1, -0.3), (0, 0, 1), twist=4, bend=20, drop=0.06, nod=10, motion="hit"),
        Key(0.62, (-0.06, -0.22, 0.56), (0.06, -1, -0.32), (0, 0, 1), twist=4, bend=21, drop=0.06, nod=10, motion="stop"),
        ready,
    ], left_on_shaft=0.13, stance=0.1, elbow_r=(-0.5, 0.4, -0.3), elbow_l=(0.4, 0.0, -0.6))


# ---- bare hands ----
# Without a tool the "tool" of a key is the right hand itself: the grip is the middle of the palm, the shaft the way the thumb points and the
# edge the way the fingers point. These loops stay down the whole time: the game blends into them from standing (GatherAnimationSet's
# fade) and back to standing when the work ends, so only the hands work while the loop plays.

def pickup():
    # Squatting over something lying on the ground, no tool: the right hand reaches down, picks a piece up, passes it to the left hand
    # held at the knees, and reaches down for the next one a little to the side.
    squat = dict(bend=54, drop=0.25, nod=22)
    ready = Key(0.00, (-0.08, -0.16, 0.24), (0.8, -0.6, 0.2), (0, -0.4, -1), twist=4, left=(0.06, -0.16, 0.24), **squat)
    return Clip("Pickup", None, 1.2, [
        ready,
        Key(0.36, (-0.1, -0.22, 0.08), (1, -0.2, 0), (0, -0.3, -1), twist=6, left=(0.06, -0.16, 0.24), motion="stop", **squat),
        Key(STRIKE, (-0.1, -0.22, 0.075), (1, -0.2, 0), (0, -0.3, -1), twist=6, left=(0.06, -0.16, 0.24), motion="stop", **squat),
        Key(0.72, (-0.01, -0.15, 0.24), (0.7, -0.5, 0.4), (0, -0.5, -0.7), twist=0, left=(0.05, -0.16, 0.25), motion="stop", **squat),
        Key(0.86, (-0.04, -0.2, 0.14), (0.9, -0.4, 0.1), (0, -0.4, -1), twist=-2, left=(0.06, -0.16, 0.24), **squat),
        ready,
    ], stance=0.05, elbow_r=(-0.5, 0.2, -0.4), elbow_l=(0.5, 0.1, -0.5), prefix="Hands")


def hands_harvest():
    # Crouched over a plant that a tool could help with, gathered by hand: both hands take hold of it low down and pull it up with a tug
    # of the arms and the back, without standing up, then reach down for the next.
    crouch = dict(drop=0.17, nod=22)
    ready = Key(0.00, (-0.05, -0.21, 0.22), (1, -0.2, 0), (0, -0.3, -1), bend=46, left=(0.03, -0.21, 0.22), **crouch)
    return Clip("Harvest", None, 1.1, [
        ready,
        Key(0.30, (-0.04, -0.24, 0.12), (1, -0.2, 0), (0, -0.3, -1), bend=50, left=(0.03, -0.24, 0.13), motion="stop", **crouch),
        Key(0.38, (-0.04, -0.24, 0.115), (1, -0.2, 0), (0, -0.3, -1), bend=50, left=(0.03, -0.24, 0.125), motion="stop", **crouch),
        Key(STRIKE, (-0.05, -0.17, 0.3), (1, -0.2, 0.1), (0, -0.2, -1), bend=40, left=(0.03, -0.17, 0.31), motion="hit", **crouch),
        Key(0.62, (-0.06, -0.16, 0.31), (1, -0.2, 0.1), (0, -0.2, -1), bend=39, left=(0.03, -0.16, 0.32), motion="stop", **crouch),
        Key(0.82, (-0.05, -0.2, 0.24), (1, -0.2, 0), (0, -0.3, -1), bend=45, left=(0.03, -0.2, 0.24), **crouch),
        ready,
    ], stance=0.06, elbow_r=(-0.6, 0.1, -0.4), elbow_l=(0.6, 0.1, -0.4), prefix="Hands")


CLIPS = {"axe": axe_chop, "pickaxe": pickaxe_strike, "sickle": sickle_reap, "spear": spear_thrust, "pickup": pickup, "hands": hands_harvest}


# ---- the rig ----

def empty(name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def rotate_about_joint(pb, rotation):
    joint = pb.head.copy()
    pb.matrix = Matrix.Translation(joint) @ rotation @ Matrix.Translation(-joint) @ pb.matrix
    bpy.context.view_layer.update()


class Rig:
    def __init__(self, armature, clip):
        self.armature = armature
        self.clip = clip
        bones = armature.pose.bones
        self.hand_length = armature.data.bones[P("RightHand")].length
        # A little short of the full length, so the elbows always bend and the IK knows which way.
        self.reach = (armature.data.bones[P("RightArm")].length + armature.data.bones[P("RightForeArm")].length) * 0.95
        self.rest_chest = (armature.matrix_world @ armature.data.bones[P("Spine2")].matrix_local).to_3x3()
        self.targets = {}
        for side in ("Right", "Left"):
            target, pole = empty(f"{side}HandTarget"), empty(f"{side}ElbowPole")
            ik = bones[P(side + "ForeArm")].constraints.new("IK")
            ik.target, ik.pole_target, ik.chain_count = target, pole, 2
            copy = bones[P(side + "Hand")].constraints.new("COPY_ROTATION")
            copy.target = target
            self.targets[side] = (target, pole, ik)

            foot_target, knee_pole = empty(f"{side}FootTarget"), empty(f"{side}KneePole")
            foot = bones[P(side + "Foot")]
            leg_ik = bones[P(side + "Leg")].constraints.new("IK")
            leg_ik.target, leg_ik.pole_target, leg_ik.chain_count = foot_target, knee_pole, 2
            # The feet stand apart, wider than at rest (a worker's stance, not a soldier's), the left one a step forward, the toes turned
            # a little out; they stay planted. The knees point the way the toes do.
            outward = 1.0 if side == "Left" else -1.0
            ankle = armature.matrix_world @ foot.bone.head_local
            step = V((outward * STANCE_WIDTH, -clip.stance if side == "Left" else clip.stance * 0.3, 0))
            toe_out = Matrix.Rotation(math.radians(outward * TOE_OUT), 3, "Z")
            foot_target.matrix_world = Matrix.Translation(ankle + step) @ (toe_out @ (armature.matrix_world @ foot.bone.matrix_local).to_3x3()).to_4x4()
            foot_copy = foot.constraints.new("COPY_ROTATION")
            foot_copy.target = foot_target
            knee_pole.location = ankle + step + toe_out @ V((0, -0.5, 0.15))
            self.targets[side + "Leg"] = (foot_target, knee_pole, leg_ik)

    def chest(self):
        return self.armature.matrix_world @ self.armature.pose.bones[P("Spine2")].matrix

    def pose(self, key):
        """Puts the skeleton in the pose of `key`: the trunk by hand, the arms and the legs by IK. Returns the tool's world matrix."""
        arm = self.armature
        bones = arm.pose.bones
        for pb in bones:
            pb.matrix_basis = Matrix.Identity(4)
        # The arms are dead straight at rest, where the IK cannot tell which way to bend them: a slight bend to start from.
        for side in ("Right", "Left"):
            bones[P(side + "ForeArm")].matrix_basis = Matrix.Rotation(math.radians(30 if side == "Right" else -30), 4, "Z")
        bpy.context.view_layer.update()
        hips = bones[P("Hips")]
        hips.matrix = Matrix.Translation((0, 0, -key.drop)) @ hips.matrix
        bpy.context.view_layer.update()
        # The hips take a little of the turn, the three spine bones share the rest; the bend comes mostly from the lower back.
        shares = {"Hips": (0.25, 0.0, 0.2), "Spine": (0.25, 0.4, 0.3), "Spine1": (0.25, 0.35, 0.3), "Spine2": (0.25, 0.25, 0.2)}
        for name, (twist_share, bend_share, side_share) in shares.items():
            rotation = (Matrix.Rotation(math.radians(-key.twist * twist_share), 4, "Z")
                        @ Matrix.Rotation(math.radians(key.bend * bend_share), 4, "X")
                        @ Matrix.Rotation(math.radians(-key.side * side_share), 4, "Y"))
            rotate_about_joint(bones[P(name)], rotation)
        # The head looks at the work: half the bend is taken back by the neck, then the nod.
        rotate_about_joint(bones[P("Neck")], Matrix.Rotation(math.radians(-key.bend * 0.5 + key.nod * 0.5), 4, "X"))
        rotate_about_joint(bones[P("Head")], Matrix.Rotation(math.radians(key.nod * 0.5), 4, "X"))

        tool = self.reachable(tool_matrix(key))
        right_target, right_pole, _ = self.targets["Right"]
        left_target, left_pole, _ = self.targets["Left"]
        right_target.matrix_world = tool @ hand_in_tool(False, 0.0, self.hand_length)
        if self.clip.left_on_shaft is not None:
            left_target.matrix_world = tool @ hand_in_tool(True, self.clip.left_on_shaft, self.hand_length)
        else:
            # A free left hand: a loose fist (the back of the hand up, the fingers forward).
            free = Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1))).transposed().to_4x4()  # X, Y, Z of the hand as columns
            wrist = key.left - V((0, -1, 0)) * (HAND_GRIP_ALONG * self.hand_length)
            free.translation = wrist + self.pull(wrist, "Left")
            left_target.matrix_world = free
        # The elbows point the way the clip says in the frame of the chest, so they turn with the trunk.
        turn = self.chest().to_3x3() @ self.rest_chest.inverted()
        right_pole.location = self.shoulder("Right") + turn @ (self.clip.elbow_r * 0.5)
        left_pole.location = self.shoulder("Left") + turn @ (self.clip.elbow_l * 0.5)
        bpy.context.view_layer.update()
        return tool

    def shoulder(self, side):
        return self.armature.matrix_world @ self.armature.pose.bones[P(side + "Arm")].head

    def pull(self, wrist, side):
        """How far a wrist target has to move to come within the reach of its arm (zero when it is within it)."""
        to_wrist = wrist - self.shoulder(side)
        if to_wrist.length <= self.reach:
            return V()
        return to_wrist.normalized() * (self.reach - to_wrist.length)

    def reachable(self, tool):
        """The tool moved (not turned) as little as needed for the hands to reach it: the keys say what is meant, the arms decide."""
        for _ in range(8):
            shift = self.pull((tool @ hand_in_tool(False, 0.0, self.hand_length)).translation, "Right")
            if self.clip.left_on_shaft is not None:
                shift += self.pull((tool @ hand_in_tool(True, self.clip.left_on_shaft, self.hand_length)).translation, "Left")
            if shift.length < 1e-4:
                break
            tool.translation += shift
        return tool

    def held_tool(self):
        """Where the tool really is: in the right hand as the grip says, wherever the IK got the hand."""
        hand = self.armature.matrix_world @ self.armature.pose.bones[P("RightHand")].matrix
        return hand @ hand_in_tool(False, 0.0, self.hand_length).inverted()

    def fit_pole_angles(self, key):
        """Finds the pole angle of every IK chain that bends the joint towards its pole (it depends on the bones' rolls)."""
        for side, elbow_bone in (("Right", "RightForeArm"), ("Left", "LeftForeArm"), ("RightLeg", "RightLeg"), ("LeftLeg", "LeftLeg")):
            _, pole, ik = self.targets[side]
            best = None
            for degrees in range(-180, 180, 15):
                ik.pole_angle = math.radians(degrees)
                self.pose(key)
                pb = self.armature.pose.bones[P(elbow_bone)]
                root, joint, end = pb.parent.head, pb.head, pb.tail
                line = (end - root).normalized()
                bend = (joint - root) - line * (joint - root).dot(line)
                want = (pole.location - root) - line * (pole.location - root).dot(line)
                score = bend.normalized().dot(want.normalized()) if bend.length > 1e-5 else -2
                if best is None or score > best[0]:
                    best = (score, degrees)
            ik.pole_angle = math.radians(best[1])
            print(f"  pole {side}: {best[1]} deg (alignment {best[0]:.2f})")

    def reach_report(self, key, label):
        bones = self.armature.pose.bones
        for side in ("Right", "Left"):
            target = self.targets[side][0]
            wrist = bones[P(side + "Hand")].head
            miss = (wrist - target.matrix_world.translation).length
            if miss > 0.01:
                print(f"  {label}: the {side.lower()} hand misses its target by {miss:.3f} m")


# ---- baking, export, preview ----

def bake(rig, clip):
    """Every frame of one cycle as plain bone keys (no constraints left), the last frame the same as the first."""
    arm = rig.armature
    frames = round(clip.length * FPS)
    recorded = []
    tool_frames = []
    for f in range(frames + 1):
        key = sample(clip, f / frames if f < frames else 0.0)
        rig.pose(key)
        tool_frames.append(rig.held_tool())
        if f in (0, round(frames * STRIKE)):
            rig.reach_report(key, f"frame {f}")
        recorded.append({pb.name: pb.matrix.copy() for pb in arm.pose.bones})
    for pb in arm.pose.bones:
        for constraint in list(pb.constraints):
            pb.constraints.remove(constraint)
    arm.animation_data_clear()
    previous = {}
    for f, matrices in enumerate(recorded):
        for pb in arm.pose.bones:
            bone = pb.bone
            if bone.parent:
                rest = bone.parent.matrix_local.inverted() @ bone.matrix_local
                basis = rest.inverted() @ matrices[bone.parent.name].inverted() @ matrices[pb.name]
            else:
                basis = bone.matrix_local.inverted() @ matrices[pb.name]
            location, rotation, _ = basis.decompose()
            if pb.name in previous:
                rotation.make_compatible(previous[pb.name])
            previous[pb.name] = rotation
            pb.location = location
            pb.rotation_quaternion = rotation
            pb.keyframe_insert("location", frame=f)
            pb.keyframe_insert("rotation_quaternion", frame=f)
    arm.animation_data.action.name = clip.name
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 0, frames
    scene.render.fps = FPS
    return tool_frames


def export(armature, clip):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    path = f"{EXPORT_DIR}/{clip.prefix}@{clip.name}.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, add_leaf_bones=False,
                             object_types={"ARMATURE"}, use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    print("exported", path)


def render_sheet(armature, tool_obj, tool_frames, clip, columns=8):
    """A contact sheet: `columns` frames of the cycle (the strike among them) from three quarters front and from the right."""
    scene = bpy.context.scene
    render = scene.render
    shading = scene.display.shading
    render.engine = "BLENDER_WORKBENCH"
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_object_outline = True
    render.resolution_x = render.resolution_y = 260
    render.film_transparent = False
    render.image_settings.file_format = "PNG"
    render.image_settings.color_mode = "RGB"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.55, 0.75, 0.55)
    cam_data = bpy.data.cameras.new("SheetCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 1.45
    cam = bpy.data.objects.new("SheetCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    frames = len(tool_frames) - 1
    picks = sorted(set([round(frames * i / columns) for i in range(columns)] + [round(frames * STRIKE)]))
    views = {"front": V((0.55, -1, 0.25)), "right": V((-1, -0.1, 0.12))}
    target = V((0, -0.12, 0.6))
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    tiles = []
    for view, direction in views.items():
        row = []
        cam.location = target + direction.normalized() * 3.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        for f in picks:
            scene.frame_set(f)
            if tool_obj is not None:
                tool_obj.matrix_world = tool_frames[f]
            render.filepath = f"{PREVIEW_DIR}/_tile.png"
            bpy.ops.render.render(write_still=True)
            image = bpy.data.images.load(render.filepath, check_existing=False)
            pixels = array("f", [0.0]) * (len(image.pixels))
            image.pixels.foreach_get(pixels)
            row.append(pixels)
            bpy.data.images.remove(image)
        tiles.append(row)
    size = render.resolution_x
    width, height = size * len(picks), size * len(views)
    sheet = bpy.data.images.new(f"sheet_{clip.name}", width, height)
    out = array("f", [0.0]) * (width * height * 4)
    for r, row in enumerate(tiles):
        top = len(views) - 1 - r
        for c, pixels in enumerate(row):
            for y in range(size):
                start = ((top * size + y) * width + c * size) * 4
                out[start:start + size * 4] = pixels[y * size * 4:(y + 1) * size * 4]
    sheet.pixels.foreach_set(out)
    sheet.filepath_raw = f"{PREVIEW_DIR}/{clip.name}.png"
    sheet.file_format = "PNG"
    sheet.save()
    print("sheet", sheet.filepath_raw, "frames", picks)
    bpy.data.objects.remove(cam, do_unlink=True)


def build(clip_name):
    clear_scene()
    clip = CLIPS[clip_name]()
    armature = kit.build_armature()
    meshes = [kit.head_plain(armature), kit.torso_plain(armature), kit.shoes_low(armature)]
    tool_obj = clip.tool() if clip.tool else None
    for pb in armature.pose.bones:
        pb.rotation_mode = "QUATERNION"
    rig = Rig(armature, clip)
    print(clip.name)
    rig.fit_pole_angles(clip.keys[0])
    tool_frames = bake(rig, clip)
    if os.environ.get("TOOL_ANIM_EXPORT", "1") != "0":
        export(armature, clip)
    render_sheet(armature, tool_obj, tool_frames, clip)
    return meshes


def main():
    only = [n.strip() for n in os.environ.get("TOOL_ANIM_ONLY", "").split(",") if n.strip()] or list(CLIPS)
    for name in only:
        build(name)


if __name__ == "__main__":
    main()
