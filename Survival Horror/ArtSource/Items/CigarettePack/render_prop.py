"""blender --background --python ArtSource/Items/CigarettePack/render_prop.py"""
import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Game/Items/CigarettePack'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
# Individual face vertices keep the carton corners crisp; every face has its own panel.
x,y,z=.03,.0125,.045
faces=[ [(-x,-y,-z),(x,-y,-z),(x,-y,z),(-x,-y,z)],
        [(x,-y,-z),(x,y,-z),(x,y,z),(x,-y,z)],
        [(x,y,-z),(-x,y,-z),(-x,y,z),(x,y,z)],
        [(-x,y,-z),(-x,-y,-z),(-x,-y,z),(-x,y,z)],
        [(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],
        [(-x,y,-z),(x,y,-z),(x,-y,-z),(-x,-y,-z)] ]
mesh=bpy.data.meshes.new('CigarettePack_Carton')
mesh.from_pydata([p for f in faces for p in f],[],[tuple(range(i*4,i*4+4)) for i in range(6)])
mesh.update(); uv=mesh.uv_layers.new(name='Packaging')
for i,poly in enumerate(mesh.polygons):
    # Insets prevent neighboring atlas panels bleeding into carton edges.
    u=(i%3)/3; v=1-(i//3+1)/2
    corners=[(u+.5/384,v+.5/256),(u+1/3-.5/384,v+.5/256),
             (u+1/3-.5/384,v+.5-.5/256),(u+.5/384,v+.5-.5/256)]
    for loop,coord in zip(poly.loop_indices,corners): uv.data[loop].uv=coord
obj=bpy.data.objects.new('CigarettePack',mesh); bpy.context.collection.objects.link(obj)
mat=bpy.data.materials.new('CigarettePack_Paper'); mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.85
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(OUT/'CigarettePack_Albedo.png')); tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']); mesh.materials.append(mat)
bpy.context.view_layer.objects.active=obj; obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'CigarettePack.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.film_transparent=True; scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGBA'
scene.world.color=(.12,.12,.12)
def aim(o,p=(0,0,0)): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.135,-.24,.12)); cam=bpy.context.object; aim(cam); cam.data.type='ORTHO'; cam.data.ortho_scale=.137; scene.camera=cam
for name,loc,power,color,size in [('Key',(-.12,-.18,.2),9,(1,.88,.71),.18),('Fill',(.15,-.08,.08),3,(.68,.79,1),.15),('Rim',(.05,.08,.15),8,(1,.45,.3),.1)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=power*.22; data.color=color; data.shape='DISK'; data.size=size
    light=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(light); light.location=loc; aim(light)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.color_depth='8'
scene.render.filepath=str(OUT/'CigarettePack_PickupIcon.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('CigarettePack.blend')))
bpy.ops.render.render(write_still=True)
