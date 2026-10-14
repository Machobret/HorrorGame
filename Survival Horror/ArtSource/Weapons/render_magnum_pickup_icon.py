"""Render the fixed carton as a fresh pickup/inventory icon; leave geometry alone."""
from pathlib import Path
import bpy,math,shutil,re,uuid
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];folder=ROOT/'Assets/Game/Weapons/Models/RevolverAmmo'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(folder/'RevolverAmmo.fbx'))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
for mat in {m for o in meshes for m in o.data.materials}:
 bsdf=mat.node_tree.nodes.get('Principled BSDF')
 for link in list(bsdf.inputs['Alpha'].links):mat.node_tree.links.remove(link)
 bsdf.inputs['Alpha'].default_value=1;bsdf.inputs['Roughness'].default_value=.85;bsdf.inputs['Metallic'].default_value=0
 if 'Carton' in mat.name:
  for link in list(bsdf.inputs['Base Color'].links):mat.node_tree.links.remove(link)
  bsdf.inputs['Base Color'].default_value=(.44,.30,.21,1)
 else:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE':node.image=bpy.data.images.load(str(folder/'RevolverAmmo_Albedo.png'),check_existing=True);node.interpolation='Closest'
points=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)])
radius=max((p-center).length for p in points)
bpy.ops.object.camera_add(location=center+Vector((.9,-1.5,2.8))*radius)
camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
projected=[camera.rotation_euler.to_quaternion().inverted()@(p-center) for p in points]
camera.data.type='ORTHO';camera.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.16
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=scene.render.resolution_y=1024;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.1
scene.world=bpy.data.worlds.new('Horror studio');scene.world.color=(.08,.08,.08)
for location,power,size,color in [((1,-1,3),160,2,(1,.8,.6)),((2,0,.5),70,2,(.8,.9,1)),((1,2,2),200,.7,(.65,.8,1))]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*radius)
 lamp=bpy.context.object;lamp.data.energy=power*radius**2;lamp.data.size=size*radius;lamp.data.color=color
 lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
target=folder/'RevolverAmmo_PickupIcon.png'
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(target);bpy.ops.render.render(write_still=True)
shutil.copy2(target,folder/'RevolverAmmo_Icon.png')
meta=Path(str(target)+'.meta')
text=meta.read_text() if meta.exists() else re.sub(r'^guid: \w+','guid: '+uuid.uuid4().hex,(folder/'RevolverAmmo_Icon.png.meta').read_text(),flags=re.M)
text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',text);text=re.sub(r'textureCompression: \d+','textureCompression: 0',text)
meta.write_text(text)
guid=re.search(r'^guid: (\w+)',text,re.M)[1]
data=ROOT/'Assets/Game/Weapons/Data/RevolverAmmo.asset';text=data.read_text()
text=re.sub(r'Image: \{[^}]+\}','Image: {fileID: 21300000, guid: '+guid+', type: 3}',text);data.write_text(text)
print('MAGNUM_PICKUP_ICON_UPDATED',guid)
