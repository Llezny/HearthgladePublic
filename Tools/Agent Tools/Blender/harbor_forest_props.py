"""Dark timber props of the forest port Mosshollow: shingled timber houses, wooden market stalls, rail fence, wood pile and a
bracket lantern, drawn after the reference of a dusk pine forest (steep shingle roofs, plaster between dark red-brown beams,
glowing windows). Reuses the helpers of harbor_props.py; the colours come from a palette of its own (Colorsheet Forest.png) that keeps
the cell layout of the main palette, so the shared props (crate, barrel, quay, ...) only need the forest material in Unity.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python harbor_forest_props.py
Writes the palette (+ its emission map, which makes the windows and lamps glow), one FBX per prop into EXPORT_DIR and a contact sheet.

Same conventions as harbor_props.py: metres, Z up, origin at the bottom centre, front of a prop faces -Y.
"""
import math
import os
import random
import struct
import sys
import zlib

import bmesh
import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harbor_props as hp  # noqa: E402

PALETTE_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette"
PALETTE = f"{PALETTE_DIR}/Colorsheet Forest.png"
EMISSION = f"{PALETTE_DIR}/Colorsheet Forest Emission.png"
EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/HarborForest"
PREVIEW_PATH = os.environ.get("FOREST_PREVIEW", "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/b04cb7dc-a75a-4f5a-8f1c-9cd40b16323a/scratchpad/forest_props.png")

# Cells the forest props add to the layout of the main palette.
SHINGLE_A = (6, 1)
SHINGLE_B = (6, 2)
SHINGLE_MOSS = (6, 3)
TIMBER_RED = (6, 4)
IRON = (6, 5)
COPPER = (6, 6)
GLOW = (6, 7)
PLASTER_DIM = (7, 0)
PLASTER_SHADE = (7, 1)
LOG_END = (7, 2)
FERN = (7, 3)
APPLE_GREEN = (7, 4)
MUSHROOM_CAP = (7, 5)

(BLACK, WOOD_DARK, WOOD_MID, PLANK_TAN, THATCH, WATER_TEAL, WALL_WHITE, GREEN_L, BLUE_L, HAY, SKIN, BLUE_D, PLANK_BROWN, WALL_CREAM,
 PURPLE, PINK, STONE, GREEN, RED, ORANGE, YELLOW, YELLOW_L, STONE_D, ROOF_RUST, ROOF_GREY, RED_BRIGHT) = (
    hp.BLACK, hp.WOOD_DARK, hp.WOOD_MID, hp.PLANK_TAN, hp.THATCH, hp.WATER_TEAL, hp.WALL_WHITE, hp.GREEN_L, hp.BLUE_L, hp.HAY, hp.SKIN,
    hp.BLUE_D, hp.PLANK_BROWN, hp.WALL_CREAM, hp.PURPLE, hp.PINK, hp.STONE, hp.GREEN, hp.RED, hp.ORANGE, hp.YELLOW, hp.YELLOW_L,
    hp.STONE_D, hp.ROOF_RUST, hp.ROOF_GREY, hp.RED_BRIGHT)

# The forest palette: darker and browner than the main one, muted cloth, warm glow for the lit windows.
FOREST_COLOURS = {
    BLACK: "1c1815", WOOD_DARK: "3b2a22", WOOD_MID: "6a4630", PLANK_TAN: "8a6a48", THATCH: "6e6240", WATER_TEAL: "3f6a66",
    WALL_WHITE: "a39d8e", GREEN_L: "6b8a4a", BLUE_L: "5a7a86", HAY: "a68c4e", SKIN: "c9a07e", BLUE_D: "36505f",
    PLANK_BROWN: "56382a", WALL_CREAM: "b5ab92", PURPLE: "5e3f6a", PINK: "a8707a", STONE: "7d8280", GREEN: "3e5a35",
    RED: "8a3a30", ORANGE: "b8702f", YELLOW: "b89a38", YELLOW_L: "ffb040", STONE_D: "4a504e", ROOF_RUST: "5b4a2e",
    ROOF_GREY: "4a4a3c", RED_BRIGHT: "b0483a",
    SHINGLE_A: "4b4631", SHINGLE_B: "5e5738", SHINGLE_MOSS: "4d5a35", TIMBER_RED: "5e2f26", IRON: "2b2d30", COPPER: "a25f35",
    GLOW: "ffb347", PLASTER_DIM: "a29b8b", PLASTER_SHADE: "8a8474", LOG_END: "c49a64", FERN: "33502d", APPLE_GREEN: "8ca03f",
    MUSHROOM_CAP: "9c4a3a",
}
GLOWING = (YELLOW_L, GLOW)
# The lighting of the game is bright and hazy, so the colours above are drawn darker in the texture.
DARKEN = 0.72


