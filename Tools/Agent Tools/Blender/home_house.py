"""The player's house on the home island, seen from outside (docs/HOME_ISLAND_PLAN.md): a cabin of dark timber on a fieldstone foundation,
after the reference of a small wooden cottage by the water: plank walls in a timber frame, a steep shingled roof, a stone chimney on the gable
end, a covered porch with a step and a hanging lamp, glowing windows with shutters and flower boxes.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python home_house.py
Writes HomeHouse<level>.fbx into EXPORT_DIR (only level 1 so far; the levels above still use the forest houses) and previews of it.

Same conventions as harbor_forest_props.py (whose helpers, frames and palette cells it reuses): metres, Z up, origin at the bottom centre,
the front (door side) faces -Y. The model is drawn at the size it has in the game (scale 1 in Unity).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harbor_props as hp  # noqa: E402
import harbor_forest_props as hf  # noqa: E402

hp.PALETTE = hf.PALETTE  # the forest colours, the material of the other houses

EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Home"
PREVIEW_PATH = os.environ.get("HOME_HOUSE_PREVIEW", "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/3d375fb7-214e-4255-ae4d-00766cf3f4be/scratchpad/home_house.png")

(WOOD_DARK, WOOD_MID, PLANK_TAN, PLANK_BROWN, STONE, STONE_D, IRON, COPPER, GLOW, TIMBER, ROOF_RUST, GREEN_L, FERN, APPLE_GREEN,
 PINK, RED_BRIGHT, YELLOW, STONE_WARM, LOG_END, BLACK) = (
    hf.WOOD_DARK, hf.WOOD_MID, hf.PLANK_TAN, hf.PLANK_BROWN, hf.STONE, hf.STONE_D, hf.IRON, hf.COPPER, hf.GLOW, hf.TIMBER_RED, hf.ROOF_RUST,
    hf.GREEN_L, hf.FERN, hf.APPLE_GREEN, hf.PINK, hf.RED_BRIGHT, hf.YELLOW, hf.PLASTER_SHADE, hf.LOG_END, hf.BLACK)

PLANK_LIGHT, PLANK_PALE = hf.PLANK_LIGHT, hf.PLANK_PALE
WORLD, WALL_FRONT, WALL_RIGHT, WALL_BACK, WALL_LEFT = hf.WORLD, hf.WALL_FRONT, hf.WALL_RIGHT, hf.WALL_BACK, hf.WALL_LEFT
Frame = hf.Frame
box = hf.box

# The body: width (x, along the ridge), depth (y), the stone foundation, the walls above it and the rise of the roof.
W, D = 1.2, 0.8
FOUNDATION, WALL_H, RISE = 0.10, 0.44, 0.40
BASE = FOUNDATION + WALL_H  # the top of the walls, where the roof starts
# The door under the porch, in the front wall.
DOOR_W, DOOR_H = 0.22, 0.33
# The porch: a deck in front of the door, two posts and its own gable roof.
PORCH_W, PORCH_D, DECK_TOP = 0.6, 0.34, FOUNDATION + 0.02
POST_TOP = 0.47
STONES = [STONE, STONE_D, STONE_WARM, STONE, STONE]


class Scatter:
    """Cells picked pseudo-randomly by index: the roof helper indexes its list with a running number, which on a short list makes diagonal
    stripes; this is a long 'list' with no pattern, mostly the main brown with a few darker and lighter shingles."""

    def __init__(self, cells, seed=5):
        rng = random.Random(seed)
        self.cells = [rng.choice(cells) for _ in range(1009)]

    def __len__(self):
        return len(self.cells)

    def __getitem__(self, i):
        return self.cells[i]


SHINGLES = Scatter([WOOD_MID] * 6 + [PLANK_BROWN] * 3 + [ROOF_RUST])
BOARDS = [PLANK_LIGHT, PLANK_PALE, PLANK_TAN, PLANK_LIGHT, PLANK_PALE]


# ---- stone ------------------------------------------------------------------------------------------------------------------

def stone_face(bm, f, depth, length, z0, z1, rng, row_h=0.055, relief=0.022):
    """Rows of uneven fieldstones on a wall (outside normal local -Y at `depth` from the frame origin), the dark core shows as mortar."""
    rows = max(1, round((z1 - z0) / row_h))
    h = (z1 - z0) / rows
    out = []
    for r in range(rows):
        x = -length / 2 - (rng.uniform(0.03, 0.09) if r % 2 else 0.0)
        while x < length / 2:
            width = rng.uniform(0.085, 0.16)
            x0, x1 = max(x, -length / 2), min(x + width, length / 2)
            if x1 - x0 > 0.035:
                out_by = relief * rng.uniform(0.7, 1.25)
                height = h - 0.008 + rng.uniform(-0.008, 0.004)
                out += box(bm, f, ((x0 + x1) / 2, -depth - out_by / 2 + 0.004, z0 + (r + 0.5) * h), (x1 - x0 - 0.008, out_by, height), STONES[rng.randrange(len(STONES))])
            x += width
    return out


def foundation(bm, rng):
    out = box(bm, WORLD, (0, 0, FOUNDATION / 2), (W + 0.03, D + 0.03, FOUNDATION), STONE_D)
    for f, depth, length in ((WALL_FRONT, D / 2 + 0.015, W + 0.03), (WALL_BACK, D / 2 + 0.015, W + 0.03),
                             (WALL_RIGHT, W / 2 + 0.015, D + 0.03), (WALL_LEFT, W / 2 + 0.015, D + 0.03)):
        out += stone_face(bm, f, depth, length, 0.0, FOUNDATION, rng)
    return out


def chimney(bm, rng):
    """A fieldstone chimney against the west gable: a wide firebox, a shoulder, a narrower shaft and a capped top."""
    out = []
    sections = ((0.2, 0.28, 0.0, 0.40), (0.15, 0.22, 0.40, 1.0))
    for sx, sy, z0, z1 in sections:
        cx = -W / 2 - sx / 2 + 0.02
        out += box(bm, WORLD, (cx, 0, (z0 + z1) / 2), (sx, sy, z1 - z0), STONE_D)
        for f, depth, length in ((Frame(cx, 0, 270), sx / 2, sy), (Frame(cx, 0, 0), sy / 2, sx - 0.02), (Frame(cx, 0, 180), sy / 2, sx - 0.02)):
            out += stone_face(bm, f, depth, length, z0, z1, rng, relief=0.02)
    cx = -W / 2 - 0.2 / 2 + 0.02
    out += box(bm, WORLD, (cx, 0, 0.405), (0.22, 0.30, 0.03), STONE)  # the shoulder
    cx = -W / 2 - 0.15 / 2 + 0.02
    out += box(bm, WORLD, (cx, 0, 1.015), (0.19, 0.26, 0.03), STONE)
    out += box(bm, WORLD, (cx, 0, 1.04), (0.11, 0.16, 0.02), IRON)
    return out


# ---- walls, roof ------------------------------------------------------------------------------------------------------------

def plank_wall(bm, f, depth, length, rng):
    """Horizontal boards in a few browns on a dark core, a sill and a top plate in the timber colour."""
    rows = 7
    h = WALL_H / rows
    out = []
    for i in range(rows):
        out += box(bm, f, (0, -depth - 0.003, FOUNDATION + (i + 0.5) * h), (length, 0.012, h - 0.004), BOARDS[rng.randrange(len(BOARDS))])
    out += box(bm, f, (0, -depth - 0.006, BASE - 0.018), (length + 0.05, 0.02, 0.036), TIMBER)
    out += box(bm, f, (0, -depth - 0.006, FOUNDATION + 0.014), (length + 0.05, 0.02, 0.028), TIMBER)
    return out


def gable_boards(bm, f, depth):
    """Vertical boards in the gable triangle (west and east wall), a little below the slope so that the roof's own triangle shows between."""
    strips = 10
    width = D / strips
    out = []
    for i in range(strips):
        y = -D / 2 + (i + 0.5) * width
        h = RISE * (1 - (abs(y) + width / 2) / (D / 2)) * 0.97
        out += box(bm, f, (y, -depth - 0.004, BASE + h / 2), (width - 0.004, 0.01, h), (PLANK_LIGHT, PLANK_TAN, PLANK_PALE)[i % 3])
    return out


