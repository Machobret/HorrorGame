"""Blender recipe for the approved November 2026 environment download batch.

Run with Blender --background --factory-startup --disable-autoexec --python this.py.
Working files stay in the temporary source batch; runtime exports go to their owning
Assets/Game/Environment family. The source review file is updated only after Unity
confirms successful import (see EnvironmentAssetImportBuilder).
"""
from pathlib import Path
import bpy, bmesh, json, math, re, hashlib, shutil
from collections import defaultdict
from mathutils import Vector, Matrix
import numpy as np

PROJECT = Path(__file__).resolve().parents[3]
STAGE = Path(__import__('tempfile').gettempdir()) / 'CodexDownloadedAssetsReview-20261103'
WORK = STAGE / 'UnityImport'
WORK.mkdir(exist_ok=True)
REVIEW = Path.home() / 'Downloads' / 'DownloadedAssets_Review.blend'
RUNTIME = PROJECT / 'Assets' / 'Game' / 'Environment'
SIZE = 1024
PAD = 4

# GGBot cars were imported at oversized review-world dimensions. Keep the pack's
# relative proportions (including its loose wheel), with metre-sized geometry and
# unit-scale Unity prefab roots. Classic64 vehicles use their separate recipe.
CARS_SOURCE_SCALE = 0.7

def clean(name):
    return re.sub(r'[^A-Za-z0-9]+', '_', name).strip('_')

def category(name):
    n = name.lower()
    if n.startswith('tablelamp'):return 'LightingElectrical'
    if n.startswith('basketball'):return 'HouseholdDecor'
    if n.startswith('painting'):return 'HouseholdDecor'
    if n.startswith(('box_pizza','box_strawberries')):return 'FoodKitchenware'
    families = [
        ('InteriorFixtures', ('sink','bath','toilet','radiator','curtain','doormat','carpet')),
        ('Furniture', ('chair','armchair','table','bed','couch','shelves','shelv','bookshelf','bookcase','sideboard','wardrobe','chest_drawers','cupboard','cabinet','fitment','kitchen')),
        ('AppliancesElectronics', ('fridge','refriger','oven','stove','washing','microwave','toaster','tv','radio','phone','remote','ventilator','ceiling_fan','blades')),
        ('LightingElectrical', ('lamp','light','ceilinglight','walllight','focus','farol','breaker','socket','powersocket','button','b_01','b_02')),
        ('StorageContainers', ('box','crate','barrel','locker','bin','trash','garbage','petrol','pallet','basket')),
        ('ToolsEquipment', ('shovel','beak','generator','ladder','broom','bucket','medical','flashlight','umbrella','hanger','cable')),
        ('MineEquipment', ('wagon','tnt','detonator','mining_helmet','tray')),
        ('Nature', ('rock','plant','flower','gravel')),
        ('FoodKitchenware', ('food','pizza','plate','clean_plate','dirty_plate','bowl','cereal_bowl','glass','empty_glass','empy_glass','tumbler','cup','pan','pot','knife','butter','fork','spoon','cutlery','chicken','meat','milk','cake','flan','ice_cream','fried','can','beer','soda','bottle','choco','chips','toast','apple','burger','egg','scotch','product','paperbag')),
        ('HouseholdDecor', ('book','painting','clock','alarm','mirror','tedd','globe','chess','basketball','pencil','vhs','gadgets','palette')),
    ]
    for family, prefixes in families:
        if n.startswith(prefixes): return family
    return 'HouseholdDecor'

def bounds(objects):
    p = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    return Vector(tuple(min(x[i] for x in p) for i in range(3))), Vector(tuple(max(x[i] for x in p) for i in range(3)))

