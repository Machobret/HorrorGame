from pathlib import Path
import bpy,json
from mathutils import Vector
root=Path('C:/Users/Mangis/Downloads/Assets/Guns')
paths=[root/'PSXMiscGuns/PSXMiscGuns/1911/1911.fbx',root/'PSXShotgunPack[FIXED]/PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Fbx/DoubleBarrelShotgun.fbx']
report=[]
for path in paths:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 entries=[]
 for obj in bpy.data.objects:
  if obj.type!='MESH':continue
  pts=[obj.matrix_world@Vector(p) for p in obj.bound_box]
  entries.append({'name':obj.name,'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)],'materials':[{'name':m.name,'images':[n.image.filepath for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]} for m in obj.data.materials]})
 report.append({'path':str(path),'objects':entries})
dest=Path(__file__).resolve().parent/'TemplateReplacements';dest.mkdir(exist_ok=True)
(dest/'sources.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