def main_roof(bm):
    return hf.roof(bm, WORLD, 0, 0, W, D, BASE, RISE, 0.08, SHINGLES, PLANK_TAN, courses=9, ov_x=0.08, cols=12)


def corner_posts(bm):
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += box(bm, WORLD, (sx * W / 2, sy * D / 2, FOUNDATION + WALL_H / 2), (0.058, 0.058, WALL_H), TIMBER)
    return out


# ---- openings ---------------------------------------------------------------------------------------------------------------

def window(bm, f, depth, x, z, w=0.16, h=0.17, shutters=True, flowers=False, seed=0):
    """A glowing window on a wall (outside = local -Y, the board face at `depth`): frame, glass, bars, sill, shutters, a flower box."""
    out = box(bm, f, (x, -depth - 0.006, z), (w + 0.05, 0.012, h + 0.05), TIMBER)
    out += box(bm, f, (x, -depth - 0.008, z), (w, 0.012, h), GLOW)
    out += box(bm, f, (x, -depth - 0.0145, z), (0.012, 0.006, h + 0.004), WOOD_DARK)
    out += box(bm, f, (x, -depth - 0.0145, z), (w + 0.004, 0.006, 0.012), WOOD_DARK)
    out += box(bm, f, (x, -depth - 0.022, z - h / 2 - 0.03), (w + 0.09, 0.04, 0.02), WOOD_MID)
    if shutters:
        for s in (-1, 1):
            out += box(bm, f, (x + s * (w / 2 + 0.047), -depth - 0.006, z), (0.04, 0.01, h + 0.04), PLANK_BROWN)
            out += box(bm, f, (x + s * (w / 2 + 0.047), -depth - 0.012, z + h * 0.18), (0.04, 0.006, 0.012), WOOD_DARK)
            out += box(bm, f, (x + s * (w / 2 + 0.047), -depth - 0.012, z - h * 0.18), (0.04, 0.006, 0.012), WOOD_DARK)
    if flowers:
        rng = random.Random(seed)
        by = z - h / 2 - 0.085
        out += box(bm, f, (x, -depth - 0.045, by), (w + 0.07, 0.06, 0.05), WOOD_MID)
        for k in range(4):
            bx = x + (k - 1.5) * (w + 0.05) / 4
            blob = hp.add_blob(bm, f.p((bx, -depth - 0.05, by + 0.045)), (0.035, 0.03, 0.03), (GREEN_L, FERN)[k % 2], seed=seed + k)
            out += blob
            out += box(bm, f, (bx + rng.uniform(-0.01, 0.01), -depth - 0.055, by + 0.075), (0.016, 0.016, 0.016), (PINK, YELLOW, RED_BRIGHT, PINK)[k], (0, 0, rng.uniform(0, 1)))
    return out


