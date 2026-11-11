"""Blender CLI recipe for one static old cash register; no gameplay scripts."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore'
REVIEW=ROOT/'Exports/Reviews/NeighborhoodStore';REVIEW.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('Register_Atlas');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.8
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'Textures/CashRegister_Atlas.png'));tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
parts=[]
def mesh(name,verts,faces,tile=0,sub=None):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();data.materials.append(mat)
    uv=data.uv_layers.new(name='UVMap');u=tile%4/4;v=1-(tile//4+1)/2
    a,b,c,d=sub or (0,0,1,1)
    for face in data.polygons:
        for loop,coord in zip(face.loop_indices,[(a,b),(c,b),(c,d),(a,d)]):uv.data[loop].uv=(u+(.005+.99*coord[0])/4,v+(.005+.99*coord[1])/2)
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);parts.append(ob);return ob
def box(name,pos,size,tile=0,rot=0,sub=None):
    x,y,z=[n/2 for n in size];verts=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    ob=mesh(name,verts,[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)],tile,sub);ob.location=pos;ob.rotation_euler.x=rot;return ob
box('Drawer base',(0,0,.055),(.42,.43,.08),0)
box('Drawer seam',(0,-.217,.054),(.404,.006,.063),1)
box('Drawer face',(0,-.222,.054),(.392,.009,.051),0)
box('Finger pull',(0,-.231,.063),(.09,.013,.009),1)
for x in (-.165,.165):
    for y in (-.16,.16):box('Rubber foot',(x,y,.009),(.045,.045,.018),1)
# Sloped housing, with the keyboard raised toward the rear.
mesh('Sloped register body',[(-.19,-.18,.096),(.19,-.18,.096),(.19,.18,.096),(-.19,.18,.096),(-.19,-.18,.13),(.19,-.18,.13),(.19,.18,.27),(-.19,.18,.27)],[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)],0)
angle=math.atan2(.14,.36)
box('Keyboard recess',(.025,-.05,.19),(.27,.24,.008),1,angle)
for row in range(4):
    for col in range(4):
        index=row*4+col;x=-.07+col*.045;y=-.135+row*.05;z=.13+(y+.18)*.14/.36+.012
        keyIndex=(3-row)*4+col;cx=keyIndex%4;cy=keyIndex//4
        box('Key_'+str(index),(x,y,z),(.034,.037,.016),2,angle,(cx/4,1-(cy+1)/4,(cx+1)/4,1-cy/4))
box('Total key',(.135,-.045,.215),(.042,.125,.022),3,angle)
box('Receipt slot',(-.145,-.018,.2),(.065,.013,.008),1,angle)
box('Receipt paper',(-.145,-.033,.226),(.052,.105,.002),6,.38)
box('Printer cover',(-.145,.074,.24),(.075,.105,.04),0,angle)
box('Back ventilation',(0,.184,.173),(.28,.004,.105),5)
box('Display pedestal',(0,.13,.28),(.075,.055,.07),1)
box('Display housing',(0,.13,.33),(.32,.065,.09),0)
box('Display recess',(0,.095,.33),(.287,.006,.065),1)
# Isolate the readable face so the digits are not stretched across other faces.
mesh('Green numeric display',[(-.137,.091,.305),(.137,.091,.305),(.137,.091,.354),(-.137,.091,.354)],[(0,1,2,3)],4)
mesh('Maker plate',[(-.183,-.183,.103),(-.095,-.183,.103),(-.095,-.183,.129),(-.183,-.183,.129)],[(0,1,2,3)],7)
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();register=bpy.context.object;register.name='CashRegister'
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/CashRegister.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
size=register.dimensions;triangles=sum(len(p.vertices)-2 for p in register.data.polygons)
(OUT/'CashRegister_Manifest.json').write_text(json.dumps({'materials':[{'name':'Register_Atlas','texture':'Assets/Game/Environment/NeighborhoodStore/Textures/CashRegister_Atlas.png','roughness':.8}], 'size':[size.x,size.z,size.y],'triangles':triangles},indent=2))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=768;scene.render.resolution_y=576;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.world.color=(.18,.18,.18)
bpy.ops.mesh.primitive_plane_add(size=20);bpy.context.object.location.z=-.001
def aim(o):o.rotation_euler=(Vector((0,0,.16))-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power in [('Key',(-1,-2,3),220),('Fill',(2,1,2),130)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.size=3;ob=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(ob);ob.location=pos;aim(ob)
bpy.ops.object.camera_add(location=(.65,-1,.72));cam=bpy.context.object;aim(cam);cam.data.type='ORTHO';cam.data.ortho_scale=.75;scene.camera=cam
scene.view_settings.view_transform='AgX';bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('CashRegister.blend')))
scene.render.filepath=str(REVIEW/'CashRegister.png');bpy.ops.render.render(write_still=True)
print('CASH_REGISTER_MODEL_OK',triangles,list(size))
