"""Renders a 512x512 item icon (transparent background) of one object, in the same style for every asset.

Paste into Blender's Scripting tab, or run it through the Blender MCP (execute_blender_code). Set OBJECT, OUT and, if the
object is not about 0.2 m across, TARGET_Z / DISTANCE, then check the result: a good icon fills roughly 80% of the frame
with equal margins on all sides. Blender scene settings are restored afterwards.

Made for the MushroomBrown icon (Assets/Arts/Sprites/ItemIcons/Usable/MushroomBrown.png). In Unity the png is imported as a
Sprite with the same settings as the other item icons and assigned to the item's `icon` field.
"""
import bpy
from mathutils import Vector

OBJECT = "MushroomBrown"
OUT = "C:/Temp/icon.png"
TARGET_Z = 0.0855          # height of the point the camera looks at (about the middle of the object), metres
DISTANCE = 0.78            # multiplier of the base camera offset below; 0.78 frames an object about 0.2 m across
BASE_OFFSET = Vector((0.263, -0.337, 0.139))   # camera position relative to the target: 38 degrees around, 18 degrees up

scene = bpy.context.scene
render = scene.render
shading = scene.display.shading
assert OBJECT in bpy.data.objects, f"no object named {OBJECT}"

saved = {
    "engine": render.engine, "x": render.resolution_x, "y": render.resolution_y, "pct": render.resolution_percentage,
    "film": render.film_transparent, "path": render.filepath, "fmt": render.image_settings.file_format,
    "mode": render.image_settings.color_mode, "camera": scene.camera,
    "light": shading.light, "color_type": shading.color_type, "outline": shading.show_object_outline,
    "outline_color": tuple(shading.object_outline_color),
}

camera_data = bpy.data.cameras.new("IconCamera")
camera = bpy.data.objects.new("IconCamera", camera_data)
scene.collection.objects.link(camera)
target = Vector((0.0, 0.0, TARGET_Z))
camera.location = target + BASE_OFFSET * DISTANCE
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()

try:
    render.engine = "BLENDER_WORKBENCH"
except TypeError as error:
    print("engine switch failed:", error)

shading.light = "STUDIO"
shading.color_type = "TEXTURE"
shading.show_object_outline = True
shading.object_outline_color = (0.10, 0.06, 0.04)
render.resolution_x = render.resolution_y = 512
render.resolution_percentage = 100
render.film_transparent = True
render.image_settings.file_format = "PNG"
render.image_settings.color_mode = "RGBA"
scene.camera = camera
render.filepath = OUT

try:
    bpy.ops.render.render(write_still=True)
    print("rendered", OUT)
finally:
    scene.camera = saved["camera"]
    render.engine = saved["engine"]
    render.resolution_x, render.resolution_y, render.resolution_percentage = saved["x"], saved["y"], saved["pct"]
    render.film_transparent = saved["film"]
    render.filepath = saved["path"]
    render.image_settings.file_format = saved["fmt"]
    render.image_settings.color_mode = saved["mode"]
    shading.light, shading.color_type, shading.show_object_outline = saved["light"], saved["color_type"], saved["outline"]
    shading.object_outline_color = saved["outline_color"]
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)
