from pathlib import Path
import bpy
root = Path(__file__).resolve().parents[2]
for name in ('HuntingRifle', 'FlareGun', 'FlareAmmo'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / 'Assets/Game/Weapons/Models' / name / (name + '.fbx')))
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
