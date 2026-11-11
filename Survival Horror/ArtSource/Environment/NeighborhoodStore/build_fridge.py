"""One stocked single-door shop fridge, exported independently of review lighting."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore';REVIEW=ROOT/'Exports/Reviews/NeighborhoodStore'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
atlas=bpy.data.images.load(str(OUT/'Textures/DrinksFridge_Atlas.png'))
materials=[]
for name,alpha in [('Fridge_Atlas',1),('Fridge_Glass',.12)]:
    mat=bpy.data.materials.new(name);mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF')
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value=.75 if alpha==1 else .25;bs.inputs['Alpha'].default_value=alpha
    if alpha<1:mat.surface_render_method='DITHERED'
    materials.append(mat)
parts=[];glassParts=[]
def mesh(name,verts,faces,tile=0,glass=False,uvs=None):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();data.materials.append(materials[1 if glass else 0]);uv=data.uv_layers.new(name='UVMap')
    baseU=tile%4/4;baseV=1-(tile//4+1)/2
    for fi,p in enumerate(data.polygons):
        coords=uvs[fi] if uvs else [(0,0),(1,0),(1,1),(0,1)]
        for loop,co in zip(p.loop_indices,coords):uv.data[loop].uv=(baseU+(.005+.99*co[0])/4,baseV+(.005+.99*co[1])/2)
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);(glassParts if glass else parts).append(ob);return ob
def box(name,pos,size,tile=0,glass=False):
    x,y,z=[s/2 for s in size];ob=mesh(name,[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)],tile,glass);ob.location=pos;return ob
def drink(name,pos,bottle=False):
    profiles=[(0,.03),(.012,.032),(.13,.032),(.17,.015),(.205,.014),(.21,.017),(.226,.017)] if bottle else [(0,.033),(.006,.035),(.116,.035),(.122,.032)]
    verts=[(math.cos(i*math.tau/8)*r,math.sin(i*math.tau/8)*r,z) for z,r in profiles for i in range(8)];faces=[];uvs=[]
    for j in range(len(profiles)-1):
        for i in range(8):faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i));uvs.append([(i/8,j/(len(profiles)-1)),((i+1)/8,j/(len(profiles)-1)),((i+1)/8,(j+1)/(len(profiles)-1)),(i/8,(j+1)/(len(profiles)-1))])
    ob=mesh(name,verts,faces,5 if bottle else 4,uvs=uvs);ob.location=pos
    # Opaque closed top and bottom, with metal/plastic cap on the appropriate end.
    for z,r in (profiles[0],profiles[-1]):
        vs=[(0,0,z)]+[(math.cos(i*math.tau/8)*r,math.sin(i*math.tau/8)*r,z) for i in range(8)]
        fs=[(0,i+1,(i+1)%8+1) if z>0 else (0,(i+1)%8+1,i+1) for i in range(8)]
        uv3=[[(.5,.5),(.5+math.cos(i*math.tau/8)*.5,.5+math.sin(i*math.tau/8)*.5),(.5+math.cos((i+1)*math.tau/8)*.5,.5+math.sin((i+1)*math.tau/8)*.5)] for i in range(8)]
        cap=mesh(name+'_cap',vs,fs,1 if bottle else 0,uvs=uv3);cap.location=pos
for x in (-.31,.31):
    for y in (-.25,.25):box('Foot',(x,y,.035),(.1,.1,.07),3)
# End the cream shell below the canopy instead of letting their side faces occupy the same plane.
shell_bottom=.07;shell_top=1.865
assert shell_top < 1.96-.18/2
box('Rear housing',(0,.322,(shell_bottom+shell_top)/2),(.82,.056,shell_top-shell_bottom),0)
for x in (-.387,.387):box('Insulated side',(x,0,(shell_bottom+shell_top)/2),(.046,.64,shell_top-shell_bottom),0)
box('Compressor base',(0,0,.18),(.82,.64,.24),0)
box('Bottom vent',(0,-.327,.18),(.68,.015,.15),2)
box('Top canopy',(0,0,1.96),(.82,.66,.18),1)
box('Header border',(0,-.339,1.96),(.75,.018,.14),3)
# Header face has independent UVs, not lettering on the sides/back.
mesh('Cold drinks sign',[(-.35,-.35,1.905),(.35,-.35,1.905),(.35,-.35,2.015),(-.35,-.35,2.015)],[(0,1,2,3)],6)
box('Interior back',(0,.282,1.07),(.72,.012,1.51),0)
for x in (-.335,.335):box('Shelf track',(x,.27,1.075),(.012,.016,1.46),2)
for shelf,z in enumerate((.345,.69,1.035,1.38)):
    for y in (-.25,.23):box('Shelf edge',(0,y,z),(.71,.012,.015),1)
    for x in (-.35,.35):box('Shelf side',(x,-.01,z),(.012,.48,.015),1)
    for n in range(13):box('Shelf wire',(-.33+n*.055,-.01,z),(.005,.48,.005),1)
    # Sparse stock: one row of nine bottles against the back of every shelf.
    for row in range(1):
        for column in range(9):
            x=-.30+column*.075;y=.225
            assert abs(x)+.032 < .35 and y+.032 < .27 and y-.032 > -.30
            drink('Water bottle',(x,y,z+.012),True)
            # Scale bottle height only, keeping shelf clearance and packing width unchanged.
            for piece in parts[-3:]:piece.scale.z=1.28
for x in (-.37,.37):box('Door seal',(x,-.336,1.075),(.035,.027,1.56),3)
for x in (-.367,.367):box('Green door upright',(x,-.36,1.075),(.029,.03,1.56),1)
for z in (.3,1.85):box('Green door crosspiece',(0,-.36,z),(.76,.03,.03),1)
box('Door glass',(0,-.36,1.075),(.70,.005,1.49),7,True)
for z in (.83,1.22):box('Handle standoff',(.302,-.393,z),(.02,.04,.02),1)
box('Pull handle',(.302,-.419,1.025),(.022,.025,.43),1)
for z in (.5,1.62):box('Hinge',(-.391,-.354,z),(.025,.04,.065),2)
def join(items,name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in items:ob.select_set(True)
    bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);return ob
body=join(parts,'DrinksFridge_Body');glass=join(glassParts,'DrinksFridge_Glass')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);glass.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/DrinksFridge.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
allcorners=[ob.matrix_world@Vector(c) for ob in (body,glass) for c in ob.bound_box];size=[max(v[i] for v in allcorners)-min(v[i] for v in allcorners) for i in (0,2,1)]
triangles=sum(len(p.vertices)-2 for ob in (body,glass) for p in ob.data.polygons)
(OUT/'DrinksFridge_Manifest.json').write_text(json.dumps({'size':size,'triangles':triangles,'stockCount':36,'bottleCount':36,'shelves':4,'bottlesAcross':9,'rowsDeep':1},indent=2))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=768;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.world.color=(.18,.18,.18)
bpy.ops.mesh.primitive_plane_add(size=20);bpy.context.object.location.z=-.002
def aim(o):o.rotation_euler=(Vector((0,0,1.04))-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power in [('Key',(-2,-3,4),350),('Fill',(2,-1,3),220),('Rim',(1,2,3),180)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=3;ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);ob.location=pos;aim(ob)
bpy.ops.object.camera_add(location=(2.4,-4,2.6));cam=bpy.context.object;aim(cam);cam.data.type='ORTHO';cam.data.ortho_scale=2.55;scene.camera=cam
scene.view_settings.view_transform='AgX';bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('DrinksFridge.blend')))
scene.render.filepath=str(REVIEW/'DrinksFridge.png');bpy.ops.render.render(write_still=True)
print('DRINKS_FRIDGE_MODEL_OK',size,triangles)
