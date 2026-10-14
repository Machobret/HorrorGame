"""Correct carton UVs and render its pickup image without rebuilding weapons."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
folder=ROOT/'Assets/Game/Weapons/Models/RevolverAmmo'
bpy.ops.wm.read_factory_settings(use_empty=True)
image=bpy.data.images.load(str(folder/'RevolverAmmo_Albedo.png'))
material=bpy.data.materials.new('MagnumBox');material.use_nodes=True
texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
texture.interpolation='Closest'
bsdf=material.node_tree.nodes.get('Principled BSDF')
material.node_tree.links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=.85
paper=bpy.data.materials.new('MagnumCarton');paper.use_nodes=True
paper.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.16,.075,.035,1)
paper.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.95
bpy.ops.mesh.primitive_cube_add(size=1)
obj=bpy.context.object;obj.name='MagnumAmmoBox';obj.scale=(.11,.075,.04)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
uv=obj.data.uv_layers.active
for face in obj.data.polygons:
    face.material_index = 0 if face.normal.z>.5 else 1
    for loop in face.loop_indices:
        p=obj.data.vertices[obj.data.loops[loop].vertex_index].co
        if face.normal.z>.5:
            # A single upright lid label. Camera faces the front (-Y).
            coord=(p.x/.11+.5,p.y/.075+.5)
        else:
            # Worn cardboard and red edging from the atlas margin. Avoid
            # stretching the lid lettering onto the narrow carton sides.
            u=p.x/.11+.5 if abs(face.normal.y)>.5 else p.y/.075+.5
            v=p.z/.04+.5
            coord=(.10+.8*u,.015+.065*v)
        uv.data[loop].uv=coord
obj.data.materials.append(material)
obj.data.materials.append(paper)
bevel=obj.modifiers.new('Carton edges','BEVEL');bevel.width=.0007;bevel.segments=1
bpy.ops.object.modifier_apply(modifier=bevel.name)
bpy.ops.export_scene.fbx(filepath=str(folder/'RevolverAmmo.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,path_mode='STRIP')

# A raised front view makes the lid readable instead of presenting it sideways.
bpy.ops.object.camera_add(location=(.16,-.23,.25))
camera=bpy.context.object;camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=.15
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.15,.15,.15)
for location,energy in [((.15,-.2,.35),4),((-.2,.1,.2),2)]:
    bpy.ops.object.light_add(type='AREA',location=location)
    light=bpy.context.object;light.data.energy=energy;light.data.size=.2
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(folder/'RevolverAmmo_Icon.png')
bpy.ops.render.render(write_still=True)
print('MAGNUM_AMMO_VISUAL_OK')