def selection(manifest):
    selected, deferred = [], []
    for a in manifest['assets']:
        meshes = sorted((o for o in bpy.data.collections[a['collection']].all_objects if o.type == 'MESH'), key=lambda o:o.name)
        cat = a['category']
        if cat in {'Characters','Fences','Vegetation','SurfaceMaterials'}:
            deferred.append({'source':a['source'],'collection':a['collection'],'reason':{'Vegetation':'Texture/material reuse only; no source foliage meshes','SurfaceMaterials':'Texture swatches are not runtime meshes'}.get(cat,'Deferred family including its materials')})
            continue
        if cat == 'ModularInteriors':
            if '/props/' not in a['source'].replace('\\','/'):
                deferred.append({'source':a['source'],'collection':a['collection'],'reason':'Construction geometry and its materials deferred'})
                continue
            if len(meshes) > 1:
                deferred.append({'source':a['source'],'collection':a['collection'],'reason':'Separate opening furniture components; solid variants only'})
                continue
        if cat == 'MineKit' and Path(a['source'].replace('\\','/')).stem == 'Mines_Modu':
            deferred.append({'source':a['source'],'collection':a['collection'],'reason':'Mine construction and placed construction instances deferred'})
            continue
        if cat in {'MineKit','VillageInteriors'}:
            units = [[o] for o in meshes]
        else:
            units = [meshes]
        for objects in units:
            if not objects: continue
            o=objects[0]; name=o.get('source_object_name',o.name) if cat in {'MineKit','VillageInteriors'} else a['name']
            original = re.sub(r'\.\d{3}$','',name)
            reason = None
            if cat == 'VillageInteriors':
                if name.startswith('House_Demo'): reason='Construction/demo arrangement; no implicit structural import'
                elif name in {'Chest_drawers','Chest_drawers_02','Chest_drawers_06'}: reason='Opening cabinet body; supplied complete solid variants selected where available'
                elif name.startswith('Chest_drawers') and '.' in name and name not in {'Chest_drawers_02.005','Chest_drawers_06.007'}: reason='Separate drawer or opening cabinet assembly'
                elif name.startswith('Refrigerator'): reason='Separate refrigerator opening components; use supplied solid variants'
                elif name.startswith('Ceiling_Fan_Blades') or name.startswith('blades'): reason='Moving fan component; standalone complete models retained separately'
                elif name.startswith('Cupboard') and name not in {'Cupboard_01.002','Cupboard_01.003'}: reason='Opening cupboard component'
                elif name.startswith('Flower_Table'):reason='Foliage geometry deferred; texture/material reuse only'
            elif cat == 'MineKit':
                if original.startswith(('Fence','Tb_','MetalRusted','Gravel_R')): reason='Construction/fence piece deferred'
                elif name.startswith(('Locker_D','Locker_01','Detonator_M','Cables_Detonator','Farol_M')): reason='Separate opening/alternate assembly component; solid version retained'
                elif name.startswith(('Barrel.','Barrel_01.')): reason='Repeated placed copy; named source model retained'
            elif cat == 'KitchenProps' and a['name']=='fridge_door': reason='Separate opening refrigerator assembly'
            elif cat == 'Vehicles' and a['name']=='Car_Shadow': reason='Legacy shadow helper; Unity supplies scene shadows'
            if cat=='ModularInteriors' and a['name'].startswith('plant'):reason='Foliage geometry deferred; texture/material reuse only'
            if any(len(x.data.polygons)==0 for x in objects): reason='No usable source faces'
            if reason:
                deferred.append({'source':a['source'],'objects':[x.name for x in objects],'reason':reason})
                continue
            family = 'RoadFurniture' if cat=='RoadFurniture' else 'Vehicles' if cat=='Vehicles' else category(name)
            pack = {'VillageInteriors':'Village','KitchenProps':'Kitchen','ModularInteriors':'HouseProps','MineKit':'Mine','Vehicles':'Cars','RoadFurniture':'Road'}[cat]
            assetname=pack+'_'+clean(name)
            sourceid=hashlib.sha256((a['source']+'|'+','.join(x.get('source_object_name',x.name) for x in objects)).encode()).hexdigest()[:16]
            selected.append({'id':sourceid,'name':assetname,'category':family,'source':a['source'],'source_asset':a['name'],'source_objects':[x.name for x in objects],'source_collection':a['collection'],'source_root':a['review_root']})
    return selected,deferred



def surface_mode(mat):
    if not mat or not mat.use_nodes: return 'Opaque'
    bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if not bsdf: return 'Opaque'
    if bsdf.inputs['Transmission Weight'].default_value>0.01: return 'Transparent'
    alpha=bsdf.inputs['Alpha']
    if alpha.is_linked:
        upstream=[link.from_node for link in alpha.links];seen=set();im=None
        while upstream:
            node=upstream.pop()
            if node in seen:continue
            seen.add(node)
            if node.type=='TEX_IMAGE' and node.image:im=node.image;break
            upstream.extend(link.from_node for socket in node.inputs for link in socket.links)
        if im and im.channels==4:
            pixels=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(pixels)
            al=pixels[3::4]
            if np.min(al)<0.99:
                return 'Transparent' if mat.surface_render_method=='BLENDED' and np.any((al>0.05)&(al<0.95)) else 'Cutout'
    elif alpha.default_value<0.99: return 'Transparent'
    return 'Opaque'

def make_copy(entry, dest):
    copies=[]
    for name in entry['source_objects']:
        src=bpy.data.objects[name]
        o=src.copy();o.data=src.data.copy();o.animation_data_clear();dest.objects.link(o)
        world=src.matrix_world.copy();o.parent=None;o.matrix_world=world
        if o.data.uv_layers:
            o.data.uv_layers.active.name='SourceUV'
        else: o.data.uv_layers.new(name='SourceUV')
        copies.append(o)
    if len(copies)>1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in copies:o.select_set(True)
        bpy.context.view_layer.objects.active=copies[0]
        bpy.ops.object.join()
    o=copies[0];o.name=entry['name'];o.data.name=entry['name']+'_Mesh'
    # Bake transforms into the copied mesh, preserving the source world orientation.
    world=o.matrix_world.copy();o.data.transform(world);o.matrix_world=Matrix.Identity(4)
    coords=[v.co for v in o.data.vertices]
    lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)));hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    o.data.transform(Matrix.Translation(-center))
    source_size = hi-lo
    factor = CARS_SOURCE_SCALE if entry['category']=='Vehicles' and entry['name'].startswith('Cars_') else 1.0
    if factor != 1.0:
        o.data.transform(Matrix.Scale(factor, 4))
        entry['source_dimensions_m'] = list(source_size)
        entry['scale_correction'] = factor
    entry['dimensions_m']=list(source_size * factor)
    entry['source_material_count']=len(o.data.materials)
    entry['vertices']=len(o.data.vertices);entry['triangles']=sum(len(p.vertices)-2 for p in o.data.polygons)
    return o



