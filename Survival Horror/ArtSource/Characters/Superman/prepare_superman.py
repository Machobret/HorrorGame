"""Run with Blender --factory-startup --background --python <this file> -- <source.blend>.

Preserve the player skeleton; fit source arms using a temporary rest-pose rig.
The generated cloth mask travels in FBX UV channel 2, independent of skin weights.
The supplied Simulated group guides dimensions; attachment is fitted for deformation.
"""
import bpy
import hashlib
import json
import math
import pathlib
import shutil
import sys
import uuid
from mathutils import Matrix, Vector

PROJECT = pathlib.Path(__file__).resolve().parents[3]
SOURCE_DIR = pathlib.Path(__file__).resolve().parent
CONTENT = PROJECT / 'Assets/Game/Characters/Superman'
REVIEW = PROJECT / 'Exports/Reviews/Superman'
source = pathlib.Path(sys.argv[sys.argv.index('--')+1])
CONTENT.mkdir(parents=True, exist_ok=True)
REVIEW.mkdir(parents=True, exist_ok=True)
if source.resolve() != (SOURCE_DIR/'Superman_Source.blend').resolve():
    shutil.copy2(source, SOURCE_DIR/'Superman_Source.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
body=bpy.data.objects['Sovereign']
cape=bpy.data.objects['Cape']
assert cape.vertex_groups.get('Simulated'), 'Author Simulated vertex group is required'
original_mask_count=sum(any(g.group==cape.vertex_groups['Simulated'].index and g.weight>0 for g in v.groups) for v in cape.data.vertices)
# Resolve the modeled symmetry before assigning skinning or exporting mask data.
bpy.context.view_layer.objects.active=cape
for mod in list(cape.modifiers):
    assert mod.type=='MIRROR', 'Unexpected source modifier: '+mod.type
    bpy.ops.object.modifier_apply(modifier=mod.name)
mask_group=cape.vertex_groups['Simulated']
mask=[next((g.weight for g in v.groups if g.group==mask_group.index),0.0) for v in cape.data.vertices]
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(PROJECT/'Assets/AssetStore/HorrorEngine/ExampleContent/Character/CharacterPlaceholder.fbx'), use_anim=False)
added=list(set(bpy.data.objects)-before)
target=next(o for o in added if o.type=='ARMATURE')
reference=next(o for o in added if o.type=='MESH')
target.animation_data_clear()
for pb in target.pose.bones: pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()

points=[body.matrix_world@v.co for v in body.data.vertices]
refpoints=[reference.matrix_world@v.co for v in reference.data.vertices]
height=max(v.z for v in refpoints)-min(v.z for v in refpoints)
scale=height/(max(v.z for v in points)-min(v.z for v in points))
center=Vector(((min(v.x for v in points)+max(v.x for v in points))/2,(min(v.y for v in points)+max(v.y for v in points))/2,min(v.z for v in points)))
alignment=Matrix.Scale(scale,4)@Matrix.Rotation(-math.pi/2,4,'Z')@Matrix.Translation(-center)
for obj in (body,cape):
    world=alignment@obj.matrix_world
    for v in obj.data.vertices: v.co=world@v.co
    obj.parent=None
    obj.matrix_world=Matrix.Identity(4)

# Temporary donor skeleton follows the authored arms-down pose. The final skeleton
# retains the exact imported player's rest matrices and bone hierarchy.
donor=target.copy();donor.data=target.data.copy();bpy.context.collection.objects.link(donor)
bpy.ops.object.select_all(action='DESELECT');donor.select_set(True);bpy.context.view_layer.objects.active=donor
bpy.ops.object.mode_set(mode='EDIT')
for side,sign in [('L',1),('R',-1)]:
    def point(x,y,z): return donor.matrix_world.inverted()@Vector((x*sign,y,z))
    shoulder=point(.255,.065,1.60)
    elbow=point(.325,.045,1.32)
    wrist=point(.368,-.075,1.005)
    tip=point(.392,-.135,.88)
    for name,h,t in [('shoulder',donor.data.edit_bones['DEF-shoulder.'+side].head.copy(),shoulder),('upper_arm',shoulder,elbow),('forearm',elbow,wrist),('hand',wrist,tip),('hand.'+side+'_end',tip,tip+(tip-wrist))]:
        bn='DEF-'+name+'.'+side if not name.endswith('_end') else 'DEF-'+name
        b=donor.data.edit_bones[bn];b.use_connect=False;b.head=h;b.tail=t
bpy.ops.object.mode_set(mode='OBJECT')
for bone in donor.data.bones: bone.use_deform=bone.name.startswith('DEF-') and not bone.name.endswith('_end')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);donor.select_set(True);bpy.context.view_layer.objects.active=donor
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
assert all(sum(g.weight for g in v.groups)>1e-5 for v in body.data.vertices), 'Unweighted body vertices'
bpy.context.view_layer.objects.active=body;donor.select_set(False)
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
sys.path.insert(0,str(SOURCE_DIR))
import blender_review

# Only the hanging sheet enters Cloth. Its reference skin follows the chest;
# the body-fitted shoulder attachment is built separately after pose fitting.
cape.vertex_groups.clear()
chest=cape.vertex_groups.new(name='DEF-spine.003')
chest.add(list(range(len(cape.data.vertices))),1,'REPLACE')
cape.parent=donor
modifier=cape.modifiers.new('Player skin','ARMATURE');modifier.object=donor
cape.matrix_world=Matrix.Identity(4)