def door(bm, f, depth, z0):
    """A plank door in a timber frame, iron straps, a handle and a small glowing pane."""
    out = []
    for s in (-1, 1):
        out += box(bm, f, (s * (DOOR_W / 2 + 0.018), -depth - 0.006, z0 + DOOR_H / 2 + 0.015), (0.036, 0.016, DOOR_H + 0.03), TIMBER)
    out += box(bm, f, (0, -depth - 0.006, z0 + DOOR_H + 0.018), (DOOR_W + 0.09, 0.016, 0.036), TIMBER)
    planks = 3
    pw = DOOR_W / planks
    for i in range(planks):
        out += box(bm, f, ((i - 1) * pw, -depth - 0.008, z0 + DOOR_H / 2), (pw - 0.004, 0.012, DOOR_H), (PLANK_TAN, WOOD_MID, PLANK_TAN)[i])
    for z in (0.22, 0.07):
        out += box(bm, f, (0, -depth - 0.016, z0 + z), (DOOR_W, 0.006, 0.02), IRON)
    out += box(bm, f, (0, -depth - 0.016, z0 + DOOR_H * 0.74), (0.07, 0.008, 0.07), TIMBER)
    out += box(bm, f, (0, -depth - 0.02, z0 + DOOR_H * 0.74), (0.05, 0.006, 0.05), GLOW)
    out += box(bm, f, (DOOR_W / 2 - 0.035, -depth - 0.02, z0 + DOOR_H * 0.45), (0.018, 0.012, 0.018), COPPER)
    return out


# ---- the porch --------------------------------------------------------------------------------------------------------------

