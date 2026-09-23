import bpy, math, random, json, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
ROOT=r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror'
OUT=ROOT+'/Assets/Game/Enemies/Resources/Brute'
SRC=ROOT+'/ArtSource/Brute'
random.seed(31)
bpy.ops.wm.read_factory_settings(use_empty=True)
# A small, nearest-filtered colour atlas shared by every part of the creature.
img=bpy.data.images.load(SRC+'/Brute_Flesh_Atlas_Source.png',check_existing=False)
img.name='Brute_PSX_Atlas'
img.scale(256,256)
img.filepath_raw=OUT+'/Brute_PSX_Atlas.png';img.file_format='PNG';img.save()
mat=bpy.data.materials.new('Brute_PSX_Skin');mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=1
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest';shade=mat.node_tree.nodes.new('ShaderNodeMixRGB');shade.blend_type='MULTIPLY';shade.inputs[0].default_value=1;shade.inputs[2].default_value=(.58,.58,.58,1)
mat.node_tree.links.new(tex.outputs['Color'],shade.inputs[1]);mat.node_tree.links.new(shade.outputs[0],bsdf.inputs['Base Color'])
cloth=bpy.data.images.new('Brute_Pants_Cloth',width=64,height=64,alpha=True)
clothpixels=[]
for y in range(64):
 for x in range(64):
  n=random.uniform(-.012,.012)+(.009 if (x+y)%2 else -.009)
  clothpixels.extend([.105+n,.115+n,.105+n,1])
cloth.pixels=clothpixels;cloth.filepath_raw=OUT+'/Brute_Pants_Cloth.png';cloth.file_format='PNG';cloth.save()
pantsmat=bpy.data.materials.new('Brute_Pants');pantsmat.use_nodes=True
pnode=pantsmat.node_tree.nodes.get('Principled BSDF');pnode.inputs['Roughness'].default_value=1
pt=pantsmat.node_tree.nodes.new('ShaderNodeTexImage');pt.image=cloth;pt.interpolation='Closest';pantsmat.node_tree.links.new(pt.outputs['Color'],pnode.inputs['Base Color'])
# Game-ready deform skeleton (Z up, face towards -Y). A hunched rest shape is applied below.
bones={
 'pelvis':((0,0,.95),(0,0,1.2),None),
 'spine':((0,0,1.2),(0,0,1.65),'pelvis'),
 'chest':((0,0,1.65),(0,0,2.12),'spine'),
 'neck':((0,0,2.12),(0,0,2.3),'chest'),
 'head':((0,0,2.3),(0,0,2.7),'neck')}
for side,sign in [('L',1),('R',-1)]:
 bones.update({
  'upper_arm.'+side:((sign*.68,0,2.02),(sign*.9,0,1.57),'chest'),
  'forearm.'+side:((sign*.9,0,1.57),(sign*1.01,-.06,1.15),'upper_arm.'+side),
  'hand.'+side:((sign*1.01,-.06,1.15),(sign*1.04,-.12,.97),'forearm.'+side),
  'thigh.'+side:((sign*.35,0,.98),(sign*.39,0,.52),'pelvis'),
  'shin.'+side:((sign*.39,0,.52),(sign*.4,0,.13),'thigh.'+side),
  'foot.'+side:((sign*.4,0,.13),(sign*.4,-.3,.1),'shin.'+side)})
armdata=bpy.data.armatures.new('Brute_Rig');arm=bpy.data.objects.new('BruteRig',armdata);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,(head,tail,parent) in bones.items():
 b=armdata.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');arm.select_set(False)
parts=[]
def finish(o,name,tile,weights):
 o.name=name
 bpy.context.view_layer.objects.active=o
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 # Vertex coordinates in rig space make export and skinning unambiguous.
 for v in o.data.vertices:v.co=o.matrix_world@v.co
 o.matrix_world.identity()
 if not o.data.uv_layers:o.data.uv_layers.new()
 for uv in o.data.uv_layers.active.data:
  uv.uv.x=(tile%4+(0.06+uv.uv.x*.88))/4
  uv.uv.y=(tile//4+(0.03+uv.uv.y*.94))/2
 for name in bones:o.vertex_groups.new(name=name)
 for v in o.data.vertices:
  w=weights(v.co) if callable(weights) else weights
  for bn,value in w.items():
   if value>0:o.vertex_groups[bn].add([v.index],value,'REPLACE')
 o.data.materials.append(mat)
 for face in o.data.polygons:face.use_smooth=False
 parts.append(o);return o
def ball(name,loc,scale,tile=0,weights=None,segments=8,rings=5):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=loc)
 o=bpy.context.object;o.scale=scale
 return finish(o,name,tile,weights or {'head':1})
