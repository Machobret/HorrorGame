"""Assemble complete exterior buildings from ALEX's authored module geometry/UVs.

Run in background Blender. Original files remain read-only; only selected completed
buildings are exported to the isolated Unity project.
"""
from pathlib import Path
from collections import Counter,defaultdict
import math,json,hashlib
import bpy
from mathutils import Vector,Matrix

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
plan=json.loads((HERE/'Selection.json').read_text())
STAGE=Path(plan['stage_root'])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(PROJECT/plan['source']))
sources={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
assert len(sources)==188
surfaces={m['source_material']:m for m in plan['materials']}
for obj in sources.values():
    obj.hide_render=True
    assert len(obj.data.uv_layers)>0 and len(obj.data.materials)==1
    material=obj.data.materials[0]
    images=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    assert images and all(im.packed_file and hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==surfaces[material.name]['atlas_sha256'] for im in images)

def rectangle(w,d):return {(x,y) for x in range(w) for y in range(d)}

specs=[
    dict(name='Alex_RedBrickWalkup',theme='RedBrick',levels=[rectangle(4,5)]*4,roof='flat',seed=0),
    dict(name='Alex_WhiteBrickCorner',theme='GreyBrick',levels=[{(x,y) for x in range(5) for y in range(5) if x<2 or y<2}]*5,roof='flat',seed=2),
    dict(name='Alex_GreyCourtyard',theme='GreyPlaster',levels=[{(x,y) for x in range(7) for y in range(5) if x<2 or x>=5 or y>=3}]*4,roof='flat',seed=4),
    dict(name='Alex_OrangeTerrace',theme='OrangePlaster',levels=[rectangle(4,8)]*3,roof='hip',seed=1),
    dict(name='Alex_RedPlasterTower',theme='RedPlaster',levels=[rectangle(4,4)]*4+[{(x,y) for x in range(1,4) for y in range(1,4)}]*2,roof='flat',seed=3),
    dict(name='Alex_LongBrickBlock',theme='RedBrick',levels=[rectangle(4,10)]*5,roof='hip_black',seed=5),
]

def edges(cells):
    result=[]
    for x,y in sorted(cells):
        # Local module width is +Y, height +Z and exterior normal +X.
        if (x+1,y) not in cells:result.append((x,y,0,(2*x+2,2*y),0))
        if (x,y+1) not in cells:result.append((x,y,1,(2*x+2,2*y+2),math.pi/2))
        if (x-1,y) not in cells:result.append((x,y,2,(2*x,2*y+2),math.pi))
        if (x,y-1) not in cells:result.append((x,y,3,(2*x,2*y),-math.pi/2))
    return result

def window_variant(x,y,side,floor,seed):
    # Preserve each bay's wide/narrow frame family, varying curtains/wear by storey.
    base=(x+2*y+side+seed)%8+1
    choices=tuple(range(1,7)) if base<=6 else (7,8)
    bay=(x*73856093)^(y*19349663)^(side*83492791)^(seed*2654435761)
    previous=-1
    for level in range(floor+1):
        value=(bay+(level+1)*0x9e3779b9)&0xffffffff
        value^=value>>16;value=(value*0x7feb352d)&0xffffffff
        value^=value>>15;value=(value*0x846ca68b)&0xffffffff;value^=value>>16
        candidate=value%len(choices)
        if candidate==previous:candidate=(candidate+1+(value//len(choices))%(len(choices)-1))%len(choices)
        previous=candidate
    return choices[previous]

rows=[];blueprints=[]
for spec in specs:
    placed=[];usage=Counter();uv_corners=0;expected_triangles=0;window_slots=[]
    def place(name,position,angle=0):
        global uv_corners,expected_triangles
        source=sources[name]
        obj=source.copy();obj.data=source.data.copy();obj.hide_render=False
        obj.name='Assembly_'+str(len(placed)).zfill(4)
        bpy.context.scene.collection.objects.link(obj)
        before=[tuple(v.uv) for v in obj.data.uv_layers.active.data]
        transform=Matrix.Translation(Vector(position))@Matrix.Rotation(angle,4,'Z')
        obj.data.transform(transform);obj.matrix_world=Matrix.Identity(4)
        assert [tuple(v.uv) for v in obj.data.uv_layers.active.data]==before
        usage[name]+=1;uv_corners+=len(before)
        expected_triangles+=sum(len(p.vertices)-2 for p in obj.data.polygons)
        placed.append(obj)

    base=spec['levels'][0];boundary=edges(base)
    front=[e for e in boundary if e[2]==3 and e[1]==0]
    entry=front[len(front)//2][:3]
    pipe_keys={e[:3] for e in boundary[::max(1,len(boundary)//3)]}
    for floor,cells in enumerate(spec['levels']):
        for x,y,side,point,angle in edges(cells):
            variant=window_variant(x,y,side,floor,spec['seed'])
            module=spec['theme']+'_window'+str(variant).zfill(2)
            if (x+y+side+spec['seed'])%9==0:module=spec['theme']+'_wall'
            if floor==0 and (x,y,side)==entry:module=spec['theme']+'_door'+str(spec['seed']%3+1).zfill(2)
            if '_window' in module:window_slots.append(dict(storey=floor+1,cell=[x,y],side=side,module=module))
            place(module,(*point,floor*2),angle)
            normal=Vector((math.cos(angle),math.sin(angle),0))
            along=Vector((-math.sin(angle),math.cos(angle),0))
            if (x,y,side) in pipe_keys:
                at=Vector((*point,floor*2))+normal*.07+along*.10
                place('gutter_rusty' if spec['seed']%2 else 'gutter',at,angle)
            if floor%2==1 and side==0 and y%3==1:
                at=Vector((*point,floor*2+.45))+normal*.015+along*.6
                place('ac_unit',at,angle)

        # Each exposed upper surface is tiled; raised storeys leave roof terraces.
        following=spec['levels'][floor+1] if floor+1<len(spec['levels']) else set()
        exposed=cells-following
        if not exposed:continue
        top=(floor+1)*2
        if spec['roof'].startswith('hip') and floor==len(spec['levels'])-1:
            length=2*(max(y for x,y in cells)+1)
            prefix='roof_black_' if spec['roof']=='hip_black' else 'roof_'
            place(prefix+'end',(8,0,top))
            for y in range(2,length-2,2):place(prefix+'mid',(8,y,top))
            place(prefix+'end',(0,length,top),math.pi)
        else:
            for x,y in sorted(exposed):place('roof_tile_large',(x*2+2,y*2,top))
            for x,y,side,point,angle in edges(cells):
                if (x,y) in exposed:place(spec['theme']+'_roof_long_norm',(*point,top),angle)

    # Apply one uniform source-grid correction and make the whole building's pivot.
    for obj in placed:obj.data.transform(Matrix.Scale(plan['uniform_source_scale'],4));obj.data.update()
    bpy.context.view_layer.update()
    corners=[Vector(c) for obj in placed for c in obj.bound_box]
    low=Vector([min(p[i] for p in corners) for i in range(3)])
    high=Vector([max(p[i] for p in corners) for i in range(3)])
    pivot=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
    for obj in placed:obj.data.transform(Matrix.Translation(-pivot))

    groups=defaultdict(list)
    for obj in placed:groups[obj.data.materials[0].name].append(obj)
    parts=[];joined=[]
    for index,(material,objects) in enumerate(sorted(groups.items())):
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join()
        obj=bpy.context.object;obj.name=spec['name']+'_Part'+str(index).zfill(2);obj.data.name=obj.name
        # Identical materials must remain one submesh per joined part.
        mat=bpy.data.materials[material];obj.data.materials.clear();obj.data.materials.append(mat)
        for face in obj.data.polygons:face.material_index=0
        parts.append(dict(name=obj.name,material_path=surfaces[material]['path'],triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)))
        joined.append(obj)
    assert sum(p['triangles'] for p in parts)==expected_triangles
    assert sum(len(o.data.uv_layers.active.data) for o in joined)==uv_corners
    bpy.ops.object.select_all(action='DESELECT')
    for obj in joined:obj.select_set(True)
    bpy.context.view_layer.objects.active=joined[0]
    model_path=plan['folder']+'/'+spec['name']+'_Model.fbx'
    target=STAGE/model_path;target.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,
        add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
    size=high-low
    rows.append(dict(name=spec['name'],source=plan['source'],source_sha256=plan['source_sha256'],
        model_path=model_path,prefab_path=plan['folder']+'/'+spec['name']+'_Exterior.prefab',model_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
        bounds_m=[size.x,size.z,size.y],triangles=expected_triangles,expected_triangles=expected_triangles,
        uv_corners=uv_corners,mesh_parts=len(parts),parts=parts,pivot='GroundCenter'))
    blueprints.append(dict(name=spec['name'],theme=spec['theme'],roof=spec['roof'],storeys=len(spec['levels']),
        seed=spec['seed'],window_pattern='Deterministic by bay and storey; wide/narrow family retained; adjacent storeys use different window images',
        window_slots=window_slots,floor_cells=[sorted(c) for c in spec['levels']],source_modules=dict(usage),module_instances=sum(usage.values())))
    for obj in joined:bpy.data.objects.remove(obj,do_unlink=True)

plan['models']=rows
for path in (HERE/'Selection.json',STAGE/'ArtSource/Environment/AlexBuildingKit/Selection.json'):
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8',newline='\n')
(HERE/'AssemblyBlueprints.json').write_text(json.dumps(blueprints,indent=2)+'\n',encoding='utf-8',newline='\n')
print('ALEX_BUILDINGS_ASSEMBLED models='+str(len(rows))+' triangles='+str(sum(r['triangles'] for r in rows)))
