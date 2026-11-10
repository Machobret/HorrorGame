"""Export five edited Wall_02 modules and the selected storefront door from Models1.blend.

Run with headless Blender, --disable-autoexec, and --source/--project after --.
The source file is never saved; transforms and names change only in export copies.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source_hash = hashlib.sha256(args.source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(args.source))
    sources = sorted([o for o in bpy.data.objects if o.type == 'MESH' and re.fullmatch(
        r'Wall_02_(Solid|Door)_01_W\d+_H2800_T300_Body', o.name)], key=lambda o: o.name)
    assert len(sources) == 5, 'Expected the three solids and two doorway surrounds authored in Models1.'
    door = bpy.data.objects.get('Insert_Door_01_W1000_H2800_Model01_Body')
    assert door is not None and door.type == 'MESH', 'Expected the selected storefront door.'
    sources.append(door)
    target = args.project / 'Assets/Game/Environment/Storefront/Sources'
    target.mkdir(parents=True, exist_ok=True)
    snapshot = bpy.data.scenes.new('Storefront T150')
    records = []
    for source in sources:
        is_insert = source == door
        name = (source.name.removesuffix('_Body').replace('Model01', 'ModelStorefront') if is_insert
            else source.name.removesuffix('_Body').replace('Wall_02_', 'Wall_Storefront_').replace('_T300', '_T150'))
        width = int(re.search(r'_W(\d+)', name)[1]) / 1000
        body = source.copy()
        body.data = source.data.copy()
        # Strip parent/display layout; bake only the body's centimetre scale and axis rotation.
        axes = source.matrix_basis.copy()
        axes.translation = Vector((0, 0, 0))
        body.data.transform(axes)
        body.parent = None
        body.matrix_world = Matrix.Identity(4)
        body.name = 'Body'
        bpy.context.scene.collection.objects.link(body)
        lo = [min(v.co[a] for v in body.data.vertices) for a in range(3)]
        hi = [max(v.co[a] for v in body.data.vertices) for a in range(3)]
        if not is_insert:
            assert abs(lo[0]) < .0001 and abs(hi[0] - width) < .0001
            assert abs(lo[2]) < .0001 and abs(hi[2] - 2.8) < .0001
            assert abs(lo[1] + .075) < .0001 and abs(hi[1] - .075) < .0001, (name, lo, hi)
        assert [m.name for m in body.data.materials] == ['Outside', 'Glass_Clear']
        uv = body.data.uv_layers.active.data
        glass_faces = [p for p in body.data.polygons if p.material_index == 1]
        if is_insert:
            assert len(glass_faces) == 1 and len(glass_faces[0].vertices) == 4, 'Keep the door glass as a single pane.'
        for polygon in glass_faces:
            for i in polygon.loop_indices:
                vertex = body.data.vertices[body.data.loops[i].vertex_index].co
                uv[i].uv = (vertex.x / .55, vertex.z / 1.1)
        ordered = bmesh.new()
        ordered.from_mesh(body.data)
        ordered.faces.sort(key=lambda face: face.material_index)
        ordered.faces.index_update()
        ordered.to_mesh(body.data)
        ordered.free()
        body.data.update()
        markers = []
        if '_Door_' in name:
            for role in ('OpeningMin', 'OpeningMax'):
                original = next(o for o in source.parent.children if o.name.endswith('_' + role))
                marker = bpy.data.objects.new(role, None)
                bpy.context.scene.collection.objects.link(marker)
                marker.location = original.location
                markers.append(marker)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in [body] + markers:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.export_scene.fbx(filepath=str(target / (name + '.fbx')), use_selection=True,
            object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y', global_scale=1,
            bake_space_transform=True, bake_anim=False, use_custom_props=False,
            path_mode='STRIP', mesh_smooth_type='FACE')
        records.append({'source_object': source.name, 'file': name + '.fbx', 'bounds_blender': [lo, hi],
            'vertices': len(body.data.vertices), 'faces': len(body.data.polygons), 'glass_faces': len(glass_faces),
            'glass_planes': [{'normal': list(p.normal), 'area': p.area} for p in body.data.polygons if p.material_index == 1]})
        collection = bpy.data.collections.new(name)
        snapshot.collection.children.link(collection)
        for obj in [body] + markers:
            obj.name = name + '_' + obj.name
            collection.objects.link(obj)
            obj.hide_set(True)
    source_dir = args.project / 'ArtSource/Environment/Storefront'
    bpy.data.libraries.write(str(source_dir / 'Storefront_Source.blend'), {snapshot}, path_remap='ABSOLUTE', compress=True)
    assert source_hash == hashlib.sha256(args.source.read_bytes()).hexdigest()
    (source_dir / 'provenance.json').write_text(json.dumps({'source': str(args.source), 'sha256': source_hash,
        'source_modified': False, 'family': 'Storefront', 'thickness_metres': .15, 'objects': records}, indent=2))
    print('STOREFRONT_EXPORT ' + json.dumps(records))


if __name__ == '__main__':
    main()
