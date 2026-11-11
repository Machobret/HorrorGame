"""Import the user's Survival PSX Zippo; retain runtime GUIDs and native item setup."""
import bpy,shutil
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]; OUT=ROOT/'Assets/Game/Items/Lighter'; SRC=Path(__file__).parent
original=SRC/'SurvivalSource';original.mkdir(exist_ok=True)
for a,b in [(Path(r'C:/Users/Mangis/Downloads/Models_Separated/Survival/zippo.fbx'),original/'zippo.fbx'),(Path(r'C:/Users/Mangis/Downloads/Models_Separated/Survival/Textures/zippo.png'),original/'zippo.png')]:
 if not b.exists():shutil.copy2(a,b)
shutil.copy2(original/'zippo.png',OUT/'Lighter_Albedo.png')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(original/'zippo.fbx'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.join();obj=bpy.context.object;obj.name='Lighter'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Modelled in oversized units; retain the authored open lid while bringing body width to 3.85 cm.
coords=[obj.matrix_world@Vector(c) for c in obj.bound_box]
lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)));hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
scale=.0385/.113121185; center=(lo+hi)/2
for v in obj.data.vertices:v.co=(v.co-center)*scale
obj.location=(0,0,0)
mat=bpy.data.materials.new('Lighter_SurvivalMetal');mat.use_nodes=True
image=bpy.data.images.load(str(OUT/'Lighter_Albedo.png'));image.pack()
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest'
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.72
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
obj.data.materials.clear();obj.data.materials.append(mat)
for p in obj.data.polygons:p.material_index=0
bpy.ops.export_scene.fbx(filepath=str(OUT/'Lighter.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.world.color=(.12,.12,.12)
def aim(o):o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.09,-.18,.075));cam=bpy.context.object;aim(cam);cam.data.type='ORTHO';cam.data.ortho_scale=.105;scene.camera=cam
for name,loc,power,color,size in [('Key',(-.12,-.18,.2),2.8,(1,.87,.68),.18),('Fill',(.15,-.08,.08),1.5,(.68,.79,1),.15),('Rim',(.05,.08,.15),2,(1,.45,.3),.1)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
 light=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(light);light.location=loc;aim(light)
scene.view_settings.view_transform='AgX';scene.render.filepath=str(OUT/'Lighter_PickupIcon.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Lighter.blend'))
bpy.ops.render.render(write_still=True)
print('SURVIVAL_ZIPPO_OK',len(obj.data.vertices),'vertices',len(obj.data.polygons),'faces',tuple(obj.dimensions))
