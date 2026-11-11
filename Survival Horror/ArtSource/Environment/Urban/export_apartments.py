"""Read selected GLBs, bake their source placement to ground-centred metre FBXs.

Run in background Blender. Source meshes, UVs and material atlas remain intact.
"""
from pathlib import Path
import hashlib,json
import bpy
from mathutils import Matrix,Vector

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
plan=json.loads((HERE/'Selection.json').read_text())
scale=plan.get('uniform_source_scale',0.7)
assert abs(scale*plan.get('source_storey_pitch_m',4)-plan.get('target_storey_pitch_m',2.8))<.00001
stage=Path(plan['stage_root'])
records=[]

for row in plan['models']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(PROJECT/row['source']))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(meshes)==1,'Expected the complete apartment to be one source mesh'
    corners=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    low=Vector([min(p[i] for p in corners) for i in range(3)])
    high=Vector([max(p[i] for p in corners) for i in range(3)])
    pivot=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
    uv_before=[tuple(uv.uv) for o in meshes for uv in o.data.uv_layers.active.data]
    triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    assert triangles==row['expected_triangles'] and len(uv_before)>0
    for obj in meshes:
        assert len(obj.data.materials)==1 and obj.data.materials[0] is not None
        packed=[n.image for n in obj.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        assert packed and all(im.packed_file and hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==plan['atlas_sha256'] for im in packed)
        transform=Matrix.Scale(scale,4)@Matrix.Translation(-pivot)@obj.matrix_world
        obj.parent=None;obj.data=obj.data.copy();obj.data.transform(transform);obj.matrix_world=Matrix.Identity(4)
        obj.name=row['name']+'_Mesh';obj.data.name=obj.name
    assert [tuple(uv.uv) for o in meshes for uv in o.data.uv_layers.active.data]==uv_before
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    destination=stage/row['model_path'];destination.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(destination),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
    original_size=high-low
    size=original_size*scale
    row.update(bounds_m=[size.x,size.z,size.y],source_pivot_blender=list(pivot),triangles=triangles,
               original_bounds_m=[original_size.x,original_size.z,original_size.y],scale_correction=scale,
               storey_pitch_m=plan['target_storey_pitch_m'],
               uv_corners=len(uv_before),mesh_parts=len(meshes),pivot='GroundCenter',model_sha256=hashlib.sha256(destination.read_bytes()).hexdigest())
    records.append(row)

plan['models']=records
for p in (HERE/'Selection.json',stage/'ArtSource/Environment/Urban/Selection.json'):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8',newline='\n')
print('URBAN_APARTMENTS_EXPORTED models='+str(len(records)))
