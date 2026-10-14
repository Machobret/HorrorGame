"""Approved horror-lit thumbnails using the existing weapon meshes."""
from pathlib import Path
import bpy, math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];MODELS=ROOT/'Assets/Game/Weapons/Models'
for name in ('Revolver','HuntingRifle','FlareGun'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder=MODELS/name
    bpy.ops.import_scene.fbx(filepath=str(folder/(name+'.fbx')))
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    for mat in {m for o in meshes for m in o.data.materials}:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE':
                filename=('ScopeText.jpg' if '067' in mat.name else 'HuntingRifleText.png') if name=='HuntingRifle' else ('cylinderRefV2.jpg' if '034' in mat.name else 'ColtPython.png') if name=='Revolver' else 'Flaregun_Text_240.png'
                node.image=bpy.data.images.load(str(folder/filename),check_existing=True);ratio=min(1,128/max(node.image.size));node.image.scale(max(1,round(node.image.size[0]*ratio)),max(1,round(node.image.size[1]*ratio)));node.interpolation='Closest'
            elif node.type=='BSDF_PRINCIPLED':
                for link in list(node.inputs['Alpha'].links):mat.node_tree.links.remove(link)
                node.inputs['Alpha'].default_value=1
                node.inputs['Metallic'].default_value=.15
                node.inputs['Roughness'].default_value=.65
    points=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
    center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)])
    radius=max((p-center).length for p in points)
    bpy.ops.object.camera_add(location=center+Vector((2,-1,.35))*radius)
    camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.rotation_euler.rotate_axis('Z',math.radians(45 if name=='HuntingRifle' else 30))
    projected=[camera.rotation_euler.to_quaternion().inverted()@(p-center) for p in points]
    camera.data.type='ORTHO';camera.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.12
    scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=64
    scene.render.resolution_x=scene.render.resolution_y=1024;scene.render.resolution_percentage=100
    scene.render.film_transparent=True;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.2
    scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.08,.08,.08)
    for location,power,size,color in [((1,-1,3),125,1,(1,.65,.35)),((2,0,.5),45,2,(.75,.85,1)),((1,2,2),230,.7,(.55,.75,1))]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*radius)
        lamp=bpy.context.object;lamp.data.energy=power*radius**2;lamp.data.size=size*radius;lamp.data.color=color
        lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/(name+'_Icon.png'))
    bpy.ops.render.render(write_still=True)
    print('PICKUP_ICON_OK',name)