def source_material(mat, channel, image):
    m=mat.copy() if mat else bpy.data.materials.new('DefaultSource')
    m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links
    bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
    # Pin implicit UV inputs to the untouched source UV set.
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='SourceUV'
    for n in list(nodes):
        if n.type=='TEX_IMAGE' and not n.inputs['Vector'].is_linked:links.new(uv.outputs['UV'],n.inputs['Vector'])
        elif n.type=='UVMAP' and n!=uv:n.uv_map='SourceUV'
        elif n.type=='TEX_COORD':
            for link in list(n.outputs['UV'].links):links.new(uv.outputs['UV'],link.to_socket)
    emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=1
    def connect(socket,target,default):
        if socket and socket.is_linked:links.new(socket.links[0].from_socket,target)
        else:
            value=socket.default_value if socket else default
            if hasattr(value,'__len__'):target.default_value=value
            elif target.type=='RGBA':target.default_value=(value,value,value,1)
            else:target.default_value=value
    if channel=='BaseColor':connect(bsdf.inputs['Base Color'] if bsdf else None,emit.inputs['Color'],(0.8,0.8,0.8,1))
    elif channel=='Alpha':connect(bsdf.inputs['Alpha'] if bsdf else None,emit.inputs['Color'],1)
    elif channel=='Mask':
        combine=nodes.new('ShaderNodeCombineColor');combine.mode='RGB'
        connect(bsdf.inputs['Metallic'] if bsdf else None,combine.inputs['Red'],0)
        connect(bsdf.inputs['Roughness'] if bsdf else None,combine.inputs['Green'],0.8)
        links.new(combine.outputs[0],emit.inputs['Color'])
    else:
        connect(bsdf.inputs['Emission Color'] if bsdf else None,emit.inputs['Color'],(0,0,0,1))
        if bsdf:connect(bsdf.inputs['Emission Strength'],emit.inputs['Strength'],1)
    out=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None) or nodes.new('ShaderNodeOutputMaterial')
    links.new(emit.outputs[0],out.inputs['Surface'])
    target=nodes.new('ShaderNodeTexImage');target.image=image;nodes.active=target;target.select=True
    return m

def image_pixels(im):
    p=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(p);return p.reshape(-1,4)

def save_pixels(name,pixels,path,color=True,width=SIZE,height=SIZE):
    im=bpy.data.images.new(name,width=width,height=height,alpha=True,float_buffer=True)
    im.colorspace_settings.name='Linear Rec.709' if color else 'Non-Color'
    im.pixels.foreach_set(pixels.astype(np.float32).ravel())
    im.filepath_raw=str(path);im.file_format='PNG'
    if color:im.save_render(str(path),scene=bpy.context.scene)
    else:im.save()
    bpy.data.images.remove(im)

def bake_page(page,objects,output):
    scene=bpy.context.scene
    name=page['name'];mode=page['mode']
    base_path=output/(name+'_BaseColor.png');mask_path=output/(name+'_MetallicSmoothness.png')
    width=page.get('width',SIZE);height=page.get('height',SIZE)
    target=bpy.data.images.new(name+'_Bake',width=width,height=height,alpha=True,float_buffer=True)
    target.colorspace_settings.name='Linear Rec.709'
    bake_objects=[];material_sources={};material_indices={}
    proxy_faces=defaultdict(set)
    for proxy in page.get('proxy_parts',[]):proxy_faces[proxy['owner']].update(proxy['faces'])
    for block_index,block in enumerate(page['blocks']):
        original=objects[block['object']]
        keep={f['index'] for f in block['faces']}-proxy_faces[block['object']]
        if not keep:continue
        o=original.copy();o.data=original.data.copy();scene.collection.objects.link(o);o.name=original.name+'_BakeOnly'
        # Keep temporary source surfaces spatially isolated during shared baking.
        o.location.x+=block_index*100
        bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in keep],context='FACES')
        bm.to_mesh(o.data);bm.free();o.data.update()
        material_sources[o.name]=list(o.data.materials)
        material_indices[o.name]=[p.material_index for p in o.data.polygons]
        bake_objects.append(o)
    for index,proxy in enumerate(page.get('proxy_parts',[])):
        mesh=bpy.data.meshes.new(name+'_PaletteBake');mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2,3)]);mesh.materials.append(bpy.data.materials[proxy['material']])
        source=mesh.uv_layers.new(name='SourceUV');atlas=mesh.uv_layers.new(name='AtlasUV')
        for loop in range(4):source.data[loop].uv=proxy['source_uvs'][loop];atlas.data[loop].uv=proxy['atlas_uvs'][loop]
        mesh.uv_layers.active=atlas;atlas.active_render=True
        o=bpy.data.objects.new(name+'_PaletteBake',mesh);scene.collection.objects.link(o);o.location.x=(len(page['blocks'])+index)*100;bake_objects.append(o)
    # Joining only temporary bake geometry avoids a separate Cycles setup per prop.
    # Runtime meshes and the review source objects remain independent.
    bpy.ops.object.select_all(action='DESELECT')
    for o in bake_objects:o.select_set(True)
    bpy.context.view_layer.objects.active=bake_objects[0]
    if len(bake_objects)>1:bpy.ops.object.join()
    bake_objects=[bpy.context.view_layer.objects.active]
    # Object joins can reset the active UV map to the first (source) layer.
    for o in bake_objects:
        o.data.uv_layers.active=o.data.uv_layers['AtlasUV']
        o.data.uv_layers['AtlasUV'].active_render=True
    material_sources={o.name:list(o.data.materials) for o in bake_objects}
    material_indices={o.name:[p.material_index for p in o.data.polygons] for o in bake_objects}
    has_emission=False
    for mats in material_sources.values():
        for m in mats:
            if m and m.use_nodes:
                b=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                if b and (b.inputs['Emission Color'].is_linked or max(b.inputs['Emission Color'].default_value[:3])>0.001) and (b.inputs['Emission Strength'].is_linked or b.inputs['Emission Strength'].default_value>0):has_emission=True
    channels=['BaseColor','Mask']+(['Alpha'] if mode!='Opaque' else [])+(['Emission'] if has_emission else [])
    pixels={}
    for channel in channels:
        bpy.ops.object.select_all(action='DESELECT')
        temporary=[]
        for o in bake_objects:
            o.hide_render=False;o.hide_viewport=False;o.select_set(True)
            o.data.materials.clear()
            for m in material_sources[o.name]:
                bake=source_material(m,channel,target);temporary.append(bake);o.data.materials.append(bake)
            for face,index in zip(o.data.polygons,material_indices[o.name]):face.material_index=index
            o.data.update()
        bpy.context.view_layer.update()
        bpy.context.view_layer.objects.active=bake_objects[0]
        print('BAKE',name,channel,'objects',len(bake_objects),flush=True)
        bpy.ops.object.bake(type='EMIT',use_clear=True,margin=max(1,int(page.get('padding',PAD*2)/2)-1),margin_type='EXTEND',uv_layer='AtlasUV')
        pixels[channel]=image_pixels(target)
        for o in bake_objects:o.data.materials.clear()
        for m in temporary:bpy.data.materials.remove(m)
    color=pixels['BaseColor'];coverage=color[:,3]>.5
    color[:,3]=pixels['Alpha'][:,0] if 'Alpha' in pixels else 1
    values=color[coverage]
    if len(values) and np.max(np.ptp(values,axis=0))<1/255:
        save_pixels(name+'_BaseColor',values.mean(axis=0).reshape(1,4),output/(name+'_BaseColor.png'),width=1,height=1)
        page['uniform_base_color']=True;page['base_color_dimensions']=[1,1]
    else:
        save_pixels(name+'_BaseColor',color,output/(name+'_BaseColor.png'),width=width,height=height)
        page['base_color_dimensions']=[width,height]
    mask=pixels['Mask'];mask[:,3]=1-mask[:,1];mask[:,1]=1;mask[:,2]=0
    page['base_color']=str((output/(name+'_BaseColor.png')).relative_to(PROJECT)).replace('\\','/')
    values=mask[coverage][:,[0,3]]
    if len(values) and np.max(np.ptp(values,axis=0))<1/255:
        page.pop('mask',None)
        page['metallic']=float(values[:,0].mean());page['smoothness']=float(values[:,1].mean())
        print('CONSTANT_SURFACE_NO_MASK_SAMPLE',name,page['metallic'],page['smoothness'],flush=True)
    else:
        save_pixels(name+'_MetallicSmoothness',mask,output/(name+'_MetallicSmoothness.png'),False,width,height)
        page['mask']=str((output/(name+'_MetallicSmoothness.png')).relative_to(PROJECT)).replace('\\','/')
    if has_emission:
        save_pixels(name+'_Emission',pixels['Emission'],output/(name+'_Emission.png'),width=width,height=height)
        page['emission']=str((output/(name+'_Emission.png')).relative_to(PROJECT)).replace('\\','/')
    else:page.pop('emission',None)
    for o in bake_objects:
        mesh=o.data;bpy.data.objects.remove(o,do_unlink=True);bpy.data.meshes.remove(mesh)
    bpy.data.images.remove(target)

