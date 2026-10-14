import bpy,json,os
from mathutils import Vector
paths={'Rifle':r'C:/Users/Mangis/Downloads/Assets/Guns/PSXHuntingRifle[FIXED]/PSXHuntingRifle[FIXED]/HuntingRifle.fbx','Flare':r'C:/Users/Mangis/Downloads/Assets/Guns/PSXMiscGuns/PSXMiscGuns/FlareGun/FlareGun.fbx'}
for label,path in paths.items():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=path)
 for o in bpy.data.objects:
  if o.type=='MESH':
   pts=[o.matrix_world@Vector(v) for v in o.bound_box]
   print(label,json.dumps({'name':o.name,'vertices':len(o.data.vertices),'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)],'materials':[m.name for m in o.data.materials]}))
 print(label,'ACTIONS',[(a.name,a.frame_range[:]) for a in bpy.data.actions])