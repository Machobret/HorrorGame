"""Replace template geometry while retaining its FBX, material, item and icon GUIDs."""
from pathlib import Path
import bpy, shutil, json, math, sys
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
DOWNLOAD=Path('C:/Users/Mangis/Downloads/Assets/Guns')
ART=ROOT/'ArtSource/Weapons/TemplateReplacements';ART.mkdir(exist_ok=True)
jobs=[
 ('Gun',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911.fbx',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911_Texture.png',.087,(-.70,.92),{'1911_Mag_Full','45ACP','45ACP_Shell'},.01,-1,(1.233,1.61)),
 ('Shotgun',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Fbx/DoubleBarrelShotgun.fbx',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Textures/DBShotgunText.png',.25,(-.9,1.62),{'ShellLowPolyGreen.005','ShellLowPolyRed.005'},.19875449,1,(1.582,1.81))
]
report={}
for name,source,texture_path,scale,grip,exclude,held_scale,forward_sign,muzzle in jobs:
 folder=ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Weapons'/name
 for filename in (name+'.fbx',name+'.png',name+'.mat'):
  backup=ART/(name+'_Original_'+filename)
  if not backup.exists():shutil.copy2(folder/filename,backup)
 shutil.copy2(texture_path,folder/(name+'_PSX_Albedo.png'))
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source))
 meshes=[]
 for obj in list(bpy.data.objects):
  if obj.type!='MESH' or obj.name in exclude:bpy.data.objects.remove(obj,do_unlink=True);continue
  matrix=obj.matrix_world.copy()
  for vertex in obj.data.vertices:
   p=matrix@vertex.co;vertex.co=(p.y*scale,-(p.x-grip[0])*scale,(p.z-grip[1])*scale)
  obj.matrix_world=Matrix.Identity(4);meshes.append(obj)
 image=bpy.data.images.load(str(texture_path));ratio=128/max(image.size);image.scale(max(1,round(image.size[0]*ratio)),max(1,round(image.size[1]*ratio)))
 mat=bpy.data.materials.new('TemplateWeapon');mat.use_nodes=True
 node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.interpolation='Closest'
 bsdf=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
 bsdf.inputs['Roughness'].default_value=.65;bsdf.inputs['Metallic'].default_value=.15
 for obj in meshes:
  obj.data.materials.clear();obj.data.materials.append(mat)
  for polygon in obj.data.polygons:polygon.material_index=0
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name;obj.data.name=name
 points=[Vector(p) for p in obj.bound_box]
 center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)])
 radius=max((p-center).length for p in points)
 bpy.ops.object.camera_add(location=center+Vector((2,-1,.35))*radius)
 cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.rotation_euler.rotate_axis('Z',math.radians(45 if name=='Shotgun' else 30))
 projected=[cam.rotation_euler.to_quaternion().inverted()@(p-center) for p in points]
 cam.data.type='ORTHO';cam.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.12
 scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=48
 scene.render.resolution_x=scene.render.resolution_y=1024;scene.render.resolution_percentage=100
 scene.render.film_transparent=True;scene.view_settings.view_transform='AgX'
 scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.08,.08,.08);scene.view_settings.exposure=-.2
 for location,power,size,color in [((1,-1,3),125,1,(1,.65,.35)),((2,0,.5),45,2,(.75,.85,1)),((1,2,2),230,.7,(.55,.75,1))]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*radius)
  light=bpy.context.object;light.data.energy=power*radius**2;light.data.size=radius*size;light.data.color=color
  light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/(name+'.png'));bpy.ops.render.render(write_still=True) if '--export-only' not in sys.argv else None
 # Keep the coordinate conventions expected by the existing prefab sockets.
 vertices=[v.co.copy() for v in obj.data.vertices]
 for vertex,p in zip(obj.data.vertices,vertices):vertex.co=(p.y*forward_sign/held_scale,-p.x/held_scale,p.z/held_scale)
 if forward_sign<0:obj.data.flip_normals()
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,path_mode='STRIP')
 report[name]={'held_scale':held_scale,'size':[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)],'center':[float(v) for v in center],'muzzle_local':[forward_sign*(muzzle[0]-grip[0])*scale/held_scale,(muzzle[1]-grip[1])*scale/held_scale,0], 'exclude':sorted(exclude)}
 print('TEMPLATE_REPLACEMENT_BUILT',name)
(ART/'geometry.json').write_text(json.dumps(report,indent=2))