def pack_existing_islands(objects,entries):
    """Use the installed UV-Packer engine; retain the authored source UV seams."""
    import importlib.util,subprocess,queue
    addon=Path(r'C:\Portable Applications\Blender Portable\Blender 5.0.1\portable\scripts\addons\UV-Packer')
    spec=importlib.util.spec_from_file_location('uvpacker_vendor',addon/'__init__.py')
    vendor=importlib.util.module_from_spec(spec);spec.loader.exec_module(vendor)
    groups=defaultdict(list);mode_cache={};parts=[]
    for name,o in objects.items():
        trace=o.data.attributes.new('AtlasSourceLoop','INT','CORNER')
        trace.data.foreach_set('value',list(range(len(o.data.loops))))
        for slot,mat in enumerate(o.data.materials):
            faces=[p.index for p in o.data.polygons if p.material_index==slot]
            if not faces:continue
            key=mat.name if mat else '__none__'
            if key not in mode_cache:mode_cache[key]=surface_mode(mat)
            mode=mode_cache[key]
            part=o.copy();part.data=o.data.copy();bpy.context.scene.collection.objects.link(part);part.name=name+'_PackingPart'
            bm=bmesh.new();bm.from_mesh(part.data);bm.faces.ensure_lookup_table()
            keep=set(faces);bmesh.ops.delete(bm,geom=[p for p in bm.faces if p.index not in keep],context='FACES');bm.to_mesh(part.data);bm.free()
            # FBX can duplicate vertices at normal boundaries even where its UV
            # charts are continuous. Weld only the temporary packing topology;
            # retain each corner's authored UV and the exported source geometry.
            bm=bmesh.new();bm.from_mesh(part.data);count=len(bm.faces);loop_count=len(part.data.loops)
            bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
            trace_layer=bm.loops.layers.int.get('AtlasSourceLoop');source_layer=bm.loops.layers.uv.get('SourceUV')
            unchanged=all((loop[source_layer].uv-o.data.uv_layers['SourceUV'].data[loop[trace_layer]].uv).length<1e-7 for face in bm.faces for loop in face.loops)
            if len(bm.faces)==count and sum(len(f.loops) for f in bm.faces)==loop_count and unchanged:bm.to_mesh(part.data)
            bm.free()
            part.data.uv_layers.active=part.data.uv_layers['AtlasUV'];part.data.update();part.hide_set(False)
            # Some authored retro UVs deliberately collapse faces onto a color or
            # strip in the texture. Preserve those coordinates using a texture-
            # rectangle packing proxy, rather than inventing a new unwrap.
            part.data.calc_loop_triangles();valid_world=valid_uv=0;collapsed=False
            for t in part.data.loop_triangles:
                a,b,c=[part.data.vertices[i].co for i in t.vertices];wa=(b-a).cross(c-a).length/2
                a,b,c=[part.data.uv_layers['SourceUV'].data[i].uv for i in t.loops];ua=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))/2
                if wa>1e-10 and ua<1e-12:collapsed=True
                if wa>1e-10 and ua>=1e-12:valid_world+=wa;valid_uv+=ua
            item={'part':part,'owner':name,'faces':faces}
            if collapsed:
                coords=[p.uv.copy() for p in part.data.uv_layers['SourceUV'].data]
                low=Vector((min(p.x for p in coords),min(p.y for p in coords)));high=Vector((max(p.x for p in coords),max(p.y for p in coords)))
                source_low=low.copy();source_high=high.copy()
                if high.x-low.x<1e-8:low.x-=1/256;high.x+=1/256
                if high.y-low.y<1e-8:low.y-=1/256;high.y+=1/256
                area=(high.x-low.x)*(high.y-low.y)
                world=valid_world*area/valid_uv if valid_uv>1e-12 else 16/(64*64)
                w=math.sqrt(max(world,1e-8)*(high.x-low.x)/(high.y-low.y));h=world/w
                mesh=bpy.data.meshes.new(name+'_TextureRectangle');mesh.from_pydata([(0,0,0),(w,0,0),(w,h,0),(0,h,0)],[],[(0,1,2,3)])
                mesh.materials.append(mat)
                src=mesh.uv_layers.new(name='SourceUV');dst=mesh.uv_layers.new(name='AtlasUV')
                for loop,xy,source_xy in zip(range(4),[low,Vector((high.x,low.y)),high,Vector((low.x,high.y))],[source_low,Vector((source_high.x,source_low.y)),source_high,Vector((source_low.x,source_high.y))]):src.data[loop].uv=source_xy;dst.data[loop].uv=xy
                old_mesh=part.data;part.data=mesh;bpy.data.meshes.remove(old_mesh);part.data.uv_layers.active=dst;dst.active_render=True
                item.update(proxy=True,low=list(low),high=list(high),proxy_source_low=list(source_low),proxy_source_high=list(source_high))
                print('PRESERVE_PALETTE_UVS',name,'material',mat.name,flush=True)
            parts.append(part);groups[(entries[name]['category'],mode)].append(item)
    pages=[]
    for (family,mode),items in sorted(groups.items()):
        area=0
        for item in items:
            mesh=item['part'].data;mesh.calc_loop_triangles()
            area+=sum((mesh.vertices[t.vertices[1]].co-mesh.vertices[t.vertices[0]].co).cross(mesh.vertices[t.vertices[2]].co-mesh.vertices[t.vertices[0]].co).length/2 for t in mesh.loop_triangles)
        target=64 if family in {'FoodKitchenware','HouseholdDecor','LightingElectrical','ToolsEquipment','AppliancesElectronics'} else 32
        desired=max(4096,area*target*target/.72)
        choices=[(w,h) for w in [64,128,256,512,1024] for h in [64,128,256,512,1024] if w>=h and w<=2*h]
        width,height=min(choices,key=lambda wh:abs(math.log((wh[0]*wh[1])/desired)))
        bpy.ops.object.select_all(action='DESELECT')
        meshes=[i['part'] for i in items]
        for o in meshes:o.select_set(True)
        bpy.context.view_layer.objects.active=meshes[0]
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        options={'PackMode':1,'Width':width,'Height':height,'Padding':2.0,'Combine':True,'Rescale':True,'PreRotate':True,'FullRotation':False,'Rotation':1,'TilesX':1,'TilesY':1}
        process=subprocess.Popen([str(addon/'UV-Packer-Blender.exe')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,shell=False)
        messages=queue.SimpleQueue();vendor.misc.data_exchange_thread(process,options,meshes,messages)
        stats=[]
        while not messages.empty():stats.append(messages.get())
        process.stdin.close()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.terminate()
        bpy.ops.object.mode_set(mode='OBJECT')
        coverage=next((float(m[1]) for m in stats if m[0]==vendor.misc.QueueMessage.STATS),None)
        if coverage is None:raise RuntimeError('UV-Packer failed for '+family+'/'+mode+': '+str(stats))
        # Trim unused outer space without changing texel density or chart shapes.
        points=[uv.uv.copy() for item in items for uv in item['part'].data.uv_layers['AtlasUV'].data]
        x0=max(0,4*math.floor((min(p.x for p in points)*width-2)/4));x1=min(width,4*math.ceil((max(p.x for p in points)*width+2)/4))
        y0=max(0,4*math.floor((min(p.y for p in points)*height-2)/4));y1=min(height,4*math.ceil((max(p.y for p in points)*height+2)/4))
        new_w=max(4,x1-x0);new_h=max(4,y1-y0)
        for item in items:
            for uv in item['part'].data.uv_layers['AtlasUV'].data:uv.uv=((uv.uv.x*width-x0)/new_w,(uv.uv.y*height-y0)/new_h)
        coverage*=width*height/(new_w*new_h);width,height=new_w,new_h
        blocks=defaultdict(list);density=[];proxies=[]
        for item in items:
            part=item['part'];owner=objects[item['owner']]
            if item.get('proxy'):
                a,b,c,d=[p.uv.copy() for p in part.data.uv_layers['AtlasUV'].data]
                lo=Vector(item['low']);hi=Vector(item['high'])
                for face in item['faces']:
                    for loop in owner.data.polygons[face].loop_indices:
                        uv=owner.data.uv_layers['SourceUV'].data[loop].uv
                        owner.data.uv_layers['AtlasUV'].data[loop].uv=a+(b-a)*((uv.x-lo.x)/(hi.x-lo.x))+(d-a)*((uv.y-lo.y)/(hi.y-lo.y))
                proxies.append({'owner':item['owner'],'faces':item['faces'],'material':part.data.materials[0].name,'source_uvs':[list(uv.uv) for uv in part.data.uv_layers['SourceUV'].data],'atlas_uvs':[list(uv.uv) for uv in part.data.uv_layers['AtlasUV'].data]})
            else:
                trace=part.data.attributes['AtlasSourceLoop'].data
                for index,value in enumerate(trace):owner.data.uv_layers['AtlasUV'].data[value.value].uv=part.data.uv_layers['AtlasUV'].data[index].uv
            blocks[item['owner']].extend(item['faces'])
            part.data.calc_loop_triangles();surface=uv_area=0
            for t in part.data.loop_triangles:
                a,b,c=[part.data.vertices[v].co for v in t.vertices];surface+=(b-a).cross(c-a).length/2
                a,b,c=[part.data.uv_layers['AtlasUV'].data[v].uv for v in t.loops];uv_area+=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))/2
            if surface>1e-8:density.append(math.sqrt(uv_area*width*height/surface))
        page={'name':family+'_'+mode+'_A','category':family,'mode':mode,'width':width,'height':height,'padding':2,'coverage_percent':coverage,'packing_tool':'UV-Packer 1.1.0','unwrap':'Preserved source UV islands; palette/strip mappings retained through texture regions','density_min':min(density),'density_max':max(density),'proxy_parts':proxies,'blocks':[{'object':name,'faces':[{'index':i} for i in faces]} for name,faces in blocks.items()]}
        pages.append(page)
        for name in blocks:entries[name]['target_texels_per_metre']=target
        print('UVPACKER_PAGE',page['name'],width,height,'coverage',coverage,'density',min(density),max(density),flush=True)
    for part in parts:
        mesh=part.data;bpy.data.objects.remove(part,do_unlink=True);bpy.data.meshes.remove(mesh)
    for o in objects.values():
        attribute=o.data.attributes.get('AtlasSourceLoop')
        if attribute:o.data.attributes.remove(attribute)
        o.data.update()
    return pages

