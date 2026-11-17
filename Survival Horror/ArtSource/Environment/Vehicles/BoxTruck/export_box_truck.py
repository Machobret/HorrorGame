"""Export the modeled truck; preserve each material's authored UV channel."""
import bpy
import json
from pathlib import Path

project = Path(__file__).resolve().parents[4]
source = Path(__file__).with_name('BoxTruck.blend')
output = project / 'Assets/Game/Environment/Vehicles/BoxTruck_Model.fbx'
bpy.ops.wm.open_mainfile(filepath=str(source))
truck = bpy.data.objects['Cars_Car8']
assert len(truck.data.materials) == 2
assert truck.data.materials[1].name == 'TruckRandom'
mesh = truck.data
cab_uv = mesh.uv_layers['AtlasUV']
cargo_uv = mesh.uv_layers['UVMap']
uvs = [tuple((cargo_uv if p.material_index == 1 else cab_uv).data[i].uv)
       for p in mesh.polygons for i in p.loop_indices]
assert len(uvs) == len(mesh.loops)
# FBX primary UVs must contain the channel used by each material. Source .blend is untouched.
for uv in list(mesh.uv_layers):
    mesh.uv_layers.remove(uv)
primary = mesh.uv_layers.new(name='TruckUV')
for index, uv in enumerate(uvs):
    primary.data[index].uv = uv
primary.active_render = True
truck.name = 'BoxTruck'
mesh.name = 'BoxTruck'
bpy.ops.object.select_all(action='DESELECT')
truck.hide_set(False)
truck.select_set(True)
bpy.context.view_layer.objects.active = truck
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={'MESH'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                         use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP', use_custom_props=False)
report = project / 'Exports/Reviews/BoxTruck/Source.json'
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({'vertices': len(mesh.vertices), 'faces': len(mesh.polygons),
                              'triangles': sum(len(p.vertices)-2 for p in mesh.polygons),
                              'uvs': [{'x': u, 'y': v} for u, v in uvs],
                              'material_faces': [p.material_index for p in mesh.polygons]}, indent=2))
print('BOX_TRUCK_EXPORTED', output)
