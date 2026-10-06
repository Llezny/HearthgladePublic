"""Low-poly red mushroom for Hearthglade (budget: 100 triangles, one shared palette texture).

Run headless:  blender -b --python mushroom_red.py -- <out.fbx> [preview.png]

Units are metres, Z up while modelling (the FBX export converts to Unity's Y up), origin at the bottom of the stem.
Colours come from the project's shared palette (Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): a 32x32
image made of 4x4 pixel cells, so every face gets a flat UV at the centre of one cell and the model needs no texture
of its own (same material as the tree pack, so it batches with it).
"""
import math
import sys

import bmesh
import bpy
from mathutils import Vector

TRI_BUDGET = 100
SEGMENTS = 8
PALETTE_PNG = "E:/Repos/Hearthglade/Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png"

# Palette cells as (row, column) counted from the TOP-left of the image.
CELL_CAP = (4, 2)      # 9F3F3F red
CELL_SPOT = (3, 0)     # FFF9EF cream white
CELL_STEM = (0, 3)     # D3B396 light tan
CELL_GILLS = (0, 4)    # BEB296 pale tan

# Cap profile as (radius, height) from the rim up to the crown; the apex sits above the last ring.
PROFILE = [(0.110, 0.120), (0.095, 0.160), (0.055, 0.200)]
APEX_Z = 0.215
STEM_BASE = (0.038, 0.0)
STEM_TOP = (0.028, 0.115)

# White spots: (profile segment, angle in degrees, half size in metres).
SPOTS = [
    (1, 20, 0.015), (1, 140, 0.013), (1, 262, 0.014),
    (0, 80, 0.014), (0, 200, 0.016), (0, 322, 0.013),
]


def cell_uv(cell, size=32, cell_px=4):
    row, col = cell
    return ((col * cell_px + cell_px / 2) / size, 1.0 - (row * cell_px + cell_px / 2) / size)


def ring(bm, radius_z):
    radius, z = radius_z
    return [bm.verts.new((radius * math.cos(2 * math.pi * i / SEGMENTS), radius * math.sin(2 * math.pi * i / SEGMENTS), z))
            for i in range(SEGMENTS)]


def build_mesh():
    mesh = bpy.data.meshes.new("MushroomRed")
    bm = bmesh.new()
    faces = []  # (face, palette cell, group)

    def band(lower, upper, cell, group):
        for i in range(SEGMENTS):
            j = (i + 1) % SEGMENTS
            faces.append((bm.faces.new([lower[i], lower[j], upper[j], upper[i]]), cell, group))

    def fan(base, top, cell, group):
        for i in range(SEGMENTS):
            j = (i + 1) % SEGMENTS
            faces.append((bm.faces.new([base[i], base[j], top]), cell, group))

    stem_base, stem_top = ring(bm, STEM_BASE), ring(bm, STEM_TOP)
    cap_rings = [ring(bm, p) for p in PROFILE]
    apex = bm.verts.new((0.0, 0.0, APEX_Z))

    band(stem_base, stem_top, CELL_STEM, "stem")
    band(stem_top, cap_rings[0], CELL_GILLS, "under")
    band(cap_rings[0], cap_rings[1], CELL_CAP, "cap")
    band(cap_rings[1], cap_rings[2], CELL_CAP, "cap")
    fan(cap_rings[2], apex, CELL_CAP, "cap")

    # Spots: small diamonds lying just above the cap surface, facing outwards.
    for segment, degrees, size in SPOTS:
        (r0, z0), (r1, z1) = PROFILE[segment], PROFILE[segment + 1]
        angle = math.radians(degrees)
        radial = Vector((math.cos(angle), math.sin(angle), 0.0))
        around = Vector((-math.sin(angle), math.cos(angle), 0.0))
        along = (radial * (r1 - r0) + Vector((0.0, 0.0, z1 - z0))).normalized()
        normal = along.cross(around).normalized()
        if normal.dot(radial + Vector((0.0, 0.0, 0.5))) < 0:
            normal = -normal
        centre = radial * ((r0 + r1) / 2) + Vector((0.0, 0.0, (z0 + z1) / 2)) + normal * 0.003
        corners = [bm.verts.new(c) for c in (centre + along * size, centre + around * size,
                                              centre - along * size, centre - around * size)]
        faces.append((bm.faces.new([corners[0], corners[1], corners[2]]), CELL_SPOT, "spot"))
        faces.append((bm.faces.new([corners[0], corners[2], corners[3]]), CELL_SPOT, "spot"))

    bm.faces.ensure_lookup_table()
    bm.normal_update()

    # Winding: every face must point away from the mushroom (downwards for the underside).
    middle = Vector((0.0, 0.0, 0.11))
    for face, _cell, group in faces:
        centre = face.calc_center_median()
        if group == "under":
            outward = face.normal.z < 0
        elif group == "stem":
            outward = face.normal.x * centre.x + face.normal.y * centre.y > 0
        else:
            outward = face.normal.dot(centre - middle) > 0
        if not outward:
            face.normal_flip()

    uv_layer = bm.loops.layers.uv.new("UVMap")
    for face, cell, _group in faces:
        u, v = cell_uv(cell)
        for loop in face.loops:
            loop[uv_layer].uv = (u, v)
        face.smooth = False

    bm.to_mesh(mesh)
    bm.free()
    return mesh


def make_material():
    material = bpy.data.materials.new("palette")
    material.use_nodes = True
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(PALETTE_PNG)
    texture.interpolation = "Closest"
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is not None:
        material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    return material


def verify(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    triangles = len(mesh.loop_triangles)
    print("MUSHROOM tris", triangles, "verts", len(mesh.vertices), "dims", tuple(round(v, 3) for v in obj.dimensions),
          "min_z", round(min(v.co.z for v in mesh.vertices), 4))
    assert triangles <= TRI_BUDGET, f"{triangles} triangles exceed the budget of {TRI_BUDGET}"
    inward = sum(1 for p in mesh.polygons if p.center.z > 0.12 and p.normal.dot(p.center - Vector((0.0, 0.0, 0.11))) < 0)
    assert inward == 0, f"{inward} cap faces point inwards"


def render_preview(path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = scene.render.resolution_y = 640
    scene.world = scene.world or bpy.data.worlds.new("world")
    scene.world.color = (0.55, 0.7, 0.85)
    camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    scene.collection.objects.link(camera)
    camera.location = (0.42, -0.42, 0.34)
    camera.rotation_euler = (Vector((0.0, 0.0, 0.11)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_fbx = args[0] if args else None
    preview = args[1] if len(args) > 1 else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    obj = bpy.data.objects.new("MushroomRed", build_mesh())
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(make_material())
    verify(obj)

    if preview:
        render_preview(preview)
    if out_fbx:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.fbx(
            filepath=out_fbx,
            use_selection=True,
            apply_scale_options="FBX_SCALE_UNITS",
            global_scale=1.0,
            axis_forward="-Z",
            axis_up="Y",
            bake_space_transform=True,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
        )
        print("MUSHROOM exported", out_fbx)


if __name__ == "__main__":
    main()
