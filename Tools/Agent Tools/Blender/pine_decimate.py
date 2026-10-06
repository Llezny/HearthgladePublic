"""Rebuilds Pine.fbx from the existing hand-authored conifer mesh, 10% fewer triangles.

The tree the user likes ("PineTree" prefab, used in Taiga/Tundra) is historically misnamed
Assets/Arts/Models/Environment/Trees/SpruceTree.fbx - a real 3D branch mesh (not a billboard), 1550
tris, own bark/needle texture (material "_trees_normal" via the FBX, not the shared flat palette).
This script just decimates it 10% and re-exports as Pine.fbx so the biome resource lists can point at
a plainly-named, lighter file; the mesh's own material/UVs are left untouched.

Run through the Blender MCP (execute_blender_code) or paste into Blender's Scripting tab.
"""
import bpy

SOURCE = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Trees/SpruceTree.fbx"
OUTPUT = "E:/Repos/Hearthglade/Assets/Arts/Models/Environment/Trees/Pine.fbx"
DECIMATE_RATIO = 0.9  # keep 90% of faces => ~10% fewer triangles

before = set(o.name for o in bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SOURCE)
after = set(o.name for o in bpy.data.objects)
obj = bpy.data.objects[next(iter(after - before))]
obj.name = "Pine"
obj.data.name = "Pine"

bpy.context.view_layer.objects.active = obj
obj.select_set(True)
mod = obj.modifiers.new("Decimate", "DECIMATE")
mod.ratio = DECIMATE_RATIO
bpy.ops.object.modifier_apply(modifier=mod.name)
obj.data.calc_loop_triangles()
print("tris after decimate:", len(obj.data.loop_triangles), "verts:", len(obj.data.vertices))

bpy.ops.export_scene.fbx(
    filepath=OUTPUT,
    use_selection=True,
    apply_scale_options="FBX_SCALE_UNITS",
    global_scale=1.0,
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
)
print("exported", OUTPUT)