# Bake the temporary pose into mesh coordinates, then bind those coordinates to
# the untouched canonical skeleton. Do not apply a pose as the target rest pose.
for bone in target.data.bones:
    donor.pose.bones[bone.name].matrix=bone.matrix_local.copy()
    bpy.context.view_layer.update()
for obj in (body,cape):
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh=evaluated.to_mesh()
    coords=[evaluated.matrix_world@v.co for v in evaluated_mesh.vertices]
    assert len(coords)==len(obj.data.vertices)
    evaluated.to_mesh_clear()
    for v,co in zip(obj.data.vertices,coords): v.co=co
    obj.parent=None;obj.matrix_world=Matrix.Identity(4)
    for mod in list(obj.modifiers): obj.modifiers.remove(mod)
    obj.parent=target
    modifier=obj.modifiers.new('Player skin','ARMATURE');modifier.object=target

# Match the existing weapon grip centers while keeping the body and skeleton fixed.
hand_corrections={}
def hand_center(obj,name):
    g=obj.vertex_groups.get(name)
    ids=[v.index for v in obj.data.vertices if any(e.group==g.index and e.weight>.5 for e in v.groups)]
    unique={tuple(round(c,5) for c in obj.matrix_world@obj.data.vertices[i].co):obj.matrix_world@obj.data.vertices[i].co for i in ids}
    assert unique, 'No dominant hand vertices: '+name
    return ids,sum(unique.values(),Vector())/len(unique)
for side,sign in [('L',1),('R',-1)]:
    ids,center=hand_center(body,'DEF-hand.'+side)
    _,goal=hand_center(reference,'DEF-hand.'+side)
    delta=goal-center
    hand_corrections[side]=list(delta)
    elbow=target.matrix_world@target.data.bones['DEF-forearm.'+side].head_local
    axis=(center-elbow).normalized();length=(center-elbow).length
    for v in body.data.vertices:
        if v.index in ids: factor=1.0
        elif v.co.x*sign>0:
            w=sum(e.weight for e in v.groups if body.vertex_groups[e.group].name in ['DEF-forearm.'+side,'DEF-hand.'+side])
            factor=max(0,min(1,((v.co-elbow).dot(axis)/length-.12)/.7))
            factor=factor*factor*(3-2*factor)*min(1,w*2)
        else: factor=0
        v.co+=delta*factor

body.name='Superman_Body';body.data.name='Superman_Body'
cape.name='Superman_Cape';cape.data.name='Superman_Cape'
import fit_attachment
attachment,mask=fit_attachment.build(body,cape,mask)
clearance_report={'method':'Body-fitted skinned attachment; hanging sheet starts behind the arms.'}
# Keep the generated attachment mask editable and transport it safely through FBX.
uv=cape.data.uv_layers.new(name='ClothMask')
for loop in cape.data.loops: uv.data[loop.index].uv=(mask[loop.vertex_index],0)
cape['cloth_mask']='Simulated group; exported in UV channel 2 X. Zero = chest attachment.'
for obj in (body,cape):
    obj.data.update()
    for material in obj.data.materials:
        if material and material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image:
                    image=node.image
                    filename='Superman_Albedo.png' if obj==body else 'Superman_CapeAlbedo.png'
                    image.filepath_raw=str(CONTENT/filename);image.file_format='PNG';image.save();image.pack()
                    node.interpolation='Closest'
material_report={}
for obj in (body,cape):
    material=obj.data.materials[0]
    bs=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    material_report[obj.name]={'material':material.name,'base_color':list(bs.inputs['Base Color'].default_value) if bs else [1,1,1,1]}
(SOURCE_DIR/'materials.json').write_text(json.dumps(material_report,indent=2))
for obj in added:
    if obj!=target:bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.objects.remove(donor,do_unlink=True)
target.name='rig'
body['rigging']='CharacterPlaceholder exact skeleton; fitted source rest pose, max four bone weights; weapon hand centers aligned.'
bpy.ops.object.select_all(action='DESELECT')
for obj in (target,body,cape,attachment):obj.select_set(True)
bpy.context.view_layer.objects.active=target
bpy.context.preferences.filepaths.save_version=0
blender_review.configure()
staged=REVIEW/('Superman_'+uuid.uuid4().hex+'.blend')
bpy.ops.wm.save_as_mainfile(filepath=str(staged))
shutil.copyfile(staged,SOURCE_DIR/'Superman_PlayerRig.blend')
staged.unlink()
bpy.ops.export_scene.fbx(filepath=str(CONTENT/'Superman_PlayerRig.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_armature_deform_only=False,path_mode='STRIP',embed_textures=False)
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'source_mask_vertices':original_mask_count,'body_vertices':len(body.data.vertices),'cape_vertices':len(mask),'simulated_vertices':sum(w>0 for w in mask),'pinned_vertices':sum(w==0 for w in mask),'mask_weights':sorted(set(mask)),'hand_corrections':hand_corrections,'scale':scale,'cape_clearance':clearance_report}
(REVIEW/'rig_report.json').write_text(json.dumps(report,indent=2))
print('SUPERMAN_RIGGED',json.dumps(report))
