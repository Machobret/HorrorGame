"""Approved horror-lighting icons for all five guns. No geometry/gameplay changes.

Render previews, then run assemble_horror_icons.py --apply after visual inspection.
"""
from pathlib import Path
import bpy, math
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Weapons/HorrorIcons';OUT.mkdir(exist_ok=True)
MODELS=ROOT/'Assets/Game/Weapons/Models'
DOWNLOAD=Path('C:/Users/Mangis/Downloads/Assets/Guns')
jobs=[
 ('1911',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911.fbx',DOWNLOAD/'PSXMiscGuns/PSXMiscGuns/1911/1911_Texture.png',.087,(-.70,.92),{'1911_Mag_Full','45ACP','45ACP_Shell'}),
 ('DoubleBarrel',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Fbx/DoubleBarrelShotgun.fbx',DOWNLOAD/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Textures/DBShotgunText.png',.25,(-.9,1.62),{'ShellLowPolyGreen.005','ShellLowPolyRed.005'}),
 *[(name,MODELS/name/(name+'.fbx'),None,None,None,set()) for name in ('Revolver','HuntingRifle','FlareGun')]]
for name,source,texture_path,scale,grip,exclude in jobs:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source))
 meshes=[]
 for obj in list(bpy.data.objects):
  if obj.type!='MESH' or obj.name in exclude:bpy.data.objects.remove(obj,do_unlink=True);continue
  if scale:
   matrix=obj.matrix_world.copy()
   for vertex in obj.data.vertices:
    p=matrix@vertex.co;vertex.co=(p.y*scale,-(p.x-grip[0])*scale,(p.z-grip[1])*scale)
   obj.matrix_world=Matrix.Identity(4)
  meshes.append(obj)
 if texture_path:
  mat=bpy.data.materials.new('PixelTexture');mat.use_nodes=True
  node=mat.node_tree.nodes.new('ShaderNodeTexImage')
  node.image=bpy.data.images.load(str(texture_path));node.interpolation='Closest'
  mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
  for obj in meshes:
   obj.data.materials.clear();obj.data.materials.append(mat)
   for polygon in obj.data.polygons:polygon.material_index=0
 for mat in {m for o in meshes for m in o.data.materials}:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE':
    if not texture_path:
     filename=('ScopeText.jpg' if '067' in mat.name else 'HuntingRifleText.png') if name=='HuntingRifle' else ('cylinderRefV2.jpg' if '034' in mat.name else 'ColtPython.png') if name=='Revolver' else 'Flaregun_Text_240.png'
     node.image=bpy.data.images.load(str(MODELS/name/filename),check_existing=True)
    ratio=min(1,128/max(node.image.size));node.image.scale(max(1,round(node.image.size[0]*ratio)),max(1,round(node.image.size[1]*ratio)))
    node.interpolation='Closest'
   elif node.type=='BSDF_PRINCIPLED':
    for link in list(node.inputs['Alpha'].links):mat.node_tree.links.remove(link)
    node.inputs['Alpha'].default_value=1;node.inputs['Metallic'].default_value=.15;node.inputs['Roughness'].default_value=.65
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();meshes=[bpy.context.object];bpy.context.view_layer.update()
 points=[obj.matrix_world@Vector(p) for obj in meshes for p in obj.bound_box]
 center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)])
 radius=max((p-center).length for p in points)
 bpy.ops.object.camera_add(location=center+Vector((2,-1,.35))*radius)
 cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.rotation_euler.rotate_axis('Z',math.radians(45 if name in ('DoubleBarrel','HuntingRifle') else 30))
 projected=[cam.rotation_euler.to_quaternion().inverted()@(p-center) for p in points]
 cam.data.type='ORTHO';cam.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.16
 scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=48
 scene.render.film_transparent=True;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.2
 scene.render.resolution_x=scene.render.resolution_y=1024;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('HorrorStudio');scene.world.color=(.08,.08,.08)
 for location,power,size,color in [((1,-1,3),125,1,(1,.65,.35)),((2,0,.5),45,2,(.75,.85,1)),((1,2,2),230,.7,(.55,.75,1))]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*radius)
  light=bpy.context.object;light.data.energy=power*radius**2;light.data.size=size*radius;light.data.color=color
  light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
 print('HORROR_ICON_OK',name,flush=True)
