"""Normalize the largest pack revolver and render opaque examination/inventory assets."""
from pathlib import Path
import bpy, shutil
from mathutils import Matrix
ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'Assets/Game/Weapons/Models'
SOURCE = Path('C:/Users/Mangis/Downloads/Assets/Guns/PSXRevolverPack[FIXED]/PSXRevolverPack[FIXED]/Files/Colt Python')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE / 'Colt Python.fbx'))
for obj in list(bpy.data.objects):
    if obj.type != 'MESH' or obj.name in ('CPBullet', 'CPCyilnderFull'):
        bpy.data.objects.remove(obj, do_unlink=True)
        continue
    matrix = obj.matrix_world.copy()
    for vertex in obj.data.vertices:
        p = matrix @ vertex.co
        vertex.co = (p.y*.105, -(p.x+.65)*.105, (p.z-.70)*.105)
    obj.matrix_world = Matrix.Identity(4)
folder = MODELS / 'Revolver'; folder.mkdir(parents=True, exist_ok=True)
image = bpy.data.images.load(str(ROOT / 'ArtSource/Weapons/Revolver/ColtPython_Atlas_Source.png'))
image.scale(400,206); image.file_format='PNG'; image.filepath_raw=str(folder/'ColtPython.png'); image.save()
themed_cylinder=ROOT/'ArtSource/Weapons/Revolver/Cylinder_Atlas_Source.png'
if themed_cylinder.exists():
    cylinder=bpy.data.images.load(str(themed_cylinder));cylinder.scale(256,256)
    cylinder.file_format='JPEG';cylinder.filepath_raw=str(folder/'cylinderRefV2.jpg');cylinder.save()
else: shutil.copy2(SOURCE/'Textures/cylinderRefV2.jpg', folder/'cylinderRefV2.jpg')
for obj in bpy.data.objects:
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE':
                node.image=bpy.data.images.load(str(folder/('cylinderRefV2.jpg' if '034' in mat.name else 'ColtPython.png')),check_existing=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(folder/'Revolver.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,path_mode='STRIP')

# A compact, solid box with a dedicated .357 label on each face.
bpy.ops.wm.read_factory_settings(use_empty=True)
folder=MODELS/'RevolverAmmo';folder.mkdir(parents=True,exist_ok=True)
image=bpy.data.images.load(str(ROOT/'ArtSource/Weapons/Revolver/RevolverAmmo_Atlas_Source.png'))
image.scale(256,256);image.file_format='PNG';image.filepath_raw=str(folder/'RevolverAmmo_Albedo.png');image.save()
mat=bpy.data.materials.new('MagnumBox');mat.use_nodes=True
texture=mat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
mat.node_tree.links.new(texture.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,.03))
obj=bpy.context.object;obj.name='MagnumAmmoBox';obj.scale=(.11,.075,.06)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
uv=obj.data.uv_layers.active
for poly in obj.data.polygons:
    for loop in poly.loop_indices:
        p=obj.data.vertices[obj.data.loops[loop].vertex_index].co
        if abs(poly.normal.z)>.5: coord=(p.x/.11+.5,p.y/.075+.5)
        elif abs(poly.normal.y)>.5: coord=(p.x/.11*(1 if poly.normal.y<0 else -1)+.5,p.z/.06+.5)
        else: coord=(p.y/.075*(1 if poly.normal.x>0 else -1)+.5,p.z/.06+.5)
        uv.data[loop].uv=coord
obj.data.materials.append(mat)
bpy.ops.export_scene.fbx(filepath=str(folder/'RevolverAmmo.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,path_mode='STRIP')

# Reuse the established opaque, baked-diffuse icon renderer.
code=(ROOT/'ArtSource/Weapons/render_theme.py').read_text()
code=code.split('for weapon in (')[0]
code=code.replace("'FlareAmmo_Albedo.png' if name == 'FlareAmmo' else 'Flaregun_Text_240.png'", "('cylinderRefV2.jpg' if '034' in material.name else 'ColtPython.png') if name == 'Revolver' else 'RevolverAmmo_Albedo.png' if name == 'RevolverAmmo' else 'FlareAmmo_Albedo.png' if name == 'FlareAmmo' else 'Flaregun_Text_240.png'")
exec(compile(code,str(ROOT/'ArtSource/Weapons/render_theme.py'),'exec'))
render('Revolver');render('RevolverAmmo')
import runpy
runpy.run_path(str(ROOT/'ArtSource/Weapons/render_pickup_icons.py'),run_name='__main__')
runpy.run_path(str(ROOT/'ArtSource/Weapons/build_magnum_ammo.py'),run_name='__main__')
