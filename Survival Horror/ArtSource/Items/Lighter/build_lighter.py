"""Build original PSX lighter atlas, model and icon. Run with Blender --background --python."""
import bpy, random, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Game/Items/Lighter'; OUT.mkdir(parents=True,exist_ok=True)
# Pixel atlas created inside Blender to keep regeneration dependency-free.
rng=random.Random(51); size=128; pixels=[]
for y in range(size):
 for x in range(size):
  tile=(x//64)+(y//64)*2
  base=[(.33,.055,.035),(.46,.35,.19),(.12,.105,.085),(.42,.07,.045)][tile]
  n=rng.uniform(-.022,.022); c=[max(0,min(1,v+n)) for v in base]
  if rng.random()<.025: c=[v*.58 for v in c]
  if tile in (0,3) and (x%64<3 or x%64>60): c=[.53,.4,.24]
  if tile in (0,3) and rng.random()<.018: c=[.5,.35,.18]
  pixels.extend((*c,1))
# Ember diamond emblem, engraved lines and scratches in the enamel tile.
def dot(x,y,c):
 i=(y*size+x)*4; pixels[i:i+4]=[*c,1]
for y in range(23,43):
 for x in range(22,42):
  d=abs(x-32)+abs(y-33)
  if 8<=d<=10: dot(x,y,(.68,.53,.32))
for y in range(28,39):
 for x in range(29,36):
  if abs(x-32)<(y-27)//3+1: dot(x,y,(.69,.51,.26))
for y in (15,17):
 for x in range(19,46): dot(x,y,(.58,.43,.24))
for k in range(27):
 x=rng.randrange(6,57); y=rng.randrange(5,60)
 for dx in range(rng.randrange(1,5)): dot(min(x+dx,62),y,(.52,.4,.26))
im=bpy.data.images.new('Lighter_Albedo',width=size,height=size); im.pixels=pixels
im.filepath_raw=str(OUT/'Lighter_Albedo.png'); im.file_format='PNG'; im.save()
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('Lighter_Enamel'); mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.68; bs.inputs['Metallic'].default_value=.25
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=im; tex.interpolation='Closest'; mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
parts=[]
def box(name,loc,dims,tile,bevel=.001):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name=name; o.dimensions=dims
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  m=o.modifiers.new('Soft metal corners','BEVEL'); m.width=bevel; m.segments=2
  bpy.ops.object.modifier_apply(modifier=m.name)
 # Each face gets the whole same tile, covering all sides and bevels.
 uv=o.data.uv_layers.active
 for p in o.data.polygons:
  normal=p.normal; axis=max(range(3),key=lambda i:abs(normal[i])); axes=[i for i in range(3) if i!=axis]
  for li in p.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co
   a=co[axes[0]]/dims[axes[0]]+.5; b=co[axes[1]]/dims[axes[1]]+.5
   uv.data[li].uv=((tile%2)*.5+.015+a*.47,(tile//2)*.5+.015+b*.47)
 o.data.materials.append(mat); parts.append(o); return o
box('Lower brass case',(0,0,-.009),(.04,.014,.045),1,.002)
box('Red enamel body',(0,-.0001,-.009),(.039,.0142,.0415),0,.0018)
box('Lid seam',(0,0,.014),(.039,.0138,.0008),2,.0002)
box('Flip top brass lid',(0,0,.022),(.04,.014,.015),1,.0018)
box('Red enamel lid',(0,-.0001,.0225),(.039,.0142,.0125),3,.0014)
# Side hinge visible from three-quarter view, segmented pin ends.
for z in (.010,.014,.018): box('Hinge barrel',(.0202,.002,z),(.0038,.006,.0035),1,.0008)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); obj=bpy.context.object; obj.name='Lighter'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Lighter.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.film_transparent=True; scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGBA'
scene.world.color=(.12,.12,.12)
def aim(o):o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.09,-.18,.075)); cam=bpy.context.object; aim(cam); cam.data.type='ORTHO'; cam.data.ortho_scale=.084; scene.camera=cam
for name,loc,power,color,size in [('Key',(-.12,-.18,.2),2.8,(1,.87,.68),.18),('Fill',(.15,-.08,.08),1.5,(.68,.79,1),.15),('Rim',(.05,.08,.15),2,(1,.45,.3),.1)]:
 data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.color=color; data.shape='DISK'; data.size=size
 light=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(light); light.location=loc; aim(light)
scene.view_settings.view_transform='AgX'; scene.render.filepath=str(OUT/'Lighter_PickupIcon.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('Lighter.blend')))
bpy.ops.render.render(write_still=True)
print('LIGHTER_MODEL_OK',len(obj.data.polygons),'polygons')
