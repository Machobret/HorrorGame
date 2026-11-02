
# Shared paths keep generated assets beside their owning weapon.
import sys as _sys
from pathlib import Path as _Path
_sys.path.insert(0, str(next(p for p in _Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir()) / "Tools" / "Art"))
from weapon_paths import weapon_asset
from pathlib import Path
import bpy
root = Path(__file__).resolve().parents[2]
for name in ('HuntingRifle', 'FlareGun', 'FlareAmmo'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(weapon_asset('Models', str(name) + "/" + str(name + '.fbx'))))
    for obj in bpy.data.objects:
        if obj.type != 'MESH': continue
        print('MESH', name, obj.name, 'faces', len(obj.data.polygons), 'dimensions', tuple(obj.dimensions))
        for material in obj.data.materials:
            print('MATERIAL', material.name)
            for node in material.node_tree.nodes:
                if node.type == 'TEX_IMAGE':
                    print('IMAGE', node.label, node.image.filepath if node.image else None)
            for link in material.node_tree.links:
                print('LINK', link.from_node.type, link.from_socket.name, '->', link.to_node.type, link.to_socket.name)
        if name == 'FlareAmmo':
            print('UV_POLYS', [[tuple(obj.data.uv_layers.active.data[i].uv) for i in p.loop_indices] for p in obj.data.polygons])