def porch(bm, rng):
    front = -D / 2 - PORCH_D  # the front edge of the deck
    out = []
    # The deck: boards along the depth, on stone footings.
    out += box(bm, WORLD, (0, -D / 2 - PORCH_D / 2 + 0.01, DECK_TOP - 0.03), (PORCH_W, PORCH_D + 0.02, 0.06), WOOD_DARK)
    boards = 7
    for i in range(boards):
        x = -PORCH_W / 2 + (i + 0.5) * PORCH_W / boards
        out += box(bm, WORLD, (x, -D / 2 - PORCH_D / 2 + 0.01, DECK_TOP - 0.004), (PORCH_W / boards - 0.006, PORCH_D + 0.02, 0.012), (PLANK_LIGHT, PLANK_TAN)[i % 2])
    out += box(bm, WORLD, (0, front - 0.012, DECK_TOP - 0.025), (PORCH_W + 0.03, 0.03, 0.05), TIMBER)  # the front edge board
    # A stone step in front of the deck.
    out += box(bm, WORLD, (0, front - 0.075, 0.03), (0.46, 0.12, 0.06), STONE)
    out += box(bm, WORLD, (0, front - 0.075, 0.062), (0.46, 0.12, 0.012), STONE_WARM)
    # Two posts on stone feet, a beam and diagonal braces under it.
    for s in (-1, 1):
        x = s * (PORCH_W / 2 - 0.045)
        out += box(bm, WORLD, (x, front + 0.04, DECK_TOP + 0.02), (0.085, 0.085, 0.04), STONE)
        out += box(bm, WORLD, (x, front + 0.04, (DECK_TOP + POST_TOP) / 2 + 0.02), (0.05, 0.05, POST_TOP - DECK_TOP), TIMBER)
        out += box(bm, WORLD, (x - s * 0.06, front + 0.04, POST_TOP - 0.06), (0.1, 0.02, 0.02), TIMBER, (0.0, s * 0.785, 0.0))
    out += box(bm, WORLD, (0, front + 0.04, POST_TOP - 0.01), (PORCH_W - 0.01, 0.055, 0.045), TIMBER)
    # The porch roof: its ridge runs back into the main roof, so the two meet in a valley.
    length = PORCH_D + 0.16
    y_front = front - 0.06
    out += hf.roof(bm, Frame(0, y_front + (length + 0.12) / 2 - 0.0, 90), 0, 0, length, PORCH_W + 0.02, POST_TOP + 0.03, 0.17, 0.05, SHINGLES, PLANK_BROWN, courses=4, ov_x=0.06, cols=7)
    # The gable in front of the porch roof: boards, a king post and a glowing round window.
    out += hf.wall_disc(bm, WORLD, (0, front - 0.012, POST_TOP + 0.105), 0.042, 0.01, TIMBER)
    out += hf.wall_disc(bm, WORLD, (0, front - 0.018, POST_TOP + 0.105), 0.03, 0.01, GLOW)
    # A lamp on a short bracket by the door.
    lamp_x, lamp_y = -PORCH_W / 2 + 0.2, front + 0.04
    out += box(bm, WORLD, (lamp_x, lamp_y, POST_TOP - 0.06), (0.004, 0.004, 0.05), IRON)
    out += box(bm, WORLD, (lamp_x, lamp_y, POST_TOP - 0.092), (0.05, 0.05, 0.01), IRON)
    out += box(bm, WORLD, (lamp_x, lamp_y, POST_TOP - 0.12), (0.04, 0.04, 0.05), GLOW)
    out += box(bm, WORLD, (lamp_x, lamp_y, POST_TOP - 0.15), (0.05, 0.05, 0.01), IRON)
    return out


def path(bm, rng):
    """Flat stones from the step towards the south."""
    out = []
    front = -D / 2 - PORCH_D - 0.14
    for i in range(3):
        w, d = rng.uniform(0.2, 0.3), rng.uniform(0.15, 0.2)
        out += box(bm, WORLD, (rng.uniform(-0.05, 0.05), front - 0.08 - i * 0.2, 0.012), (w, d, 0.024), STONES[i % 3], (0.0, 0.0, rng.uniform(-0.3, 0.3)))
    return out