def write_png(path, size, pixels):
    """Plain RGBA PNG writer (the colours are written as they are, no colour management in the way)."""
    raw = b"".join(b"\x00" + bytes(v for px in pixels[y * size:(y + 1) * size] for v in px) for y in range(size))

    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(png)


def rgb(hex_colour, factor=1.0):
    return tuple(min(255, round(int(hex_colour[i:i + 2], 16) * factor)) for i in (0, 2, 4)) + (255,)


def write_palettes():
    size, px = 32, 4
    base = [rgb("2a2622")] * (size * size)
    emission = [(0, 0, 0, 255)] * (size * size)
    for (row, col), colour in FOREST_COLOURS.items():
        for y in range(row * px, (row + 1) * px):
            for x in range(col * px, (col + 1) * px):
                glowing = (row, col) in GLOWING
                base[y * size + x] = rgb(colour, 1.0 if glowing else DARKEN)
                if glowing:
                    emission[y * size + x] = rgb(colour)
    write_png(PALETTE, size, base)
    write_png(EMISSION, size, emission)


# ---- placing things in a frame (position + yaw) -------------------------------------------------------------------------

class Frame:
    def __init__(self, x=0.0, y=0.0, yaw_deg=0.0):
        self.origin = Vector((x, y, 0.0))
        self.matrix = Euler((0.0, 0.0, math.radians(yaw_deg)), "XYZ").to_matrix()

    def p(self, point):
        return self.origin + self.matrix @ Vector(point)


WORLD = Frame()


def box(bm, f, center, size, cell, rot=(0.0, 0.0, 0.0)):
    matrix = f.matrix @ Euler(rot, "XYZ").to_matrix()
    c = f.p(center)
    sx, sy, sz = (s / 2 for s in size)
    verts = [bm.verts.new(c + matrix @ Vector((dx * sx, dy * sy, dz * sz))) for dz in (-1, 1) for dy in (-1, 1) for dx in (-1, 1)]
    out = []
    for idx in [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]:
        face = bm.faces.new([verts[i] for i in idx])
        hp.face_out(face, c)
        out.append((face, cell))
    return out


def tri(bm, f, a, b, c, cell, inside):
    return hp.add_tri(bm, f.p(a), f.p(b), f.p(c), cell, f.p(inside))


def disc(bm, center, radius, width, cell, sides=10):
    """A short cylinder lying along Y (a round window, the top of an arched door); both faces and the rim in one cell."""
    cx, cy, cz = center
    ring_a = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy - width / 2, cz + radius * math.sin(2 * math.pi * k / sides))) for k in range(sides)]
    ring_b = [bm.verts.new((cx + radius * math.cos(2 * math.pi * k / sides), cy + width / 2, cz + radius * math.sin(2 * math.pi * k / sides))) for k in range(sides)]
    out = []
    for k in range(sides):
        face = bm.faces.new([ring_a[k], ring_a[(k + 1) % sides], ring_b[(k + 1) % sides], ring_b[k]])
        hp.face_out(face, Vector(center))
        out.append((face, cell))
    for ring in (ring_a, ring_b):
        cap = bm.faces.new(ring)
        hp.face_out(cap, Vector(center))
        out.append((cap, cell))
    return out


def log(bm, center, radius, length, bark, end, sides=6, axis="x"):
    """A log lying along X (or along Y, its ends to the front), with lighter ends."""
    cx, cy, cz = center
    rings = []
    for t in (-length / 2, length / 2):
        ring = []
        for k in range(sides):
            a, b = radius * math.cos(2 * math.pi * k / sides), radius * math.sin(2 * math.pi * k / sides)
            ring.append(bm.verts.new((cx + t, cy + a, cz + b) if axis == "x" else (cx + a, cy + t, cz + b)))
        rings.append(ring)
    out = []
    for k in range(sides):
        face = bm.faces.new([rings[0][k], rings[1][k], rings[1][(k + 1) % sides], rings[0][(k + 1) % sides]])
        hp.face_out(face, Vector(center))
        out.append((face, bark))
    for ring in rings:
        cap = bm.faces.new(ring)
        hp.face_out(cap, Vector(center))
        out.append((cap, end))
    return out


# ---- roof, windows, doors, timber frame -------------------------------------------------------------------------------

