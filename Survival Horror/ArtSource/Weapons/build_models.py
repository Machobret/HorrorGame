import bpy, os, math, shutil
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
DOWNLOAD = 'C:/Users/Mangis/Downloads/Assets/Guns'
def build(name, source, scale, grip, exclude, textures):
    textures = [os.path.join(ROOT, 'ArtSource/Weapons/TextureTheme/Applied', os.path.basename(tex))
                if os.path.isfile(os.path.join(ROOT, 'ArtSource/Weapons/TextureTheme/Applied', os.path.basename(tex))) else tex
                for tex in textures]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.name not in exclude]
    for o in list(bpy.data.objects):
        if o not in meshes: bpy.data.objects.remove(o, do_unlink=True)
    for o in meshes:
        world = o.matrix_world.copy()
        for v in o.data.vertices:
            p = world @ v.co
            v.co = (p.y*scale, -(p.x-grip[0])*scale, (p.z-grip[1])*scale)
        o.matrix_world.identity()
        for m in o.data.materials:
            for node in m.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    filename = os.path.basename(node.image.filepath.replace('\\','/'))
                    for tex in textures:
                        if os.path.basename(tex).lower() == filename.lower():
                            node.image = bpy.data.images.load(tex, check_existing=True)
            for node in m.node_tree.nodes:
                if node.type == 'BSDF_PRINCIPLED':
                    node.inputs['Roughness'].default_value = .85
    dest = os.path.join(ROOT,'Assets/Game/Weapons/Models',name)
    os.makedirs(dest,exist_ok=True)
    # Preserve the approved texture theme when geometry is rebuilt.
    for tex in textures:
        themed = os.path.join(ROOT, 'ArtSource/Weapons/TextureTheme/Applied', os.path.basename(tex))
        shutil.copy2(themed if os.path.isfile(themed) else tex, dest)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(dest,name+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='STRIP')
    pts=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    center=Vector([(min(p[i] for p in pts)+max(p[i] for p in pts))*.5 for i in range(3)])
    radius=max((p-center).length for p in pts)
    # Render an inventory thumbnail from the imported geometry.
    bpy.ops.object.camera_add(location=center+Vector((1.5,-.35,.8))*radius)
    cam=bpy.context.object; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    if name == 'HuntingRifle': cam.rotation_euler.rotate_axis('Z', math.radians(35))
    projected=[cam.rotation_euler.to_quaternion().inverted()@(p-center) for p in pts]
    cam.data.type='ORTHO';cam.data.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in (0,1))*1.15
    scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-1.3
    scene.render.film_transparent=True;scene.world=bpy.data.worlds.new('World');scene.world.color=(.3,.3,.3)
    for loc,power in [((1,-1,3),65),((-1,1,2),35)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*max(radius,.3))
        lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=radius*3
        lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
    scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(dest,name+'_Icon.png')
    bpy.ops.render.render(write_still=True)
    print('BUILT',name, 'bounds', [min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)])

rifle=DOWNLOAD+'/PSXHuntingRifle[FIXED]/PSXHuntingRifle[FIXED]'
flare=DOWNLOAD+'/PSXMiscGuns/PSXMiscGuns/FlareGun'
ammo=DOWNLOAD+'/PSXAmmoBoxes/PSXAmmoBoxes/Rifle Ammo'
build('HuntingRifle',rifle+'/HuntingRifle.fbx',.125,(-1.65,2.25),['BARifleAmmo','BARilfeCasing'],[rifle+'/Textures/HuntingRifleText.png',rifle+'/Textures/ScopeText.jpg'])
build('FlareGun',flare+'/FlareGun.fbx',.18,(-.65,.95),['FlareBullet'],[flare+'/Textures/Flaregun_Text_240.png'])
import runpy
runpy.run_path(os.path.join(ROOT, 'ArtSource/Weapons/build_flare_ammo.py'), run_name='__main__')
build('RifleAmmo',ammo+'/RifleAmmo_240.fbx',.1,(0,0),[],[ammo+'/Textures/RifleAmmoText_240.png'])

# Match the template's baked/unlit finish in the approved gun thumbnails too.
if os.path.isfile(os.path.join(ROOT, 'ArtSource/Weapons/TextureTheme/Applied/HuntingRifleText.png')):
    import runpy
    runpy.run_path(os.path.join(ROOT, 'ArtSource/Weapons/render_theme.py'), run_name='__main__')
