"""Blender CLI: --background --python prepare_pack.py -- --source <PSX_ShelvesProps directory>."""
import bpy, json, sys, shutil, hashlib
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(sys.argv[sys.argv.index('--source') + 1])
OUT = ROOT / 'Assets/Game/Environment/PSXShelvesProps'
ART = Path(__file__).resolve().parent
OUT.mkdir(parents=True, exist_ok=True)
anchors = {}
manifest = {'source': str(SOURCE), 'materials': [], 'props': [], 'sourceFiles': []}
materials = {}

def category(name):
    if name.startswith('Display_'): return 'DisplayGeometry'
    if name.startswith('CeilingLight'): return 'Lighting'
    if name == 'BookPile': return 'Books'
    return 'Shelving'

for variant in ('Normal', 'Horror'):
    source_file = SOURCE / variant / ('PSX_ShelvesProps_' + variant + '.blend')
    manifest['sourceFiles'].append({'path': str(source_file), 'sha256': hashlib.sha256(source_file.read_bytes()).hexdigest()})
    bpy.ops.wm.open_mainfile(filepath=str(source_file))
    objects = sorted((o for o in bpy.context.scene.objects if o.type == 'MESH'), key=lambda o:o.name)
    for obj in objects:
        name = obj.name
        corners = [obj.matrix_world @ Vector(p) for p in obj.bound_box]
        low = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
        if variant == 'Normal':
            anchor = (low + high) * .5
            if category(name) == 'Lighting': anchor.z = high.z
            elif name == 'WallShelf': anchor.y = high.y
            else: anchor.z = low.z
            anchors[name] = anchor.copy()
        if name not in anchors: raise RuntimeError('Unpaired Horror object: '+name)
        anchor = anchors[name]
        slot_names = []
        for mat in obj.data.materials:
            if not mat: raise RuntimeError('Unassigned material: '+name)
            slot_names.append(mat.name)
            if mat.name in materials: continue
            image_nodes = [n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image] if mat.use_nodes else []
            image_name = Path(image_nodes[0].image.filepath.replace('\\','/')).name if image_nodes else ''
            if not image_name: raise RuntimeError('Missing source texture: '+mat.name)
            texture_source = SOURCE/'Textures'/image_name
            if not texture_source.exists(): raise FileNotFoundError(texture_source)
            (OUT/'Textures').mkdir(exist_ok=True)
            shutil.copy2(texture_source, OUT/'Textures'/image_name)
            bs = next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
            def number(key,default): return float(bs.inputs[key].default_value) if bs and key in bs.inputs else default
            emission = list(bs.inputs['Emission Color'].default_value) if bs and 'Emission Color' in bs.inputs else [0,0,0,1]
            materials[mat.name] = {'name':mat.name,'texture':image_name,'roughness':number('Roughness',.8),
                'metallic':number('Metallic',0),'alpha':number('Alpha',1),'emission':emission,
                'emissionStrength':number('Emission Strength',0)}
        group = category(name)
        directory = OUT/'Models'/variant/group
        directory.mkdir(parents=True,exist_ok=True)
        # Bake only the original mesh's world transform and shift to a canonical placement origin.
        # Both variants use the NORMAL anchor, including spilled items in the Horror mesh.
        mesh = obj.data.copy()
        mesh.transform(Matrix.Translation(-anchor) @ obj.matrix_world)
        mesh.update()
        exported = bpy.data.objects.new(name,mesh)
        bpy.context.collection.objects.link(exported)
        bpy.ops.object.select_all(action='DESELECT')
        exported.select_set(True); bpy.context.view_layer.objects.active=exported
        path = directory/(name+'_'+variant+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
            add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP',
            use_mesh_modifiers=True)
        manifest['props'].append({'name':name,'variant':variant,'category':group,
            'model':path.relative_to(ROOT).as_posix(),'materials':slot_names,
            'size':[high.x-low.x,high.z-low.z,high.y-low.y],
            'pivotSource':[anchor.x,anchor.y,anchor.z]})
        bpy.data.objects.remove(exported,do_unlink=True)
        bpy.data.meshes.remove(mesh)

manifest['materials']=list(materials.values())
(OUT/'PackManifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
for name in ('LICENSE.txt','README.txt'):
    shutil.copy2(SOURCE/name,ART/('Original_'+name))
print('SHELVES_EXPORT_OK props='+str(len(manifest['props']))+' materials='+str(len(materials)))
