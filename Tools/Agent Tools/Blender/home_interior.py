"""The inside of the player's house (docs/HOME_ISLAND_PLAN.md): a diorama room and its furniture, in the dark timber style of the forest port.

Headless:  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python home_interior.py
Writes one FBX per piece into EXPORT_DIR and a preview of the furnished room, seen from the angle of the game camera, to PREVIEW_PATH.

Same conventions as harbor_props.py / harbor_forest_props.py (whose helpers and palette cells it reuses): metres, Z up, the colour is a flat UV at a
palette cell centre (no material of its own in Unity). The room: x east, y north; the camera looks towards +x/+y, so the walls are on the NORTH and
EAST sides and the south and west sides are open. After the import and the 180 degree model flip in Unity (HarborAssetBuilder.ModelFlip),
Unity (x, z) is Blender (x, y). The shell's origin is the centre of the floor, on top of it; props have their origin at the bottom centre
and their front towards -Y.

LAYOUT below is only for the preview; the real one is in Assets/Scripts/Editor/HomeHouseBuilder.cs (keep the two in step).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harbor_props as hp  # noqa: E402
import harbor_forest_props as hf  # noqa: E402

MAIN_PALETTE = hp.PALETTE  # the colours of the original harbour: the item icons use them, as they are meant to read on a light card
hp.PALETTE = hf.PALETTE  # the preview material shows the dark forest colours

EXPORT_DIR = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Home"
ICON_DIR = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ItemIcons/Buildable"
ICONS = {"HomeStool": "StoolIcon", "HomeTable": "TableIcon", "HomeRug": "RugIcon",  # the furniture the player builds
         "ForestHouseB": "HouseLevel2Icon", "ForestHouseC": "HouseLevel3Icon"}  # the houses the player upgrades to (drawn by harbor_forest_props.py)
PREVIEW_PATH = os.environ.get("HOME_PREVIEW", "C:/Users/Lukasz/AppData/Local/Temp/claude/e--Repos-Hearthglade/3cfa699b-9566-46ba-aae7-7c5f696f05df/scratchpad/home_interior.png")

CELL = 0.3675
# The house grows to the south and west: the north-east corner of the floor stays where it is (so the furniture of the house, the door and
# everything the player put in keep their cells), and the door and the windows keep their distance from it. Level -> (width, depth) in cells.
LEVEL_CELLS = {1: (8, 6), 2: (10, 7), 3: (12, 8)}
W, D = LEVEL_CELLS[1][0] * CELL, LEVEL_CELLS[1][1] * CELL  # the first level, which the preview and the layout below are drawn for
H, T = 0.735, 0.07  # wall height and thickness
DOOR_W, DOOR_H = 0.24, 0.42  # the door in the north wall
DOOR_FROM_EAST = 2.17  # distance of the door from the east wall
WIN_W, WIN_BOTTOM, WIN_TOP = 0.4, 0.3, 0.58  # the windows in the east wall
WIN_FROM_NORTH = 1.0  # distance of the first window from the north wall; one more every 1.1 m to the south per level above the first

# name -> (x, y, yaw in degrees as in Unity); the yaw turns the front (-Y, south) clockwise seen from above.
LAYOUT = {
    "HomeShell1": (0.0, 0.0, 0.0),
    "HomeBed": (W / 2 - 0.45, D / 2 - 0.275, 0.0),
    "HomeHearth": (-0.1, D / 2 - 0.125, 0.0),
    "HomeTable": (-0.25, -0.05, 0.0),
    "HomeStool@1": (-0.25, -0.38, 0.0),
    "HomeStool@2": (-0.66, -0.05, 0.0),
    "HomeShelf": (W / 2 - 0.11, -0.55, 90.0),
    "HomeRug": (0.35, -0.35, 0.0),
}

BLACK, WOOD_DARK, WOOD_MID, PLANK_TAN, PLANK_BROWN = hf.BLACK, hf.WOOD_DARK, hf.WOOD_MID, hf.PLANK_TAN, hf.PLANK_BROWN
PLASTER, TIMBER, STONE, STONE_D, IRON, COPPER, GLOW = hf.PLASTER_DIM, hf.TIMBER_RED, hf.STONE, hf.STONE_D, hf.IRON, hf.COPPER, hf.GLOW


def B(bm, center, size, cell, rot=(0.0, 0.0, 0.0)):
    return hf.box(bm, hf.WORLD, center, size, cell, rot)


def span_box(bm, x0, x1, y0, y1, z0, z1, cell):
    """A box from its extremes."""
    return B(bm, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), cell)


# ---- the room ---------------------------------------------------------------------------------------------------------

def build_shell(level=1):
    W, D = LEVEL_CELLS[level][0] * CELL, LEVEL_CELLS[level][1] * CELL
    door_x = W / 2 - DOOR_FROM_EAST
    bm = bmesh.new()
    out = []

    # Floor: ten boards along x, the top of the floor is z = 0.
    boards = 10
    board = D / boards
    for i in range(boards):
        out += span_box(bm, -W / 2, W / 2, -D / 2 + board * i, -D / 2 + board * (i + 1), -0.05, 0.0, PLANK_TAN if i % 2 == 0 else PLANK_BROWN)

    # North wall with the door opening (inner face at y = D/2). Pieces: left of the door, right of it, the lintel.
    d0, d1 = door_x - DOOR_W / 2, door_x + DOOR_W / 2
    out += span_box(bm, -W / 2, d0, D / 2, D / 2 + T, 0.0, H, PLASTER)
    out += span_box(bm, d1, W / 2 + T, D / 2, D / 2 + T, 0.0, H, PLASTER)
    out += span_box(bm, d0, d1, D / 2, D / 2 + T, DOOR_H, H, PLASTER)
    # Door frame (embedded 2 mm into the wall so no two faces share a plane) and the closed leaf in the opening.
    for x in (d0 - 0.015, d1 + 0.015):
        out += span_box(bm, x - 0.015, x + 0.015, D / 2 - 0.012, D / 2 + 0.002, 0.0, DOOR_H + 0.03, TIMBER)
    out += span_box(bm, d0 - 0.03, d1 + 0.03, D / 2 - 0.012, D / 2 + 0.002, DOOR_H, DOOR_H + 0.03, TIMBER)
    out += span_box(bm, d0 + 0.004, d1 - 0.004, D / 2 + 0.015, D / 2 + 0.045, 0.0, DOOR_H - 0.004, WOOD_MID)
    out += B(bm, (d1 - 0.035, D / 2 + 0.008, 0.2), (0.02, 0.016, 0.02), COPPER)

    # East wall (inner face at x = W/2) with its windows, up to the north wall. Level 1 has one window, every level above one more.
    window_ys = [D / 2 - WIN_FROM_NORTH - 1.1 * k for k in range(level)]
    gaps = []
    for win_y in window_ys:
        gaps.append((win_y - WIN_W / 2, win_y + WIN_W / 2))
    edges = [-D / 2] + [e for gap in sorted(gaps) for e in gap] + [D / 2]
    for i in range(0, len(edges), 2):  # the solid wall between the windows
        if edges[i + 1] > edges[i]:
            out += span_box(bm, W / 2, W / 2 + T, edges[i], edges[i + 1], 0.0, H, PLASTER)
    for win_y in window_ys:
        w0, w1 = win_y - WIN_W / 2, win_y + WIN_W / 2
        out += span_box(bm, W / 2, W / 2 + T, w0, w1, 0.0, WIN_BOTTOM, PLASTER)
        out += span_box(bm, W / 2, W / 2 + T, w0, w1, WIN_TOP, H, PLASTER)
        # The pane glows (the forest palette lights it), with a cross of bars and a sill on the inside.
        out += span_box(bm, W / 2 + 0.025, W / 2 + 0.045, w0, w1, WIN_BOTTOM, WIN_TOP, GLOW)
        out += span_box(bm, W / 2 - 0.012, W / 2 + 0.05, win_y - 0.01, win_y + 0.01, WIN_BOTTOM, WIN_TOP, WOOD_DARK)
        out += span_box(bm, W / 2 - 0.012, W / 2 + 0.05, w0, w1, (WIN_BOTTOM + WIN_TOP) / 2 - 0.01, (WIN_BOTTOM + WIN_TOP) / 2 + 0.01, WOOD_DARK)
        out += span_box(bm, W / 2 - 0.05, W / 2 + 0.002, w0 - 0.03, w1 + 0.03, WIN_BOTTOM - 0.02, WIN_BOTTOM, WOOD_MID)
        for y in (w0 - 0.015, w1 + 0.015):
            out += span_box(bm, W / 2 - 0.012, W / 2 + 0.002, y - 0.015, y + 0.015, WIN_BOTTOM, WIN_TOP + 0.03, TIMBER)
        out += span_box(bm, W / 2 - 0.012, W / 2 + 0.002, w0 - 0.03, w1 + 0.03, WIN_TOP, WIN_TOP + 0.03, TIMBER)

    # Caps on the top of both walls, a post in the corner.
    out += span_box(bm, -W / 2, W / 2 + T + 0.01, D / 2 - 0.005, D / 2 + T + 0.01, H, H + 0.03, TIMBER)
    out += span_box(bm, W / 2 - 0.005, W / 2 + T + 0.01, -D / 2, D / 2, H, H + 0.03, TIMBER)
    out += span_box(bm, W / 2 - 0.012, W / 2 + 0.002, D / 2 - 0.012, D / 2 + 0.002, 0.0, H, TIMBER)

    # Beams along the open south and west edges of the floor, so the room has a finished rim.
    out += span_box(bm, -W / 2 - 0.04, W / 2, -D / 2 - 0.04, -D / 2, -0.05, 0.025, WOOD_DARK)
    out += span_box(bm, -W / 2 - 0.04, -W / 2, -D / 2, D / 2, -0.05, 0.025, WOOD_DARK)
    return hp.finish(f"HomeShell{level}", bm, out, budget=1200)


# ---- furniture --------------------------------------------------------------------------------------------------------

def build_bed():
    """Along x, the head at +x (against the east wall)."""
    bm = bmesh.new()
    out = []
    length, width = 0.9, 0.55
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += B(bm, (sx * (length / 2 - 0.03), sy * (width / 2 - 0.03), 0.03), (0.04, 0.04, 0.06), WOOD_DARK)
    out += B(bm, (0, 0, 0.085), (length, width, 0.05), WOOD_MID)  # frame
    out += B(bm, (0, 0, 0.125), (length - 0.04, width - 0.04, 0.04), hf.WALL_CREAM)  # mattress
    out += B(bm, (-0.12, 0, 0.135), (0.5, width - 0.03, 0.045), hf.BLUE_D)  # blanket over the foot half
    out += B(bm, (length / 2 - 0.1, 0, 0.16), (0.14, width - 0.18, 0.04), hf.WALL_WHITE)  # pillow
    out += B(bm, (length / 2 - 0.015, 0, 0.17), (0.03, width, 0.24), WOOD_DARK)  # headboard
    out += B(bm, (-length / 2 + 0.015, 0, 0.12), (0.03, width, 0.12), WOOD_DARK)  # footboard
    return hp.finish("HomeBed", bm, out)


def build_table():
    bm = bmesh.new()
    out = []
    w, d, h = 0.64, 0.33, 0.2  # two cells by one (0.735 x 0.3675 m), with a little air round it
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += B(bm, (sx * (w / 2 - 0.03), sy * (d / 2 - 0.03), (h - 0.03) / 2), (0.035, 0.035, h - 0.03), WOOD_MID)
    out += B(bm, (0, 0, h - 0.015), (w, d, 0.03), PLANK_TAN)
    out += hp.add_cyl(bm, (0.1, 0.05, h + 0.03), 0.03, 0.06, COPPER, sides=8)  # a jug
    out += B(bm, (-0.12, -0.04, h + 0.012), (0.1, 0.07, 0.024), hf.WALL_WHITE)  # a plate
    return hp.finish("HomeTable", bm, out)


def build_stool():
    bm = bmesh.new()
    out = []
    out += hp.add_cyl(bm, (0, 0, 0.115), 0.075, 0.03, PLANK_TAN, sides=8)
    for k in range(3):
        a = 2 * math.pi * k / 3
        out += B(bm, (0.04 * math.cos(a), 0.04 * math.sin(a), 0.05), (0.025, 0.025, 0.1), WOOD_MID)
    return hp.finish("HomeStool", bm, out)


def build_hearth():
    """Against the north wall: the front towards -Y, 0.5 wide and 0.25 deep, with a chimney breast up to the top of the wall."""
    bm = bmesh.new()
    out = []
    w, d = 0.5, 0.25
    out += B(bm, (0, 0, 0.09), (w, d, 0.18), STONE)  # the hearth
    out += B(bm, (0, -d / 2 + 0.004, 0.09), (0.3, 0.012, 0.13), BLACK)  # the opening
    out += B(bm, (0, -d / 2 + 0.014, 0.035), (0.2, 0.012, 0.03), hf.YELLOW_L)  # embers (glow)
    out += B(bm, (0, -d / 2 + 0.016, 0.065), (0.12, 0.012, 0.03), GLOW)
    out += B(bm, (0, -0.01, 0.2), (w + 0.06, d - 0.02, 0.04), WOOD_DARK)  # mantel
    out += B(bm, (0, d / 2 - 0.1, 0.46), (0.34, 0.2, 0.52), STONE_D)  # breast
    out += B(bm, (0.2, -0.03, 0.04), (0.06, 0.06, 0.08), WOOD_MID)  # logs
    return hp.finish("HomeHearth", bm, out)


def build_shelf():
    """A cupboard with open shelves; the front towards -Y, 0.5 wide and 0.22 deep."""
    bm = bmesh.new()
    out = []
    w, d, h = 0.5, 0.22, 0.55
    out += B(bm, (0, d / 2 - 0.01, h / 2), (w, 0.02, h), WOOD_DARK)  # back
    for sx in (-1, 1):
        out += B(bm, (sx * (w / 2 - 0.01), 0, h / 2), (0.02, d, h), WOOD_MID)  # sides
    for z in (0.0, 0.2, 0.38, h - 0.02):
        out += B(bm, (0, 0, z + 0.01), (w, d, 0.02), PLANK_TAN)  # shelves
    out += B(bm, (-0.14, 0, 0.3), (0.08, 0.08, 0.08), hf.RED)  # things on the shelves
    out += B(bm, (0.0, 0, 0.29), (0.07, 0.07, 0.06), hf.GREEN)
    out += B(bm, (0.13, 0, 0.32), (0.09, 0.09, 0.12), COPPER)
    out += B(bm, (-0.1, 0, 0.12), (0.2, 0.12, 0.1), hf.HAY)
    out += B(bm, (0.12, 0, 0.11), (0.14, 0.12, 0.08), hf.WALL_WHITE)
    out += B(bm, (-0.05, 0, 0.465), (0.2, 0.1, 0.09), hf.BLUE_D)
    return hp.finish("HomeShelf", bm, out)


def build_rug():
    bm = bmesh.new()
    out = []
    out += B(bm, (0, 0, 0.006), (1.0, 0.66, 0.012), hf.RED)  # three cells by two (1.1 x 0.735 m)
    out += B(bm, (0, 0, 0.008), (0.84, 0.5, 0.012), hf.ORANGE)
    out += B(bm, (0, 0, 0.010), (0.46, 0.26, 0.012), hf.YELLOW_L)
    return hp.finish("HomeRug", bm, out)


def build_void():
    """A big dark slab to put under the room, so that nothing but the room is seen around it."""
    bm = bmesh.new()
    out = B(bm, (0, 0, -0.07), (40.0, 40.0, 0.02), BLACK)
    return hp.finish("HomeVoid", bm, out)


BUILDERS = [lambda: build_shell(1), lambda: build_shell(2), lambda: build_shell(3), build_bed, build_table, build_stool, build_hearth, build_shelf, build_rug, build_void]


# ---- preview ----------------------------------------------------------------------------------------------------------

def render_room(objects, path):
    scene = bpy.context.scene
    by_name = {obj.name: obj for obj in objects}
    placed = []
    for key, (x, y, yaw) in LAYOUT.items():
        source = by_name[key.split("@")[0]]
        obj = source if key == source.name else source.copy()
        if obj is not source:
            scene.collection.objects.link(obj)
        obj.location = (x, y, 0.0)
        obj.rotation_euler = Euler((0.0, 0.0, -math.radians(yaw)), "XYZ")
        placed.append(obj)
    by_name["HomeVoid"].location = (0, 0, 0)
    for level in (2, 3):  # the preview shows the first level
        by_name[f"HomeShell{level}"].hide_render = True

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 4.4
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    target = Vector((0.0, 0.0, 0.25))
    cam.location = target + Vector((-6.0, -10.0, 6.0))  # the game camera's offset (-6, 6, -10) in Blender axes
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    render = scene.render
    render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_object_outline = True
    render.resolution_x, render.resolution_y = 1600, 1000
    render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.5, 0.65, 0.8)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendered", path)


def render_icons(objects):
    """512 x 512 item icons of the furniture that can be built (transparent background, the angle of render_icon.py, framed on the model)."""
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
    os.makedirs(ICON_DIR, exist_ok=True)
    main_image = bpy.data.images.load(MAIN_PALETTE, check_existing=True)
    swapped = []
    for obj in objects:
        for slot in obj.material_slots:
            for node in slot.material.node_tree.nodes:
                if node.type == "TEX_IMAGE":
                    swapped.append((node, node.image))
                    node.image = main_image
    for obj in objects:
        if obj.name not in ICONS:
            continue
        for other in objects:
            other.hide_render = other is not obj
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
        high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
        target = (low + high) / 2
        cam.location = target + Vector((0.263, -0.337, 0.139)) * 8.0  # far away: orthographic, so only the direction matters
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam_data.ortho_scale = max((high - low).length * 0.95, 0.2)
        render.filepath = f"{ICON_DIR}/{ICONS[obj.name]}.png"
        bpy.ops.render.render(write_still=True)
        print("icon", render.filepath)
    for node, image in swapped:
        node.image = image
    for other in objects:
        other.hide_render = False
    render.film_transparent = False
    scene.camera = None
    bpy.data.objects.remove(cam, do_unlink=True)


def main():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    objects = [build() for build in BUILDERS]
    for obj in objects:
        hp.export(obj, f"{EXPORT_DIR}/{obj.name}.fbx")
    houses = [hf.build_tall_house("ForestHouseB"), hf.build_longhouse("ForestHouseC")]  # for the icons only, they are exported by their own script
    render_icons(objects + houses)
    for house in houses:
        bpy.data.objects.remove(house, do_unlink=True)
    render_room(objects, PREVIEW_PATH)


if __name__ == "__main__":
    main()
