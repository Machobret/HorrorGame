"""Create a self-contained editable Blender copy of the game's current flare gun."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
ASSET=ROOT/'Assets/Game/Weapons/FlareGun/Models'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ASSET/'FlareGun.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert meshes, 'No model imported'
image=bpy.data.images.load(str(ASSET/'Flaregun_Text_240.png')); image.pack()
for o in meshes:
 for slot in o.material_slots:
  mat=slot.material
  if not mat:continue
  mat.use_nodes=True; nodes=mat.node_tree.nodes
  bs=nodes.get('Principled BSDF')
  if bs:
   tex=nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
   mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
   bs.inputs['Roughness'].default_value=.75
points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3))); hi=Vector(tuple(max(p[i] for p in points) for i in range(3))); center=(lo+hi)/2
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.type='MATERIAL'
   area.spaces.active.region_3d.view_location=center
   area.spaces.active.region_3d.view_distance=(hi-lo).length*1.6
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('FlareGun.blend')))
assert image.packed_file
print('FLARE_BLEND_OK',len(meshes),'mesh objects; packed texture;',tuple(hi-lo))