def run():
    bpy.ops.wm.open_mainfile(filepath=str(REVIEW))
    bpy.context.preferences.filepaths.save_version=0
    manifest=json.loads(bpy.data.texts['Asset_Manifest.json'].as_string())
    selected,deferred=selection(manifest)
    (WORK/'selection.json').write_text(json.dumps({'selected':selected,'deferred':deferred},indent=2))
    print('SELECTED',len(selected),'DEFERRED',len(deferred),flush=True)
    if '--inspect-only' in __import__('sys').argv:return
    if not (WORK/'BeforeUnityImport.blend').exists():shutil.copy2(REVIEW,WORK/'BeforeUnityImport.blend')
    # Regeneration uses the archived sources, not the previously exported copies.
    previous=bpy.data.collections.get('UnityImported_Atlased')
    old_status=bpy.data.texts.get('Unity_Import_Status.json')
    if previous:
        if not old_status:raise RuntimeError('Existing imported collection has no ownership manifest')
        old=json.loads(old_status.as_string());owned={a['id'] for a in old['assets']}
        children=[];stack=[previous]
        while stack:
            c=stack.pop();children.append(c);stack.extend(c.children)
        for o in list(previous.all_objects):
            if o.type=='MESH' and o.get('source_asset_id') not in owned:raise RuntimeError('Unrecognized object in generated collection: '+o.name)
            mesh=o.data if o.type=='MESH' else None
            bpy.data.objects.remove(o,do_unlink=True)
            if mesh and mesh.users==0:bpy.data.meshes.remove(mesh)
        for c in reversed(children):bpy.data.collections.remove(c)
        for atlas in old['atlases']:
            material=bpy.data.materials.get(atlas['name'])
            if material and material.users==0:bpy.data.materials.remove(material)
        bpy.data.texts.remove(old_status)
    dest=bpy.data.collections.new('UnityImported_Atlased');bpy.context.scene.collection.children.link(dest)
    objects={};entries={}
    for e in selected:
        print('LAYOUT',e['name'],flush=True)
        o=make_copy(e,dest);objects[o.name]=o;entries[o.name]=e
        o.data.uv_layers.new(name='AtlasUV');o.data.uv_layers.active=o.data.uv_layers['AtlasUV'];o.data.uv_layers['AtlasUV'].active_render=True
    pages=pack_existing_islands(objects,entries)
    if '--pack-only' in __import__('sys').argv:
        (WORK/'packing_review.json').write_text(json.dumps({'pages':[{k:v for k,v in p.items() if k!='blocks'} for p in pages]},indent=2))
        print('PACKING_ONLY_REVIEW_COMPLETE',flush=True)
        return
    print('ATLAS_PLAN',len(pages),'pages',[(p['name'],len(p['blocks'])) for p in pages],flush=True)
    hidden_states={o.name:o.hide_render for o in bpy.data.objects}
    for o in bpy.data.objects:o.hide_render=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.bake.use_selected_to_active=False;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.image_settings.color_depth='8'
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
    for p in pages:
        output=RUNTIME/p['category'];output.mkdir(parents=True,exist_ok=True)
        bake_page(p,objects,output)
    for name,hidden in hidden_states.items():
        if name in bpy.data.objects:bpy.data.objects[name].hide_render=hidden
    atlas_mats={}
    for p in pages:
        m=bpy.data.materials.new(p['name']);m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links;b=nodes.get('Principled BSDF')
        tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(PROJECT/p['base_color']));tex.image.pack();tex.interpolation='Closest'
        links.new(tex.outputs['Color'],b.inputs['Base Color']);b.inputs['Roughness'].default_value=0.8
        if p['mode']!='Opaque':links.new(tex.outputs['Alpha'],b.inputs['Alpha'])
        if p['mode']=='Transparent':m.surface_render_method='BLENDED'
        if p.get('emission'):
            em=nodes.new('ShaderNodeTexImage');em.image=bpy.data.images.load(str(PROJECT/p['emission']));em.image.pack();links.new(em.outputs['Color'],b.inputs['Emission Color']);b.inputs['Emission Strength'].default_value=1
        atlas_mats[p['name']]=m
    for o in objects.values():o.data.materials.clear()
    for p in pages:
        for block in p['blocks']:
            o=objects[block['object']];m=atlas_mats[p['name']]
            if m.name not in o.data.materials:o.data.materials.append(m)
            slot=list(o.data.materials).index(m)
            for f in block['faces']:o.data.polygons[f['index']].material_index=slot
    exports=[]
    for family in sorted({e['category'] for e in selected}):
        group=[o for name,o in objects.items() if entries[name]['category']==family]
        bpy.ops.object.select_all(action='DESELECT')
        for o in group:
            o.select_set(True)
            # FBX UV0 is the first layer; source UVs are preserved on the hidden source objects.
            for layer in list(o.data.uv_layers):
                if layer.name!='AtlasUV':o.data.uv_layers.remove(layer)
        bpy.context.view_layer.objects.active=group[0]
        fbx=RUNTIME/family/(family+'_Models.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
        for o in group:
            e=entries[o.name];e['model_path']=str(fbx.relative_to(PROJECT)).replace('\\','/');e['mesh_name']=o.name;e['material_names']=[m.name for m in o.data.materials];e['prefab_path']='Assets/Game/Environment/'+family+'/'+o.name+'.prefab';exports.append(e)
    # Texture-only imports: approved standalone surfaces and shrub texture sources.
    textures=[]
    for pack in ['asphalt_04_1k','cobblestone_pavement_1k','All bushes']:
        for file in sorted((STAGE/'Sources'/pack).rglob('*')):
            if file.suffix.lower() not in {'.png','.jpg','.jpeg'}:continue
            family='Vegetation' if pack=='All bushes' else 'RoadSurfaces' if pack.startswith('asphalt') else 'MasonrySurfaces'
            output=RUNTIME/family;output.mkdir(parents=True,exist_ok=True)
            destfile=output/(clean(file.stem)+file.suffix.lower())
            if destfile.exists() and destfile.read_bytes()!=file.read_bytes():raise RuntimeError('Texture name conflict: '+str(destfile))
            shutil.copy2(file,destfile)
            textures.append({'path':str(destfile.relative_to(PROJECT)).replace('\\','/'),'source':str(file),'family':family,'role':'normal' if '_nor_gl_' in file.stem else 'roughness' if '_rough_' in file.stem else 'height' if '_disp_' in file.stem else 'base_color'})
    # Keep imported representations visibly separate from their archived source objects.
    row=0
    for family in sorted({e['category'] for e in selected}):
        group=[o for name,o in objects.items() if entries[name]['category']==family]
        width=max(max(entries[o.name]['dimensions_m'][0],0.5) for o in group)+1
        depth=max(max(entries[o.name]['dimensions_m'][1],0.5) for o in group)+1
        for i,o in enumerate(sorted(group,key=lambda o:o.name)):
            o.location=(300+(i%10)*width,-row-(i//10)*depth,0)
            o['source_asset_id']=entries[o.name]['id'];o['unity_status']='Exported; awaiting Unity verification'
            for source in entries[o.name]['source_objects']:bpy.data.objects[source].hide_set(True)
        row+=math.ceil(len(group)/10)*depth+5
    report={'batch':'DownloadedAssets_20261103','texel_density':'Preserved source UV islands, normalized by UV-Packer. Resolution selected by category surface area; target 32 px/m large props, 64 px/m small props, with measured page densities recorded. Two-pixel chart separation; page dimensions adapt to content.','assets':exports,'deferred':deferred,'atlases':[{k:v for k,v in p.items() if k not in {'free','blocks'}} for p in pages],'textures':textures,'status':'Exported; Unity verification pending'}
    (WORK/'export_manifest.json').write_text(json.dumps(report,indent=2))
    text=bpy.data.texts.new('Unity_Import_Status.json');text.write(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'AtlasedAssets_Review.blend'),compress=True)
    print('EXPORT_COMPLETE',len(exports),'prefabs planned;',len(pages),'atlas pages;',len(textures),'standalone maps',flush=True)


def finalize_review():
    """Commit the Blender layout/status only after Unity verifies the actual prefabs."""
    import datetime
    receipt=json.loads((WORK/'unity_import_receipt.json').read_text())
    report=json.loads((WORK/'export_manifest.json').read_text())
    if not receipt.get('verified'):raise RuntimeError('Unity has not verified the import')
    if {a['id'] for a in report['assets']}!={a['id'] for a in receipt['imported']}:raise RuntimeError('Receipt does not match export selection')
    for item in receipt['files']:
        if not (PROJECT/item['path']).is_file() or not item['guid']:raise RuntimeError('Missing imported asset: '+item['path'])
    bpy.ops.wm.open_mainfile(filepath=str(WORK/'AtlasedAssets_Review.blend'))
    bpy.context.preferences.filepaths.save_version=0
    # Keep the authoring preview consistent with Unity's packed surface channels.
    for atlas in report['atlases']:
        material=bpy.data.materials.get(atlas['name'])
        if not material:continue
        nodes=material.node_tree.nodes;links=material.node_tree.links;bsdf=nodes.get('Principled BSDF')
        if atlas.get('mask'):
            tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(PROJECT/atlas['mask']),check_existing=True);tex.image.colorspace_settings.name='Non-Color';tex.image.pack();tex.interpolation='Closest'
            separate=nodes.new('ShaderNodeSeparateColor');separate.mode='RGB';links.new(tex.outputs['Color'],separate.inputs['Color']);links.new(separate.outputs[0],bsdf.inputs['Metallic'])
            invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1;links.new(tex.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],bsdf.inputs['Roughness'])
        else:
            bsdf.inputs['Metallic'].default_value=atlas.get('metallic',0);bsdf.inputs['Roughness'].default_value=1-atlas.get('smoothness',.2)
    parent=bpy.data.collections['UnityImported_Atlased']
    by_name={a['name']:a for a in receipt['imported']}
    for source in report['assets']:
        for name in source['source_objects']:
            original=bpy.data.objects.get(name)
            if original:
                original['unity_status']='ImportedAndVerified';original['unity_prefab']=by_name[source['name']]['prefab_path'];original['source_asset_id']=source['id']
    for item in report['deferred']:
        names=item.get('objects',[])
        if item.get('collection') and item['collection'] in bpy.data.collections:names=[o.name for o in bpy.data.collections[item['collection']].all_objects]
        for name in names:
            original=bpy.data.objects.get(name)
            if original and original.get('unity_status')!='ImportedAndVerified':original['unity_status']='Deferred';original['deferred_reason']=item['reason']
    for family in sorted({a['category'] for a in receipt['imported']}):
        sub=bpy.data.collections.new('Imported_'+family);parent.children.link(sub)
        for o in list(parent.objects):
            item=by_name.get(o.name)
            if not item or item['category']!=family:continue
            sub.objects.link(o);parent.objects.unlink(o)
            o['unity_status']='ImportedAndVerified';o['unity_prefab']=item['prefab_path'];o['unity_guid']=item['prefab_guid']
        bpy.context.view_layer.update()
        lo,hi=bounds(list(sub.objects))
        font=bpy.data.curves.new('Imported_'+family+'_Label','FONT');font.body=family+' - imported';font.size=.8
        label=bpy.data.objects.new(font.name,font);sub.objects.link(label);label.location=(lo.x,hi.y+1,0)
    report['status']='ImportedAndVerified';report['receipt']=receipt
    color_check=WORK/'atlas_color_validation.json'
    if color_check.exists():report['atlas_color_validation']=json.loads(color_check.read_text())
    report['deferred_scope']='Characters, construction including mine structures, wall/fence families and their materials remain deferred. Foliage meshes and opening furniture components remain deferred.'
    text=bpy.data.texts['Unity_Import_Status.json'];text.clear();text.write(json.dumps(report,indent=2))
    note=bpy.data.texts['READ_ME - Download Catalog'];note.clear()
    note.write('ENVIRONMENT IMPORT REVIEW\n\n'+str(receipt['prefab_count'])+' verified Unity prop prefabs, organized under UnityImported_Atlased by functional family. Imported representations are at X=300 metres; deferred source content remains in the original area. Original imported source objects are hidden individually and retained with their original meshes, UVs and materials.\n\nAtlas density: '+report['texel_density']+'. Geometry was not decimated. Existing solid furniture variants were selected; opening components were excluded. Source UVs remain on archived source objects.\n\nSee Unity_Import_Status.json for source IDs, Unity prefab/model/material GUIDs and the exact deferred list. The adjacent DownloadedAssets_ImportStatus.json is the handoff record for future chats.\n\nCharacters, construction, wall/fence families and their materials are deferred. Foliage uses texture/material sources only for future custom meshes. One missing original character texture (Character_Female_01) affects deferred source content only.\n')
    imported_furniture=bpy.data.collections.get('Imported_Furniture')
    meshes=[o for o in imported_furniture.objects if o.type=='MESH']
    lo,hi=bounds(meshes)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.shading.type='SOLID';space.shading.color_type='TEXTURE'
                space.region_3d.view_location=(lo+hi)/2;space.region_3d.view_distance=max((hi-lo).length*.8,10)
                space.region_3d.view_rotation=Vector((0,-1.6,1.8)).to_track_quat('Z','Y')
    bpy.ops.wm.save_as_mainfile(filepath=str(REVIEW),compress=True,relative_remap=False)
    path=REVIEW.with_name('DownloadedAssets_ImportStatus.json')
    prior=json.loads(path.read_text())
    prior.update(updated_local=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).isoformat(),unity_import_status='ImportedAndVerified',imported_into_unity=receipt['imported'],imported_files=receipt['files'],atlas_count=receipt['atlas_count'],deferred_objects=report['deferred'],atlas_plan='Implemented for this approved batch. Deferred families have not been atlased or imported.',workspace_constraint='Runtime assets and permanent import tooling are in the Unity project. Source extraction, review renders and working intermediates remain outside the repository.',review_layout='Imported representations: X=300 metres, grouped by family. Deferred sources: original area. Hidden original imported source geometry is preserved.')
    prior['approved_for_next_import']={}
    prior['atlas_quality_status']='Validated source-UV repack using UV-Packer; original unwraps retained'
    prior['atlas_layouts']=[{k:p[k] for k in ['name','width','height','coverage_percent','density_min','density_max','packing_tool','unwrap'] if k in p} for p in report['atlases']]
    if report.get('atlas_color_validation'):prior['atlas_color_validation']=report['atlas_color_validation']
    path.write_text(json.dumps(prior,indent=2),encoding='utf-8')
    (Path(__file__).parent/'ImportStatus.json').write_text(json.dumps(prior,indent=2),encoding='utf-8')
    print('REVIEW_AND_HANDOFF_UPDATED',receipt['prefab_count'],flush=True)

if __name__=='__main__':
    if '--finalize-review' in __import__('sys').argv:finalize_review()
    else:run()
