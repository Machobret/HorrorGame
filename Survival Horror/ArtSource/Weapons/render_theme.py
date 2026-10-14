"""Render existing weapon meshes without rebuilding their FBXs or gameplay prefabs.

Run with Blender --background --python render_theme.py -- [--draft]
"""
from pathlib import Path
import sys, math, shutil
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'Assets/Game/Weapons/Models'
OUTPUT = ROOT / 'ArtSource/Weapons/TextureTheme'
DRAFT = '--draft' in sys.argv
GENERATED = {
    'HuntingRifleText.png': OUTPUT / 'HuntingRifle_Atlas_Source.png',
    'Flaregun_Text_240.png': OUTPUT / 'FlareGun_Atlas_Source.png',
}

if '--apply' in sys.argv:
    # Resize generated masters for delivery; keep the original aspect ratio and
    # existing Unity texture paths/GUIDs. No gameplay assets are rebuilt.
    applied = OUTPUT / 'Applied'
    applied.mkdir(parents=True, exist_ok=True)
    for filename, size, weapons in [
        ('HuntingRifleText.png', (800, 446), ('HuntingRifle',)),
        ('Flaregun_Text_240.png', (480, 480), ('FlareGun',)),
    ]:
        image = bpy.data.images.load(str(GENERATED[filename]), check_existing=False)
        image.scale(*size)
        image.file_format = 'PNG'
        image.filepath_raw = str(applied / filename)
        image.save()
        for name in weapons:
            shutil.copy2(applied / filename, MODELS / name / filename)

def render(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder = MODELS / name
    bpy.ops.import_scene.fbx(filepath=str(folder / (name + '.fbx')))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for obj in meshes:
        uv = obj.data.uv_layers.active
        if uv:
            print('UV_BOUNDS', name, obj.name, [(min(v.uv[i] for v in uv.data), max(v.uv[i] for v in uv.data)) for i in (0, 1)])
    for obj in meshes:
        for material in obj.data.materials:
            for node in material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    filename = ('ScopeText.jpg' if '067' in material.name else 'HuntingRifleText.png') if name == 'HuntingRifle' else 'FlareAmmo_Albedo.png' if name == 'FlareAmmo' else 'Flaregun_Text_240.png'
                    path = GENERATED.get(filename, folder / filename) if DRAFT else folder / filename
                    node.image = bpy.data.images.load(str(path), check_existing=True)
                    if not DRAFT:
                        size = (512, 256) if filename == 'HuntingRifleText.png' else (256, 256) if filename in ('Flaregun_Text_240.png', 'FlareAmmo_Albedo.png') else (256, 128)
                        node.image.scale(*size)
                    node.interpolation = 'Closest'
                if node.type == 'BSDF_PRINCIPLED':
                    node.inputs['Roughness'].default_value = .8
                    node.inputs['Metallic'].default_value = 0
                    # FBX import wires albedo RGB into alpha. Unity's weapon
                    # materials are opaque; remove that link for faithful icons.
                    for link in list(node.inputs['Alpha'].links):
                        material.node_tree.links.remove(link)
                    node.inputs['Alpha'].default_value = 1
    for material in {m for obj in meshes for m in obj.data.materials}:
        # Match the template's unlit/baked material rather than introducing a
        # second lighting pass. This also supports the double-sided ammo mesh.
        nodes = material.node_tree.nodes
        principled = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
        output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
        emission = nodes.new('ShaderNodeEmission')
        color = principled.inputs['Base Color']
        if color.is_linked:
            material.node_tree.links.new(color.links[0].from_socket, emission.inputs['Color'])
        else:
            emission.inputs['Color'].default_value = color.default_value
        material.node_tree.links.new(emission.outputs[0], output.inputs['Surface'])
    points = [obj.matrix_world @ Vector(v) for obj in meshes for v in obj.bound_box]
    center = Vector([(min(p[i] for p in points) + max(p[i] for p in points)) * .5 for i in range(3)])
    radius = max((p - center).length for p in points)
    angle = (1.2, -1.8, 2.3) if name == 'FlareAmmo' else (1.5, -.35, .8)
    bpy.ops.object.camera_add(location=center + Vector(angle) * radius)
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    if name == 'HuntingRifle':
        camera.rotation_euler.rotate_axis('Z', math.radians(35))
    projected = [camera.rotation_euler.to_quaternion().inverted() @ (p - center) for p in points]
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = max(max(p[i] for p in projected) - min(p[i] for p in projected) for i in (0, 1)) * 1.15
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 32
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = -.6
    scene.render.film_transparent = True
    scene.world = bpy.data.worlds.new('World')
    scene.world.color = (.3, .3, .3)
    for location, power in [((1, -1, 3), 65), ((-1, 1, 2), 35)]:
        bpy.ops.object.light_add(type='AREA', location=center + Vector(location) * max(radius, .3))
        light = bpy.context.object
        light.data.energy = power
        light.data.shape = 'DISK'
        light.data.size = radius * 3
        light.rotation_euler = (center - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.image_settings.file_format = 'PNG'
    destination = OUTPUT / (name + '_Draft.png') if DRAFT else folder / (name + '_Icon.png')
    destination.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(destination)
    bpy.ops.render.render(write_still=True)
    print('THEME_RENDER_OK', name, destination)

for weapon in (('FlareAmmo',) if '--only-flare-ammo' in sys.argv else ('HuntingRifle', 'FlareGun', 'FlareAmmo')):
    render(weapon)

if not DRAFT and '--only-flare-ammo' not in sys.argv:
    import runpy
    runpy.run_path(str(ROOT / 'ArtSource/Weapons/render_pickup_icons.py'), run_name='__main__')