def woodpile(bm, x, y):
    """A small stack of logs against the east wall, ends to the front."""
    out = []
    radius, length = 0.024, 0.2
    for row, count in enumerate((4, 3, 2)):
        for k in range(count):
            out += hf.log(bm, (x + (k - (count - 1) / 2) * radius * 2.05, y, radius + row * radius * 1.75), radius, length, PLANK_BROWN if (k + row) % 2 else WOOD_MID, LOG_END, sides=6, axis="y")
    for dx in (-0.115, 0.115):
        out += box(bm, WORLD, (x + dx, y - length / 2 - 0.01, 0.06), (0.014, 0.014, 0.12), WOOD_DARK)
        out += box(bm, WORLD, (x + dx, y + length / 2 + 0.01, 0.06), (0.014, 0.014, 0.12), WOOD_DARK)
    return out


# ---- the house --------------------------------------------------------------------------------------------------------------

def build_house(name="HomeHouse1"):
    rng = random.Random(11)
    bm = bmesh.new()
    out = foundation(bm, rng)
    out += box(bm, WORLD, (0, 0, FOUNDATION + WALL_H / 2), (W, D, WALL_H), WOOD_MID)
    out += main_roof(bm)
    out += corner_posts(bm)
    out += plank_wall(bm, WALL_FRONT, D / 2, W, rng) + plank_wall(bm, WALL_BACK, D / 2, W, rng)
    out += plank_wall(bm, WALL_RIGHT, W / 2, D, rng) + plank_wall(bm, WALL_LEFT, W / 2, D, rng)
    out += gable_boards(bm, WALL_RIGHT, W / 2) + gable_boards(bm, WALL_LEFT, W / 2)
    # Front: the door under the porch and a window with a flower box on each side.
    surface = D / 2 + 0.009
    out += door(bm, WALL_FRONT, surface, DECK_TOP)
    for s, seed in ((-1, 3), (1, 7)):
        out += window(bm, WALL_FRONT, surface, s * 0.42, FOUNDATION + WALL_H * 0.58, flowers=True, seed=seed)
    # The east gable: a window below a round one under the peak; the back: one window.
    east = W / 2 + 0.009
    out += window(bm, WALL_RIGHT, east, 0.1, FOUNDATION + WALL_H * 0.55, shutters=False)
    out += hf.wall_disc(bm, WALL_RIGHT, (0, -east - 0.012, BASE + RISE * 0.36), 0.07, 0.01, TIMBER)
    out += hf.wall_disc(bm, WALL_RIGHT, (0, -east - 0.018, BASE + RISE * 0.36), 0.05, 0.01, GLOW)
    out += window(bm, WALL_BACK, surface, 0.0, FOUNDATION + WALL_H * 0.58, shutters=True)
    out += chimney(bm, rng)
    out += porch(bm, rng)
    out += path(bm, rng)
    out += woodpile(bm, W / 2 + 0.17, -0.2)
    return hp.finish(name, bm, out, budget=9000)


# ---- previews ---------------------------------------------------------------------------------------------------------------

def render(obj, path, elevation_deg, azimuth_deg, scale=2.2, ground=True):
    """The house with a patch of grass, seen from the south (azimuth 0) turned by azimuth_deg."""
    scene = bpy.context.scene
    for old in [o for o in scene.objects if o.type == "CAMERA"]:
        bpy.data.objects.remove(old, do_unlink=True)
    obj.location = (0, 0, 0)
    centre = Vector((0.0, -0.1, 0.5))
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = scale
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    el, az = math.radians(elevation_deg), math.radians(azimuth_deg)
    cam.location = centre + Vector((8 * math.sin(az) * math.cos(el), -8 * math.cos(az) * math.cos(el), 8 * math.sin(el)))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    render = scene.render
    render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_object_outline = True
    render.resolution_x, render.resolution_y = 1400, 1000
    render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.45, 0.62, 0.42)
    render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendered", path)


def main():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    hf.write_palettes()  # the forest palette with the light plank cells this house uses
    house = build_house()
    hp.export(house, f"{EXPORT_DIR}/{house.name}.fbx")
    stem = PREVIEW_PATH[:-4]
    render(house, f"{stem}_front.png", 24, 0)
    render(house, f"{stem}_sw.png", 28, 40)
    render(house, f"{stem}_se.png", 28, -40)
    render(house, f"{stem}_back.png", 24, 160)


if __name__ == "__main__":
    main()
