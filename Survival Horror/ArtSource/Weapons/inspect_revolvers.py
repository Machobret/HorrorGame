from pathlib import Path
import bpy,json
from mathutils import Vector
root=Path('C:/Users/Mangis/Downloads/Assets/Guns/PSXRevolverPack[FIXED]/PSXRevolverPack[FIXED]/Files')
report=[]
for path in root.rglob('*.fbx'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[]
    for obj in bpy.data.objects:
        if obj.type!='MESH': continue
        pts=[obj.matrix_world@Vector(v) for v in obj.bound_box]
        entry={'name':obj.name,'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)],'materials':[]}
        for mat in obj.data.materials:
            entry['materials'].append({'name':mat.name,'images':[n.image.filepath for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]})
        objects.append(entry)
    report.append({'file':str(path),'objects':objects})
dest=Path(__file__).resolve().parent/'Revolver'
dest.mkdir(exist_ok=True)
(dest/'source_models.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
