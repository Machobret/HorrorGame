"""Render five presentation studies without changing any game assets."""
from pathlib import Path
import bpy, math
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Weapons/IconStudies';OUT.mkdir(exist_ok=True)
DOWNLOAD=Path('C:/Users/Mangis/Downloads/Assets/Guns')
jobs=[
 ('1911',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911.fbx',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911_Texture.png',.087,(-.70,.92),{'1911_Mag_Full','45ACP','45ACP_Shell'}),
 ('DoubleBarrel',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Fbx/DoubleBarrelShotgun.fbx',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Textures/DBShotgunText.png',.25,(-.9,1.62),{'ShellLowPolyGreen.005','ShellLowPolyRed.005'})]
styles=[
 ('01_Profile',(3,-.05,.12),0,0,[( (1,-1,3),180,3,(1,1,1)),((-2,1,1),90,3,(1,1,1))]),
 ('02_Classic',(2,-.7,.6),22,0,[( (1,-1,3),170,2,(1,.93,.83)),((-2,1,1),100,2,(.8,.9,1)),((1,2,2),100,1,(1,1,1))]),
 ('03_Forensic',(2,-.3,1.6),35,-.1,[( (1,-1,3),200,2,(.7,.85,1)),((-2,1,1),60,2,(.65,.8,1)),((1,2,2),160,.8,(.85,.95,1))]),
 ('04_Horror',(2,-1,.35),30,-.2,[( (1,-1,3),125,1,(1,.65,.35)),((2,0,.5),45,2,(.75,.85,1)),((1,2,2),230,.7,(.55,.75,1))]),
 ('05_Retro',(3,-.25,.3),18,.2,[( (1,-1,3),210,1,(1,.96,.88)),((-2,1,1),70,2,(.8,.87,1))])]
for name,source,texture_path,scale,grip,exclude in jobs:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source))
 meshes=[]
 for obj in list(bpy.data.objects):
  if obj.type!='MESH' or obj.name in exclude:bpy.data.objects.remove(obj,do_unlink=True);continue
  matrix=obj.matrix_world.copy()
  for vertex in obj.data.vertices:
   p=matrix@vertex.co;vertex.co=(p.y*scale,-(p.x-grip[0])*scale,(p.z-grip[1])*scale)
  obj.matrix_world=Matrix.Identity(4);meshes.append(obj)
 image=bpy.data.images.load(str(texture_path));ratio=128/max(image.size);image.scale(max(1,round(image.size[0]*ratio)),max(1,round(image.size[1]*ratio)))
 mat=bpy.data.materials.new('PixelTexture');mat.use_nodes=True
 node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.interpolation='Closest'
 bsdf=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
 bsdf.inputs['Roughness'].default_value=.65;bsdf.inputs['Metallic'].default_value=.15
 for obj in meshes:
  obj.data.materials.clear();obj.data.materials.append(mat)
  for polygon in obj.data.polygons:polygon.material_index=0
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();meshes=[bpy.context.object]
 bpy.context.view_layer.update()
 points=[obj.matrix_world@Vector(p) for obj in meshes for p in obj.bound_box]
 center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)])
 radius=max((p-center).length for p in points)
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
 scene.render.film_transparent=True;scene.view_settings.view_transform='AgX'
 scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.08,.08,.08)
 for style,direction,roll,exposure,lights in styles:
  for obj in list(bpy.data.objects):
   if obj.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(obj,do_unlink=True)
  bpy.ops.object.camera_add(location=center+Vector(direction)*radius)
  cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  cam.rotation_euler.rotate_axis('Z',math.radians(roll if name=='1911' else (0 if style=='01_Profile' else roll+15)))
  projected=[cam.rotation_euler.to_quaternion().inverted()@(p-center) for p in points]
  cam.data.type='ORTHO';cam.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.16
  scene.camera=cam;scene.view_settings.exposure=exposure
  scene.render.resolution_x=scene.render.resolution_y=128 if style=='05_Retro' else 512
  scene.render.resolution_percentage=100
  for location,power,size,color in lights:
   bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*radius)
   light=bpy.context.object;light.data.energy=power*radius**2;light.data.size=size*radius;light.data.color=color
   light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
  scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/(style+'_'+name+'.png'))
  bpy.ops.render.render(write_still=True)
  print('ICON_STUDY_OK',name,style,flush=True)