def roof(bm, f, cx, cy, w, d, base_z, rise, ov, cells, gable_cell, courses=4, ov_x=None):
    """A steep shingled gable roof, ridge along the local X: courses of shingles in two pieces each, barge boards, crossed horns at
    the ridge ends, a ridge cap. (cx, cy) = centre, w = length along the ridge, d = depth across it."""
    ov_x = ov if ov_x is None else ov_x
    half_d = d / 2
    theta = math.atan2(rise, half_d)
    tan = math.tan(theta)
    run = half_d + ov
    length = math.hypot(run, rise + ov * tan)
    span = w + 2 * ov_x
    thick = 0.012
    out = []
    for side in (-1, 1):
        ang = -side * theta
        u = Vector((0.0, -side * math.cos(theta), math.sin(theta)))
        normal = Vector((0.0, side * math.sin(theta), math.cos(theta)))
        eave = Vector((cx, cy + side * run, base_z - ov * tan))
        for i in range(courses):
            centre = eave + u * (length * (i + 0.5) / courses) + normal * (thick / 2 + (i % 2) * 0.004)
            for k in range(2):
                x = cx + (k - 0.5) * span / 2
                cell = cells[(i * 2 + k + (1 if side > 0 else 0) + (i // 2)) % len(cells)]
                out += box(bm, f, (x, centre.y, centre.z), (span / 2 - 0.003, length / courses * 1.35, thick), cell, (ang, 0.0, 0.0))
        for end in (-1, 1):
            x = cx + end * (span / 2 - 0.004)
            centre = eave + u * (length / 2) + normal * (thick + 0.006)
            out += box(bm, f, (x, centre.y, centre.z), (0.014, length + 0.012, 0.024), TIMBER_RED, (ang, 0.0, 0.0))
    # Crossed horns over the ridge ends.
    for end in (-1, 1):
        x = cx + end * (span / 2 - 0.004)
        for side in (-1, 1):
            out += box(bm, f, (x, cy, base_z + rise + 0.004), (0.013, 0.12, 0.017), TIMBER_RED, (-side * theta, 0.0, 0.0))
    out += box(bm, f, (cx, cy, base_z + rise + 0.01), (span + 0.012, 0.03, 0.016), SHINGLE_A)
    # Gable walls under the overhang.
    inside = (cx, cy, base_z + rise * 0.3)
    for end in (-1, 1):
        x = cx + end * (w / 2)
        out += tri(bm, f, (x, cy - half_d, base_z), (x, cy + half_d, base_z), (x, cy, base_z + rise), gable_cell, inside)
    return out


def window(bm, wf, depth, x, z, w=0.05, h=0.062, shutters=False):
    """A glowing window in a wall whose outside normal is local -Y at distance `depth` from the frame origin."""
    out = box(bm, wf, (x, -depth - 0.004, z), (w + 0.024, 0.01, h + 0.024), TIMBER_RED)
    out += box(bm, wf, (x, -depth - 0.01, z), (w, 0.01, h), GLOW)
    out += box(bm, wf, (x, -depth - 0.0165, z), (w + 0.002, 0.006, 0.007), WOOD_DARK)
    out += box(bm, wf, (x, -depth - 0.0165, z), (0.007, 0.006, h + 0.002), WOOD_DARK)
    if shutters:
        for s in (-1, 1):
            out += box(bm, wf, (x + s * (w / 2 + 0.024), -depth - 0.008, z), (0.02, 0.008, h + 0.012), WOOD_MID)
    return out


def door(bm, wf, depth, x, base_z, w=0.09, h=0.16):
    """An arched plank door with a small lit pane and a stone step."""
    out = box(bm, wf, (x, -depth - 0.006, base_z + h / 2), (w, 0.012, h), WOOD_DARK)
    out += wall_disc(bm, wf, (x, -depth - 0.006, base_z + h), w / 2, 0.012, WOOD_DARK)
    for s in (-1, 1):
        out += box(bm, wf, (x + s * (w / 2 + 0.008), -depth - 0.008, base_z + (h + w / 2) / 2), (0.016, 0.016, h + w / 2), TIMBER_RED)
    out += box(bm, wf, (x, -depth - 0.014, base_z + h * 0.72), (0.03, 0.008, 0.032), GLOW)
    out += box(bm, wf, (x, -depth - 0.03, base_z + 0.009), (w + 0.05, 0.05, 0.018), STONE)
    return out


def wall_disc(bm, wf, center, radius, width, cell, sides=10):
    """A disc against a wall: built in the local frame of the wall, then moved to its place."""
    verts_before = set(bm.verts)
    out = disc(bm, center, radius, width, cell, sides)
    for v in set(bm.verts) - verts_before:
        v.co = wf.p(v.co)
    return out


def timber_wall(bm, wf, depth, length, z0, height, posts=(), braces=True):
    """Beams on a plaster wall: corner posts, extra posts at the given positions (local x), a plate on top, a rail in the middle and a
    diagonal brace in each half."""
    out = []
    zc = z0 + height / 2
    xs = [-length / 2 + 0.011, length / 2 - 0.011] + list(posts)
    for x in xs:
        out += box(bm, wf, (x, -depth - 0.003, zc), (0.022, 0.012, height), TIMBER_RED)
    out += box(bm, wf, (0, -depth - 0.003, z0 + height - 0.01), (length, 0.012, 0.02), TIMBER_RED)
    out += box(bm, wf, (0, -depth - 0.003, z0 + height * 0.5), (length, 0.012, 0.016), TIMBER_RED)
    if braces:
        for s in (-1, 1):
            dx, dz = length / 2 - 0.011, height * 0.5 - 0.02
            span = math.hypot(dx, dz) * 0.9
            out += box(bm, wf, (s * dx / 2, -depth - 0.003, z0 + height * 0.5 + dz / 2), (span, 0.011, 0.014), TIMBER_RED, (0.0, -s * math.atan2(dz, dx), 0.0))
    return out


WALL_FRONT, WALL_RIGHT, WALL_BACK, WALL_LEFT = (Frame(yaw_deg=a) for a in (0, 90, 180, 270))
PLINTH = 0.035


def plinth(bm, w, d):
    return box(bm, WORLD, (0, 0, PLINTH / 2), (w + 0.02, d + 0.02, PLINTH), STONE_D)


def chimney(bm, x, y, base, top):
    out = box(bm, WORLD, (x, y, (base + top) / 2), (0.066, 0.066, top - base), STONE_D)
    return out + box(bm, WORLD, (x, y, top), (0.085, 0.085, 0.014), IRON)


SHINGLES = [SHINGLE_A, SHINGLE_B, SHINGLE_A, SHINGLE_MOSS]


# ---- houses -------------------------------------------------------------------------------------------------------------

def build_cottage(name, w=0.54, d=0.64, wall_h=0.27, rise=0.3, door_x=0.0):
    """Gable to the street: plaster walls in a dark timber frame, a round window under the peak, an arched door."""
    bm = bmesh.new()
    out = plinth(bm, w, d)
    base = PLINTH + wall_h
    out += box(bm, WORLD, (0, 0, PLINTH + wall_h / 2), (w, d, wall_h), PLASTER_DIM)
    out += roof(bm, Frame(yaw_deg=90), 0, 0, d, w, base, rise, 0.06, SHINGLES, PLASTER_DIM)
    out += timber_wall(bm, WALL_FRONT, d / 2, w, PLINTH, wall_h, braces=False)
    for wf, depth, length in ((WALL_RIGHT, w / 2, d), (WALL_LEFT, w / 2, d)):
        out += timber_wall(bm, wf, depth, length, PLINTH, wall_h, posts=(0.0,))
        out += window(bm, wf, depth, -length * 0.22, PLINTH + wall_h * 0.62, shutters=True)
    out += door(bm, WALL_FRONT, d / 2, door_x, PLINTH)
    out += window(bm, WALL_FRONT, d / 2, door_x + 0.18, PLINTH + wall_h * 0.62)
    out += window(bm, WALL_FRONT, d / 2, door_x - 0.18, PLINTH + wall_h * 0.62)
    # Gable: post to the peak, collar beam, round window.
    out += box(bm, WORLD, (0, -d / 2 - 0.003, base + rise * 0.45), (0.016, 0.012, rise * 0.9), TIMBER_RED)
    out += box(bm, WORLD, (0, -d / 2 - 0.003, base + 0.012), (w, 0.012, 0.022), TIMBER_RED)
    out += wall_disc(bm, WORLD, (0, -d / 2 - 0.004, base + rise * 0.4), 0.044, 0.01, TIMBER_RED)
    out += wall_disc(bm, WORLD, (0, -d / 2 - 0.01, base + rise * 0.4), 0.032, 0.01, GLOW)
    out += chimney(bm, -w * 0.18, d * 0.2, base + rise * 0.55, base + rise + 0.08)
    return hp.finish(name, bm, out, budget=1500)


def build_tall_house(name, w=0.46, d=0.5, h1=0.24, h2=0.2, rise=0.3):
    """Two storeys, the upper one jettied over the street, glowing windows with shutters, a hoist beam in the gable."""
    bm = bmesh.new()
    out = plinth(bm, w, d)
    out += box(bm, WORLD, (0, 0, PLINTH + h1 / 2), (w, d, h1), PLASTER_DIM)
    uw, ud = w + 0.06, d + 0.06
    z1 = PLINTH + h1
    out += box(bm, WORLD, (0, 0, z1 + h2 / 2), (uw, ud, h2), PLASTER_SHADE)
    out += box(bm, WORLD, (0, 0, z1 + 0.012), (uw + 0.012, ud + 0.012, 0.024), TIMBER_RED)
    base = z1 + h2
    out += roof(bm, Frame(yaw_deg=90), 0, 0, ud, uw, base, rise, 0.06, SHINGLES, PLASTER_SHADE)
    out += timber_wall(bm, WALL_FRONT, d / 2, w, PLINTH, h1, braces=False)
    out += timber_wall(bm, WALL_FRONT, ud / 2, uw, z1 + 0.024, h2 - 0.024)
    for wf, length in ((WALL_RIGHT, d), (WALL_LEFT, d)):
        out += timber_wall(bm, wf, w / 2, length, PLINTH, h1, braces=False)
        out += window(bm, wf, w / 2, 0.0, PLINTH + h1 * 0.6)
        out += timber_wall(bm, wf, uw / 2, ud, z1 + 0.024, h2 - 0.024, posts=(0.0,), braces=False)
        out += window(bm, wf, uw / 2, -0.1, z1 + h2 * 0.55, shutters=True)
    out += door(bm, WALL_FRONT, d / 2, -0.07, PLINTH, w=0.085, h=0.15)
    out += window(bm, WALL_FRONT, d / 2, 0.12, PLINTH + h1 * 0.6, w=0.045, h=0.055)
    out += window(bm, WALL_FRONT, ud / 2, -0.1, z1 + h2 * 0.5, w=0.05, h=0.07, shutters=True)
    out += window(bm, WALL_FRONT, ud / 2, 0.1, z1 + h2 * 0.5, w=0.05, h=0.07, shutters=True)
    out += wall_disc(bm, WORLD, (0, -ud / 2 - 0.004, base + rise * 0.38), 0.04, 0.01, TIMBER_RED)
    out += wall_disc(bm, WORLD, (0, -ud / 2 - 0.01, base + rise * 0.38), 0.029, 0.01, GLOW)
    out += box(bm, WORLD, (0, -ud / 2 - 0.05, base + rise * 0.78), (0.016, 0.1, 0.016), TIMBER_RED)  # hoist beam
    out += chimney(bm, w * 0.2, ud * 0.22, base + rise * 0.5, base + rise + 0.08)
    return hp.finish(name, bm, out, budget=1500)


def build_longhouse(name, w=0.78, d=0.5, wall_h=0.26, rise=0.26):
    """Long roof along the street with a gabled porch over the door, like the cross-gable houses of the reference."""
    bm = bmesh.new()
    out = plinth(bm, w, d)
    base = PLINTH + wall_h
    out += box(bm, WORLD, (0, 0, PLINTH + wall_h / 2), (w, d, wall_h), PLASTER_DIM)
    out += roof(bm, WORLD, 0, 0, w, d, base, rise, 0.06, SHINGLES, PLASTER_DIM)
    out += timber_wall(bm, WALL_FRONT, d / 2, w, PLINTH, wall_h, posts=(-0.2, 0.2), braces=False)
    out += timber_wall(bm, WALL_BACK, d / 2, w, PLINTH, wall_h, braces=False)
    for wf in (WALL_RIGHT, WALL_LEFT):
        out += timber_wall(bm, wf, w / 2, d, PLINTH, wall_h, braces=True)
    out += window(bm, WALL_RIGHT, w / 2, 0.0, PLINTH + wall_h * 0.6)
    out += window(bm, WALL_LEFT, w / 2, 0.0, PLINTH + wall_h * 0.6)
    out += window(bm, WALL_FRONT, d / 2, -0.29, PLINTH + wall_h * 0.6, shutters=True)
    out += window(bm, WALL_FRONT, d / 2, 0.29, PLINTH + wall_h * 0.6, shutters=True)
    # Porch wing in front of the middle: its own gable, round window and the door.
    pw, pd = 0.3, 0.2
    py = -d / 2 - pd / 2 + 0.01
    out += box(bm, WORLD, (0, py, PLINTH + wall_h / 2), (pw, pd, wall_h), PLASTER_DIM)
    front = -d / 2 - pd + 0.01
    # The porch ridge runs back to just under the ridge of the main roof, so the two roofs meet in a valley.
    out += roof(bm, Frame(0, front / 2 - 0.01, 90), 0, 0, -front - 0.02, pw, base, 0.22, 0.05, SHINGLES, PLASTER_DIM)
    out += timber_wall(bm, WALL_FRONT, -front, pw, PLINTH, wall_h, braces=False)
    out += door(bm, WALL_FRONT, -front, 0.0, PLINTH)
    out += wall_disc(bm, WORLD, (0, front - 0.004, base + 0.22 * 0.36), 0.034, 0.01, TIMBER_RED)
    out += wall_disc(bm, WORLD, (0, front - 0.01, base + 0.22 * 0.36), 0.024, 0.01, GLOW)
    out += chimney(bm, w * 0.3, d * 0.14, base + rise * 0.5, base + rise + 0.08)
    return hp.finish(name, bm, out, budget=1500)


def build_hall(name="ForestHouseLarge"):
    """The trading house: a wide shingled hall, a cross-gable over the door with a hanging sign, a low annex on the east."""
    w, d, wall_h, rise = 1.0, 0.56, 0.3, 0.27
    bm = bmesh.new()
    out = box(bm, WORLD, (0, 0, 0.02), (w + 0.03, d + 0.03, 0.04), STONE_D)
    base = 0.04 + wall_h
    out += box(bm, WORLD, (0, 0, 0.04 + wall_h / 2), (w, d, wall_h), PLASTER_DIM)
    out += roof(bm, WORLD, 0, 0, w, d, base, rise, 0.07, SHINGLES, PLASTER_DIM, courses=5)
    out += timber_wall(bm, WALL_FRONT, d / 2, w, 0.04, wall_h, posts=(-0.33, 0.33), braces=False)
    out += timber_wall(bm, WALL_BACK, d / 2, w, 0.04, wall_h, braces=False)
    out += window(bm, WALL_FRONT, d / 2, -0.4, 0.04 + wall_h * 0.6, shutters=True)
    out += window(bm, WALL_FRONT, d / 2, 0.4, 0.04 + wall_h * 0.6, shutters=True)
    out += window(bm, WALL_FRONT, d / 2, 0.2, 0.04 + wall_h * 0.6, w=0.045)
    out += window(bm, WALL_LEFT, w / 2, 0.0, 0.04 + wall_h * 0.6)
    # Cross-gable over the door, running back to the ridge of the hall.
    pw, pd = 0.38, 0.2
    py = -d / 2 - pd / 2 + 0.01
    out += box(bm, WORLD, (-0.12, py, 0.04 + wall_h / 2), (pw, pd, wall_h), PLASTER_DIM)
    front = -d / 2 - pd + 0.01
    porch = Frame(-0.12, 0.0, 0)
    out += roof(bm, Frame(-0.12, front / 2 - 0.01, 90), 0, 0, -front - 0.02, pw, base, 0.24, 0.05, SHINGLES, PLASTER_DIM)
    out += timber_wall(bm, porch, -front, pw, 0.04, wall_h, braces=False)
    out += door(bm, porch, -front, 0.0, 0.04, w=0.11, h=0.18)
    out += wall_disc(bm, WORLD, (-0.12, front - 0.004, base + 0.25 * 0.36), 0.036, 0.01, TIMBER_RED)
    out += wall_disc(bm, WORLD, (-0.12, front - 0.01, base + 0.25 * 0.36), 0.026, 0.01, GLOW)
    # Sign board on a bracket.
    out += box(bm, WORLD, (-0.32, front - 0.05, 0.04 + 0.26), (0.016, 0.1, 0.016), TIMBER_RED)
    out += box(bm, WORLD, (-0.34, front - 0.1, 0.04 + 0.205), (0.1, 0.012, 0.075), PLANK_TAN)
    out += box(bm, WORLD, (-0.34, front - 0.108, 0.04 + 0.205), (0.07, 0.008, 0.045), COPPER)
    # Annex.
    aw, ad, ah = 0.36, 0.46, 0.2
    ax = w / 2 + aw / 2 - 0.02
    out += box(bm, WORLD, (ax, 0.04, 0.04 + ah / 2), (aw, ad, ah), PLASTER_SHADE)
    out += roof(bm, Frame(ax, 0.04, 90), 0, 0, ad, aw, 0.04 + ah, 0.17, 0.045, SHINGLES, PLASTER_SHADE)
    out += door(bm, Frame(ax, 0.0, 0), ad / 2 - 0.04, 0.0, 0.04, w=0.07, h=0.12)
    out += chimney(bm, -0.34, 0.1, base + rise * 0.5, base + rise + 0.09)
    return hp.finish(name, bm, out, budget=2400)


# ---- stalls -------------------------------------------------------------------------------------------------------------

def build_stall(name, cloth_a, cloth_b, kind):
    """A market stall of dark timber under a small shingled roof, a cloth valance in the colours of the trade and a lamp."""
    bm = bmesh.new()
    w, d = 0.6, 0.36
    post_h = 0.44
    out = []
    for sx in (-1, 1):
        for sy, h in ((-1, post_h), (1, post_h)):
            out += box(bm, WORLD, (sx * (w / 2 - 0.02), sy * (d / 2 - 0.02), h / 2), (0.028, 0.028, h), TIMBER_RED)
    out += box(bm, WORLD, (0, d / 2 - 0.02, 0.22), (w - 0.05, 0.014, 0.4), WOOD_MID)  # back wall
    for z in (0.1, 0.26, 0.41):
        out += box(bm, WORLD, (0, d / 2 - 0.028, z), (w - 0.03, 0.012, 0.02), TIMBER_RED)
    out += box(bm, WORLD, (0, -0.07, 0.06), (w - 0.07, 0.13, 0.12), PLANK_BROWN)  # counter
    out += box(bm, WORLD, (0, -0.07, 0.13), (w - 0.02, 0.15, 0.02), PLANK_TAN)
    out += box(bm, WORLD, (0, -d / 2 + 0.02, post_h - 0.012), (w, 0.022, 0.026), TIMBER_RED)  # front beam
    stripes = 6
    for i in range(stripes):
        out += box(bm, WORLD, (-w / 2 + 0.04 + (w - 0.08) * (i + 0.5) / stripes, -d / 2 + 0.006, post_h - 0.055), ((w - 0.08) / stripes, 0.008, 0.052), cloth_a if i % 2 == 0 else cloth_b)
    out += roof(bm, WORLD, 0, 0, w, d, post_h, 0.09, 0.04, SHINGLES, SHINGLE_A, courses=3)
    # Lamp hanging by the right post.
    out += box(bm, WORLD, (w / 2 - 0.045, -d / 2 - 0.012, post_h - 0.045), (0.004, 0.004, 0.04), IRON)
    out += box(bm, WORLD, (w / 2 - 0.045, -d / 2 - 0.012, post_h - 0.085), (0.034, 0.034, 0.04), GLOW)
    out += box(bm, WORLD, (w / 2 - 0.045, -d / 2 - 0.012, post_h - 0.062), (0.042, 0.042, 0.01), IRON)
    rng = random.Random(3)
    if kind == "fruit":
        for k, x in enumerate((-0.19, 0.0, 0.19)):
            out += box(bm, WORLD, (x, -0.07, 0.165), (0.15, 0.11, 0.04), WOOD_MID)  # a crate
            for j in range(4):
                cell = (RED_BRIGHT, APPLE_GREEN, RED)[(k + j) % 3]
                out += box(bm, WORLD, (x + (j % 2 - 0.5) * 0.06, -0.07 + (j // 2 - 0.5) * 0.045, 0.2), (0.04, 0.04, 0.04), cell, (0, 0, rng.uniform(0, 1)))
    elif kind == "herbs":
        for k, x in enumerate((-0.19, 0.0, 0.19)):
            out += box(bm, WORLD, (x, -0.07, 0.165), (0.14, 0.1, 0.04), WOOD_DARK)  # a basket
            for j in range(3):
                out += box(bm, WORLD, (x + (j - 1) * 0.04, -0.07, 0.205), (0.035, 0.035, 0.04), (MUSHROOM_CAP, GREEN_L, HAY)[(k + j) % 3], (0, 0, rng.uniform(0, 1)))
        for i, x in enumerate((-0.2, -0.1, 0.0, 0.1, 0.2)):  # bundles hanging from the front beam
            out += box(bm, WORLD, (x, -d / 2 + 0.034, post_h - 0.095), (0.03, 0.03, 0.065), (FERN, GREEN, HAY)[i % 3], (0, rng.uniform(-0.1, 0.1), 0))
    else:  # timber: stacked logs on the counter, a chopping block at the side
        for x in (-0.19, 0.0, 0.19):
            for dx, dz in ((-0.024, 0.0), (0.024, 0.0), (0.0, 0.04)):
                out += log(bm, (x + dx, -0.07, 0.15 + 0.02 + dz), 0.022, 0.13, PLANK_BROWN, LOG_END, axis="y")
        out += hp.add_cyl(bm, (w / 2 + 0.06, -0.1, 0.03), 0.045, 0.06, LOG_END, sides=8)
    return hp.finish(name, bm, out, budget=1000)


# ---- small props --------------------------------------------------------------------------------------------------------

def build_fence():
    """One cell of rail fence: two posts with angled tops, two rails and a brace, a little uneven like the reference."""
    bm = bmesh.new()
    length = hp.CELL
    out = []
    for x in (-length / 2 + 0.016, length / 2 - 0.016):
        out += box(bm, WORLD, (x, 0, 0.085), (0.03, 0.03, 0.17), TIMBER_RED)
        out += box(bm, WORLD, (x, 0, 0.176), (0.034, 0.034, 0.012), WOOD_DARK, (0.0, 0.35, 0.0))
    out += box(bm, WORLD, (0, -0.02, 0.135), (length - 0.01, 0.014, 0.022), WOOD_MID, (0.0, 0.025, 0.0))
    out += box(bm, WORLD, (0, -0.02, 0.07), (length - 0.01, 0.014, 0.022), WOOD_MID, (0.0, -0.02, 0.0))
    out += box(bm, WORLD, (-0.07, 0.02, 0.1), (0.14, 0.012, 0.016), WOOD_DARK, (0.0, -0.75, 0.0))
    return hp.finish("ForestFence", bm, out)


def build_woodpile():
    """A stack of logs between stakes, ends facing the street."""
    bm = bmesh.new()
    out = []
    radius, length = 0.028, 0.34
    for row, count in enumerate((6, 5, 4)):
        for k in range(count):
            x_off = (k - (count - 1) / 2) * radius * 2.05
            out += log(bm, (x_off, 0, radius + row * radius * 1.8), radius, length, PLANK_BROWN if (k + row) % 2 else WOOD_MID, LOG_END, sides=6, axis="y")
    for x in (-0.17, 0.17):
        for y in (-length / 2 - 0.01, length / 2 + 0.01):
            out += box(bm, WORLD, (x, y, 0.07), (0.016, 0.016, 0.14), WOOD_DARK)
    return hp.finish("ForestWoodPile", bm, out)


def build_lantern():
    """A dark post with a braced bracket and a hanging lamp."""
    bm = bmesh.new()
    out = box(bm, WORLD, (0, 0, 0.02), (0.05, 0.05, 0.04), STONE_D)
    out += box(bm, WORLD, (0, 0, 0.21), (0.026, 0.026, 0.34), TIMBER_RED)
    out += box(bm, WORLD, (0.05, 0, 0.365), (0.11, 0.018, 0.02), TIMBER_RED)
    out += box(bm, WORLD, (0.04, 0, 0.318), (0.075, 0.014, 0.014), WOOD_DARK, (0.0, -0.8, 0.0))
    out += box(bm, WORLD, (0.095, 0, 0.343), (0.004, 0.004, 0.025), IRON)
    out += box(bm, WORLD, (0.095, 0, 0.322), (0.05, 0.05, 0.012), IRON)
    out += box(bm, WORLD, (0.095, 0, 0.296), (0.04, 0.04, 0.042), GLOW)
    out += box(bm, WORLD, (0.095, 0, 0.272), (0.05, 0.05, 0.01), IRON)
    return hp.finish("ForestLantern", bm, out)


BUILDERS = [
    lambda: build_stall("ForestStallFruit", RED, WALL_CREAM, "fruit"),
    lambda: build_stall("ForestStallHerbs", GREEN, HAY, "herbs"),
    lambda: build_stall("ForestStallTimber", BLUE_D, WALL_CREAM, "timber"),
    lambda: build_cottage("ForestHouseA"),
    lambda: build_tall_house("ForestHouseB"),
    lambda: build_longhouse("ForestHouseC"),
    build_hall,
    build_fence,
    build_woodpile,
    build_lantern,
]


def render_view(objects, path, elevation_deg, columns, spacing=1.7):
    """The objects in a row(s), seen from the front at a lower angle than the contact sheet (the camera of the game looks from the south)."""
    scene = bpy.context.scene
    for old in [o for o in scene.objects if o.type == "CAMERA"]:
        bpy.data.objects.remove(old, do_unlink=True)
    for other in scene.objects:
        if other.type == "MESH":
            other.hide_render = other not in objects
    for i, obj in enumerate(objects):
        obj.location = ((i % columns) * spacing, -(i // columns) * spacing, 0)
    rows = (len(objects) + columns - 1) // columns
    centre = Vector(((columns - 1) * spacing / 2, -(rows - 1) * spacing / 2, 0.25))
    cam_data = bpy.data.cameras.new("CamClose")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = columns * spacing
    cam = bpy.data.objects.new("CamClose", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    elev = math.radians(elevation_deg)
    cam.location = centre + Vector((0.0, -8.0 * math.cos(elev), 8.0 * math.sin(elev)))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    render = scene.render
    render.resolution_x = 1800
    render.resolution_y = int(1800 * rows / columns * 0.8) + 200
    render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    write_palettes()
    hp.PALETTE = PALETTE
    hp.PREVIEW_PATH = PREVIEW_PATH
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    objects = [build() for build in BUILDERS]
    for obj in objects:
        hp.export(obj, f"{EXPORT_DIR}/{obj.name}.fbx")
    hp.render_sheet(objects, PREVIEW_PATH)
    by_name = {o.name: o for o in objects}
    stem = PREVIEW_PATH[:-4]
    render_view([by_name[n] for n in ("ForestHouseA", "ForestHouseB", "ForestHouseC")], f"{stem}_houses.png", 22, 3)
    render_view([by_name[n] for n in ("ForestStallFruit", "ForestStallHerbs", "ForestStallTimber")], f"{stem}_stalls.png", 28, 3)
    render_view([by_name[n] for n in ("ForestHouseLarge", "ForestFence", "ForestWoodPile", "ForestLantern")], f"{stem}_misc.png", 25, 4, spacing=1.7)


main()
