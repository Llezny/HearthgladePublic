"""Prints the dimensions and bone count of FBX files, to size new props against existing ones.

Headless:  blender.exe -b --python inspect_fbx.py -- file1.fbx file2.fbx ...
"""
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for path in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    print("FILE", path)
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            extra = ""
            if obj.type == "ARMATURE":
                extra = " bones=%d" % len(obj.data.bones)
            print("  ", obj.type, obj.name, "dims", tuple(round(v, 3) for v in obj.dimensions), "loc", tuple(round(v, 3) for v in obj.location),
                  "scale", tuple(round(v, 3) for v in obj.scale), extra)
