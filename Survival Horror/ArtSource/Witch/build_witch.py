"""Recreate the editable Blender scene and preview from the validated game mesh."""
import bpy, os, math, json
from mathutils import Vector
SRC=os.path.dirname(__file__);ROOT=os.path.abspath(SRC+'/../..');OUT=ROOT+'/Assets/Game/Enemies/Resources/Witch'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=OUT+'/Witch_Model.fbx')
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
body=next(o for o in bpy.data.objects if o.type=='MESH')
for action in bpy.data.actions:action.use_fake_user=True
idle=next(a for a in bpy.data.actions if a.name.endswith('Idle'))
arm.animation_data_create();arm.animation_data.action=idle
scene=bpy.context.scene;scene.frame_set(1)
material=bpy.data.materials.new('Witch_Skin_Preview');material.use_nodes=True
bsdf=material.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=1
tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(OUT+'/Witch_Skin.png');tex.interpolation='Closest'
material.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
body.data.materials.clear();body.data.materials.append(material)
for p in body.data.polygons:p.material_index=0
scene.render.engine='CYCLES';scene.cycles.samples=20
scene.world=bpy.data.worlds.new('World');scene.world.color=(.045,.045,.045)
def aim(o,point):o.rotation_euler=(Vector(point)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3.4,-6,2.6));cam=bpy.context.object;aim(cam,(0,0,1.15));cam.data.type='ORTHO';cam.data.ortho_scale=2.85;scene.camera=cam
for loc,power,size in [((2,-4,5),330,4),((-3,-2,3),180,3),((1,3,4),330,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.data.energy=power;light.data.size=size;aim(light,(0,0,1.3))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));floor=bpy.context.object
fm=bpy.data.materials.new('Backdrop');fm.diffuse_color=(.025,.028,.035,1);floor.data.materials.append(fm)
scene.render.resolution_x=850;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=SRC+'/Witch_Preview.png'
tex.image.pack()
bpy.ops.wm.save_as_mainfile(filepath=SRC+'/Witch.blend')
bpy.ops.render.render(write_still=True)
print('WITCH_SOURCE_RESTORED',len(body.data.polygons),'triangles;',len(arm.data.bones),'bones;',len(bpy.data.actions),'clips')