def torso_weights(v):
 if v.z<1.2:return {'pelvis':.7,'spine':.3}
 if v.z<1.65:
  t=(v.z-1.2)/.45;return {'spine':1-t*.65,'chest':t*.65}
 return {'chest':1}
# Continuous ring-built torso with the weight concentrated in a hanging belly.
levels=[(.87,.35,.26,-.04),(1.0,.62,.4,-.15),(1.2,.82,.61,-.22),(1.45,.86,.64,-.23),(1.7,.78,.49,-.09),(1.94,.71,.36,0),(2.1,.52,.3,0),(2.2,.24,.21,0)]
verts=[];faces=[];N=14
for z,rx,ry,cy in levels:
 for i in range(N):
  a=2*math.pi*i/N;verts.append((rx*math.cos(a),cy+ry*math.sin(a),z))
for j in range(len(levels)-1):
 for i in range(N):faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
faces.extend([tuple(reversed(range(N))),tuple((len(levels)-1)*N+i for i in range(N))])
mesh=bpy.data.meshes.new('Belly_Topology');mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('Heavy hanging belly',mesh);bpy.context.collection.objects.link(o)
uv=mesh.uv_layers.new()
for poly in mesh.polygons:
 for li in poly.loop_indices:
  vi=mesh.loops[li].vertex_index;uv.data[li].uv=(vi%N/N,vi//N/(len(levels)-1))
finish(o,'Heavy hanging belly',0,torso_weights)
ball('Lower belly fold',(0,-.48,1.01),(.51,.24,.18),7,{'pelvis':.5,'spine':.5})
ball('Navel',(0,-.864,1.4),(.065,.014,.048),2,{'spine':.7,'chest':.3},8,4)
for side,sign in [('L',1),('R',-1)]:
 # A single continuous shoulder-to-palm surface replaces the stacked ellipsoids.
 armrings=[(2.18,.62,.00,.19,.22),(2.06,.70,.00,.29,.29),(1.90,.78,.00,.285,.275),(1.73,.85,.00,.26,.255),(1.60,.90,-.01,.235,.23),(1.49,.93,-.02,.225,.225),(1.34,.97,-.04,.205,.21),(1.19,1.01,-.065,.155,.17),(1.08,1.035,-.1,.18,.17),(.98,1.045,-.125,.165,.145)]
 av=[];af=[];an=10
 for z,cx,cy,rx,ry in armrings:
  for i in range(an):
   a=2*math.pi*i/an;av.append((sign*cx+rx*math.cos(a),cy+ry*math.sin(a),z))
 for j in range(len(armrings)-1):
  for i in range(an):af.append((j*an+i,j*an+(i+1)%an,(j+1)*an+(i+1)%an,(j+1)*an+i))
 af.extend([tuple(reversed(range(an))),tuple((len(armrings)-1)*an+i for i in range(an))])
 af=[tuple(reversed(face)) for face in af]
 am=bpy.data.meshes.new('Continuous arm topology');am.from_pydata(av,[],af);am.update();ao=bpy.data.objects.new('Continuous arm '+side,am);bpy.context.collection.objects.link(ao)
 auv=am.uv_layers.new()
 for face in am.polygons:
  for li in face.loop_indices:
   vi=am.loops[li].vertex_index;auv.data[li].uv=(vi%an/an,vi//an/(len(armrings)-1))
 def armweight(v,s=side):
  if v.z>1.97:
   t=max(0,min(.3,(v.z-1.97)*1.4));return {'chest':t,'upper_arm.'+s:1-t}
  if v.z>1.4:
   t=max(0,min(1,(1.73-v.z)/.33));return {'upper_arm.'+s:1-t,'forearm.'+s:t}
  t=max(0,min(1,(1.24-v.z)/.18));return {'forearm.'+s:1-t,'hand.'+s:t}
 assert am.polygons[0].normal.x>0, 'Arm surface faces inward'
 finish(ao,'Continuous arm '+side,0,armweight)
 for i in range(3):ball('Blunt finger '+side,(sign*(.94+.08*i),-.15,.925),(.048,.065,.105),7,{'hand.'+side:1},6,4)
 def legweight(v,s=side):
  t=max(0,min(1,(.66-v.z)/.25));return {'thigh.'+s:1-t,'shin.'+s:t}
 # Loose trouser legs: knee folds and uneven ankle hems, weighted to the leg rig.
 rings=[(1.04,.32,.33),(.87,.34,.34),(.65,.29,.3),(.56,.31,.29),(.49,.265,.255),(.4,.255,.255),(.25,.24,.24),(.19,.255,.255)]
 vv=[];ff=[];n=10
 for j,(z,rx,ry) in enumerate(rings):
  for i in range(n):
   a=2*math.pi*i/n;zz=z+(.025*math.sin(i*2.3) if j==len(rings)-1 else 0)
   vv.append((sign*.38+rx*math.cos(a),.015+ry*math.sin(a),zz))
 for j in range(len(rings)-1):
  for i in range(n):ff.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 ff=[tuple(reversed(face)) for face in ff]
 pm=bpy.data.meshes.new('Trouser leg');pm.from_pydata(vv,[],ff);pm.update();po=bpy.data.objects.new('Pants '+side,pm);bpy.context.collection.objects.link(po)
 puv=pm.uv_layers.new()
 for face in pm.polygons:
  for li in face.loop_indices:
   vi=pm.loops[li].vertex_index;puv.data[li].uv=(vi%n/n,vi//n/(len(rings)-1))
 assert pm.polygons[0].normal.x>0, 'Pants surface faces inward'
 finish(po,'Pants '+side,0,legweight);po.data.materials[0]=pantsmat
 # Broad, planar soles with bevelled edges and a low instep.
 fv=[];ff=[];fn=10
 for level in range(4):
  for i in range(fn):
   a=2*math.pi*i/fn
   if level==3:x=.165*math.cos(a);y=.005+.175*math.sin(a);z=.255
   else:
    scale=.96 if level==0 else 1.0
    x=.235*math.cos(a)*scale;y=-.16+.34*math.sin(a)*scale
    z=0 if level==0 else (.03 if level==1 else .11+.075*(y+.5)/.68)
   fv.append((sign*.4+x,y,z))
 for j in range(3):
  for i in range(fn):ff.append((j*fn+i,j*fn+(i+1)%fn,(j+1)*fn+(i+1)%fn,(j+1)*fn+i))
 ff.extend([tuple(reversed(range(fn))),tuple(3*fn+i for i in range(fn))])
 fm=bpy.data.meshes.new('Flat sole foot');fm.from_pydata(fv,[],ff);fm.update();fo=bpy.data.objects.new('Flat foot '+side,fm);bpy.context.collection.objects.link(fo)
 fuv=fm.uv_layers.new()
 for face in fm.polygons:
  for li in face.loop_indices:
   vi=fm.loops[li].vertex_index;fuv.data[li].uv=(vi%fn/fn,vi//fn/3)
 finish(fo,'Flat foot '+side,1,{'foot.'+side:1})
 assert all(abs(fo.data.vertices[i].co.z)<1e-6 for i in range(fn))

seat=ball('Pants seat',(0,.03,.94),(.65,.36,.25),0,{'pelvis':1},12,6);seat.data.materials[0]=pantsmat
belt=ball('Pants waistband',(0,-.03,1.045),(.66,.385,.085),0,{'pelvis':1},14,4);belt.data.materials[0]=pantsmat
ball('Thick neck',(0,0,2.2),(.25,.23,.27),7,{'neck':.5,'chest':.5})
ball('Human distorted skull',(.025,-.035,2.48),(.32,.28,.35),0,{'head':1},12,8)
ball('Heavy sagging jaw',(-.035,-.19,2.28),(.265,.19,.18),7)
ball('Swollen left cheek',(.205,-.205,2.44),(.17,.125,.2),0)
ball('Sunken right cheek',(-.205,-.2,2.43),(.13,.09,.16),1)
for sign in [-1,1]:
 ball('Ear',(sign*.32,-.005,2.49),(.075,.06,.115),7,segments=8,rings=5)
 ball('Eye socket',(sign*.128,-.278,2.55+sign*.012),(.105,.025,.075),2,segments=8,rings=5)
 ball('Clouded eye',(sign*.128,-.3,2.55+sign*.012),(.025,.015,.018),7,segments=8,rings=4)
 ball('Overhanging brow',(sign*.13,-.262,2.627),(.14,.07,.065),1,segments=8,rings=5)
ball('Crooked nose',(-.022,-.33,2.47),(.075,.1,.12),7,segments=8,rings=5)
ball('Mouth cavity',(.018,-.345,2.34),(.15,.018,.066),2,segments=10,rings=4)
for i in range(3):ball('Broken tooth',(-.07+i*.064,-.365,2.365-(i%2)*.009),(.017,.014,.032),5,segments=6,rings=4)
# Join the skinned surface; preserve atlas UVs and bone weights.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Brute_Skin'
body.parent=arm;mod=body.modifiers.new('Brute skinning','ARMATURE');mod.object=arm
tri=body.modifiers.new('PSX triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
# Bake a rounded upper-back posture into both mesh and rest skeleton.
# The lower belly and legs stay planted; arms hang from the advanced shoulders.
def spine_bend(co):
 p=Vector(co); t=max(0.0,min(1.0,(p.z-1.35)/.8)); t=t*t*(3-2*t)
 angle=math.radians(35)*t
 pivot=Vector((0,0,1.35))
 return pivot+Matrix.Rotation(angle,3,'X')@(p-pivot)
shoulder_delta=spine_bend(Vector((0,0,2.02)))-Vector((0,0,2.02))
neck_base=Vector((0,0,2.3));bent_neck=spine_bend(neck_base)
def hunch_position(co,group):
 p=Vector(co)
 if any(token in group for token in ['upper_arm','forearm','hand']):
  return p+shoulder_delta
 if group=='head':
  return bent_neck+Matrix.Rotation(math.radians(15),3,'X')@(p-neck_base)
 return spine_bend(p)
for v in body.data.vertices:
 weights=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups if g.weight>0]
 v.co=sum((hunch_position(v.co,name)*weight for name,weight in weights),Vector())
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
for bone in arm.data.edit_bones:
 head=bone.head.copy();tail=bone.tail.copy()
 bone.head=hunch_position(head,bone.name);bone.tail=hunch_position(tail,bone.name)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
# Place pustules against the actual hunched skin surface, not an approximate torso volume.
# Copy weights from the struck triangle, so each boil follows its attachment point.
surface=BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices],[list(p.vertices) for p in body.data.polygons],all_triangles=True)
boils=[(-.42,-.49,1.9,.12),(-.55,-.5,1.72,.09),(-.3,-.56,1.82,.07),(.5,-.65,1.43,.14),(.59,-.55,1.27,.085),(.32,-.79,1.5,.08),(-.53,-.66,1.35,.1),(.66,.12,2.17,.105),(-.7,-.15,2.17,.13),(.28,.35,1.86,.13),(-.38,.38,1.7,.11)]
parts=[];attachments=[]
for i,(x,y,z,r) in enumerate(boils):
 anchor=spine_bend(Vector((x,y,z)));front=y<0
 origin=Vector((anchor.x,-3 if front else 3,anchor.z))
 point,normal,index,distance=surface.ray_cast(origin,Vector((0,1 if front else -1,0)))
 if point is None:raise RuntimeError('Cannot attach boil '+str(i))
 w={}
 for vi in body.data.polygons[index].vertices:
  for g in body.data.vertices[vi].groups:
   bn=body.vertex_groups[g.group].name;w[bn]=w.get(bn,0)+g.weight/3
 normal.normalize();rotation=Vector((0,0,1)).rotation_difference(normal)
 for label,scale,tile,lift in [('Inflamed boil rim',(r*1.2,r*1.2,r*.18),6,r*.055),('Green acid boil',(r,r,r*.8),3,r*.48)]:
  o=ball(label+' '+str(i),(0,0,0),scale,tile,w,10,6)
  for v in o.data.vertices:v.co=point+normal*lift+rotation@v.co
 attachments.append({'index':i,'surface':list(point),'normal':list(normal),'height_above_skin':r*1.28})
bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
tri=body.modifiers.new('Pustule triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
with open(SRC+'/BoilAttachments.json','w') as f:json.dump(attachments,f,indent=2)
# Animation takes matching the template's clip names. Gameplay still drives the timings.
arm.animation_data_create();fps=30;bpy.context.scene.render.fps=fps
clips={'MonsterIdle':60,'MonsterIdleStandUp':60,'MonsterStandup':30,'MonsterWalk':40,'MonsterAttack':30,'MonsterHurt':24,'MonsterHurtStandUp':24,'MonsterDeath':45,'MonsterDeathStandUp':45,'MonsterDeathHeadOff':45,'MonsterGrabInto':30,'MonsterGrabHit':30,'MonsterGrabLoop':40}
for name,length in clips.items():
 action=bpy.data.actions.new(name);arm.animation_data.action=action
 for frame in range(1,length+2):
  phase=(frame-1)/length;wave=math.sin(phase*2*math.pi)
  for pb in arm.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
  arm.pose.bones['spine'].rotation_euler.x=math.radians(2)*wave
  if name=='MonsterWalk':
   for side,sign in [('L',1),('R',-1)]:
    arm.pose.bones['thigh.'+side].rotation_euler.x=.3*wave*sign
    arm.pose.bones['shin.'+side].rotation_euler.x=-.22*max(0,-wave*sign)
    arm.pose.bones['upper_arm.'+side].rotation_euler.x=-.2*wave*sign
   arm.pose.bones['pelvis'].location.y=.025*math.sin(phase*4*math.pi)
   arm.pose.bones['spine'].rotation_euler.z=.035*wave
  elif name in ['MonsterAttack','MonsterGrabHit','MonsterGrabInto']:
   # Wind up, then bring both arms down at the montage's middle hit frame.
   a=math.sin(min(1,phase/.48)*math.pi/2) if phase<.48 else max(0,1-(phase-.48)/.24)
   for side in ['L','R']:
    arm.pose.bones['upper_arm.'+side].rotation_euler.x=-1.8*a
    arm.pose.bones['forearm.'+side].rotation_euler.x=-.5*a
   arm.pose.bones['spine'].rotation_euler.x=-.12*a+.15*math.sin(phase*math.pi)
  elif 'Hurt' in name:
   arm.pose.bones['spine'].rotation_euler.x=-.22*math.sin(phase*math.pi)
   arm.pose.bones['head'].rotation_euler.x=.15*math.sin(phase*math.pi)
  elif 'Death' in name:
   arm.pose.bones['pelvis'].location.y=-.75*phase
   arm.pose.bones['pelvis'].rotation_euler.x=1.3*phase
  for pb in arm.pose.bones:
   pb.keyframe_insert('rotation_euler',frame=frame);pb.keyframe_insert('location',frame=frame)
 # FBX takes exported from explicit NLA strips.
 track=arm.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,action);strip.name=name;track.mute=True
arm.animation_data.action=None
for pb in arm.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
# Export all named actions with exact take names.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=OUT+'/Blobber_Model.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
# Presentable source scene and preview, separate from export.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.world=bpy.data.worlds.new('Brute Preview World');scene.world.color=(.045,.045,.045)
def aim(o,point):o.rotation_euler=(Vector(point)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(4.2,-7.6,3.1));cam=bpy.context.object;aim(cam,(0,0,1.4));cam.data.type='ORTHO';cam.data.ortho_scale=3.6;scene.camera=cam
for loc,power,size in [((2,-4,5),230,4),((-3,-2,3),100,3),((1,3,4),280,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,1.4))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object;floor.name='Preview floor (not exported)';fm=bpy.data.materials.new('Backdrop');fm.diffuse_color=(.035,.043,.045,1);floor.data.materials.append(fm)
scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=SRC+'/Previews/Brute_Final_ThreeQuarter.png'
bpy.ops.wm.save_as_mainfile(filepath=SRC+'/Blobber.blend');bpy.ops.render.render(write_still=True)
print('BRUTE_REPORT',json.dumps({'vertices':len(body.data.vertices),'triangles':len(body.data.polygons),'bones':len(arm.data.bones),'clips':clips,'unweighted':sum(not v.groups for v in body.data.vertices)}))


for label,clip,frame in [('Walk','MonsterWalk',11),('Attack','MonsterAttack',15)]:
 arm.animation_data.action=bpy.data.actions[clip]
 scene.frame_set(frame)
 scene.render.filepath=SRC+'/Previews/Brute_Final_'+label+'.png'
 bpy.ops.render.render(write_still=True)
arm.animation_data.action=None
for pb in arm.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=SRC+'/Blobber.blend')
