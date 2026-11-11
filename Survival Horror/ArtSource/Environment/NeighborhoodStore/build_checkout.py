"""One worn neighborhood-shop counter, with original geometry and shelf-pack surface references."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore'
REVIEW=ROOT/'Exports/Reviews/NeighborhoodStore';REVIEW.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
materials={}
definitions=[('Checkout_Wood','Assets/Game/Environment/PSXShelvesProps/Textures/b_oak_dirty.png',.85),
 ('Checkout_Backing','Assets/Game/Environment/PSXShelvesProps/Textures/b_back_dirty.png',.9),
 ('Checkout_Metal','Assets/Game/Environment/PSXShelvesProps/Textures/b_steel_grey_dirty.png',.65),
 ('Checkout_Laminate','Assets/Game/Environment/NeighborhoodStore/Textures/CheckoutCounter_Laminate.png',.78),
 ('Checkout_Sign','Assets/Game/Environment/NeighborhoodStore/Textures/CheckoutCounter_Sign.png',.9)]
for name,path,roughness in definitions:
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=roughness
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(ROOT/path));node.interpolation='Closest'
    mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color']);materials[name]=mat
parts=[]
def box(name,location,size,material='Checkout_Wood',bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(materials[material])
    # Blender's default cube cross unwrap only uses a quarter of each texture; map every face fully.
    uv=o.data.uv_layers.active
    for face in o.data.polygons:
        for loop,coord in zip(face.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):uv.data[loop].uv=coord
    if bevel:
        m=o.modifiers.new('Small worn edge','BEVEL');m.width=bevel;m.segments=1
        bpy.ops.object.modifier_apply(modifier=m.name)
    parts.append(o);return o
def pin(name,loc,radius,depth,rot):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=radius,depth=depth,location=loc,rotation=rot)
    o=bpy.context.object;o.name=name;o.data.materials.append(materials['Checkout_Metal']);parts.append(o);return o
# Counter is open from the cashier side. Feet establish an exact floor-level pivot.
for x in (-.77,.77):
    for y in (-.24,.24):box('Foot',(x,y,.025),(.1,.1,.05),'Checkout_Backing')
box('Bottom plinth',(0,0,.085),(1.69,.59,.075),bevel=.008)
box('Front apron',(0,-.275,.49),(1.68,.045,.79))
for x in (-.82,.82):box('Side frame',(x,0,.49),(.045,.59,.79),bevel=.005)
box('Upper crossbar',(0,.255,.86),(1.65,.045,.07))
# Two recessed wooden front panels, with visible stiles and rails.
for x in (-.41,.41):box('Recessed front panel',(x,-.304,.48),(.715,.018,.58),'Checkout_Wood',.004)
for x in (-.8,0,.8):box('Panel stile',(x,-.323,.49),(.04,.028,.7))
for z in (.16,.82):box('Panel rail',(0,-.324,z),(1.64,.03,.045))
box('Metal kick plate',(0,-.334,.17),(1.62,.012,.075),'Checkout_Metal')
# Cashier storage on the left; right bay remains open for legs/bags.
box('Cabinet divider',(-.14,.015,.44),(.035,.52,.64))
for z in (.2,.44):box('Storage shelf',(-.475,.015,z),(.65,.53,.028),'Checkout_Backing')
box('Drawer box',(-.475,.025,.73),(.61,.45,.13),'Checkout_Backing')
box('Drawer front',(-.475,.278,.745),(.64,.035,.16),'Checkout_Wood',.005)
for x in (-.54,-.41):pin('Handle mount',(x,.304,.75),.009,.025,(math.pi/2,0,0))
pin('Drawer handle',(-.475,.317,.75),.009,.15,(0,math.pi/2,0))
# Chamfered top silhouette. Its top has a dedicated surface UV so the ring/scratches are placed once.
w,d,c=.925,.36,.045
ring=[(-w+c,-d), (w-c,-d),(w,-d+c),(w,d-c),(w-c,d),(-w+c,d),(-w,d-c),(-w,-d+c)]
verts=[(x,y,z) for z in (.9,.95) for x,y in ring]
faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
mesh=bpy.data.meshes.new('Chamfered laminate top');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('Countertop',mesh);bpy.context.collection.objects.link(o);parts.append(o)
mesh.materials.append(materials['Checkout_Laminate']);mesh.materials.append(materials['Checkout_Metal'])
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    p.material_index=0 if p.index==1 else 1
    for loop in p.loop_indices:
        v=mesh.vertices[mesh.loops[loop].vertex_index].co
        if p.index==1:uv.data[loop].uv=((v.x+w)/(2*w),(v.y+d)/(2*d))
        else:uv.data[loop].uv=(v.x*2+v.y*2,v.z*5)
# Preserve all prepared UVs/materials while joining the static prop into one low-poly mesh.
bpy.ops.object.select_all(action='DESELECT')
for p in parts:p.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();counter=bpy.context.object;counter.name='CheckoutCounter'
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
(OUT/'Models').mkdir(exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/CheckoutCounter.fbx'),use_selection=True,object_types={'MESH'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
triangles=sum(len(p.vertices)-2 for p in counter.data.polygons)
(OUT/'CheckoutCounter_Manifest.json').write_text(json.dumps({'materials':[{'name':n,'texture':p,'roughness':r} for n,p,r in definitions],
    'size':[1.85,.95,.72],'triangles':triangles},indent=2))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=768;scene.render.resolution_y=576;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world.color=(.18,.18,.18)
# Neutral studio floor is review-only, never exported into the prop.
bpy.ops.mesh.primitive_plane_add(size=20);floor=bpy.context.object;floor.name='Review floor';floor.location.z=-.004
floorMat=bpy.data.materials.new('Review floor');floorMat.diffuse_color=(.055,.065,.058,1);floor.data.materials.append(floorMat)
def aim(o,target=(0,0,.42)):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-2,-3,4),450,4),('Fill',(3,1,2),230,3),('Rim',(-2,2,3),320,2)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
    ob=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(ob);ob.location=loc;aim(ob)
bpy.ops.object.camera_add(location=(2.7,-3.8,2.7));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.65;aim(cam);scene.camera=cam
scene.view_settings.view_transform='AgX'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('CheckoutCounter.blend')))
scene.render.filepath=str(REVIEW/'CheckoutCounter_Front.png');bpy.ops.render.render(write_still=True)
cam.location=(2.7,3.8,2.6);aim(cam);scene.render.filepath=str(REVIEW/'CheckoutCounter_Back.png');bpy.ops.render.render(write_still=True)
print('CHECKOUT_MODEL_OK triangles='+str(triangles))
