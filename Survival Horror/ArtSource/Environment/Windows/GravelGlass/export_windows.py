"""Run with Blender --background --factory-startup --disable-autoexec --python.

Reads Models1.blend without saving it. Publishes the two selected bodies and
opening markers in segment coordinates, and a deterministic gravel normal map.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import zlib

import bpy
import bmesh
import numpy as np
from mathutils import Matrix


def png(path, rgb):
    height, width, _ = rgb.shape
    def chunk(kind, payload):
        return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)
    data = b''.join(b'\0' + row.tobytes() for row in rgb)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(data, 9)) + chunk(b'IEND', b''))


def gravel_normal(path):
    # A periodic, jittered field of rounded pebbles; no colour/roughness is baked.
    size, cells = 512, 16
    rng = np.random.default_rng(220611)
    centres = rng.uniform(.15, .85, (cells, cells, 2))
    xx, yy = np.meshgrid((np.arange(size) + .5) * cells / size, (np.arange(size) + .5) * cells / size)
    ix, iy = np.floor(xx).astype(int), np.floor(yy).astype(int)
    distance = np.full((size, size), np.inf)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            cx, cy = ix + dx, iy + dy
            jitter = centres[cy % cells, cx % cells]
            distance = np.minimum(distance, ((xx - cx - jitter[..., 0]) ** 2 + (yy - cy - jitter[..., 1]) ** 2))
    height = np.maximum(0, 1 - np.sqrt(distance) / .75) ** 1.5
    # A low-amplitude finer structure keeps the pebbles irregular and mineral-like.
    height += .025 * np.sin(2 * math.pi * xx * 3) * np.sin(2 * math.pi * yy * 5)
    gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * size / cells * .35
    gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * size / cells * .35
    normal = np.stack((-gx, -gy, np.ones_like(gx)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    # PNG rows are top to bottom; flip the data so +G follows upward UV.
    png(path, np.flipud(np.rint((normal * .5 + .5) * 255).astype(np.uint8)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source_hash = hashlib.sha256(args.source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(args.source))
    target = args.project / 'Assets/Game/Environment/Windows/GravelGlass'
    target.joinpath('Sources').mkdir(parents=True, exist_ok=True)
    records = []
    snapshot_scene = bpy.data.scenes.new('Gravel Glass Windows')
    for shape in ('01', '02'):
        source = bpy.data.objects[f'Insert_Window_{shape}_W1000_H2800_Model03_Body.001']
        body = source.copy()
        body.data = source.data.copy()
        body.parent = None
        body.matrix_world = Matrix.Identity(4)
        body.name = 'Body'
        bpy.context.scene.collection.objects.link(body)
        assert len(body.data.materials) == 2
        lo = [min(v.co[a] for v in body.data.vertices) for a in range(3)]
        hi = [max(v.co[a] for v in body.data.vertices) for a in range(3)]
        glass = [p for p in body.data.polygons if p.material_index == 1]
        assert len(glass) == 1 and len(glass[0].vertices) == 4, 'Export the revised single-quad pane; do not add overlapping glass.'
        vertices = [body.data.vertices[i].co for p in glass for i in p.vertices]
        glass_lo = [min(v[a] for v in vertices) for a in range(3)]
        glass_hi = [max(v[a] for v in vertices) for a in range(3)]
        uv = body.data.uv_layers.active.data
        for poly in body.data.polygons:
            for loop_id in poly.loop_indices:
                if poly.material_index == 0:
                    # Existing frame atlas: upper-left of a 4-column, 2-row image.
                    coord = uv[loop_id].uv
                    coord.x = min(.25 - .5 / 256, max(.5 / 256, coord.x))
                    coord.y = min(1 - .5 / 256, max(.5 + .5 / 256, coord.y))
                else:
                    v = body.data.vertices[body.data.loops[loop_id].vertex_index].co
                    # Broad pane faces use physical aspect-matched 0..1 UVs.
                    axes = (0, 2) if abs(poly.normal.y) > .5 else (1, 2) if abs(poly.normal.x) > .5 else (0, 1)
                    uv[loop_id].uv = tuple((v[a] - glass_lo[a]) / max(glass_hi[a] - glass_lo[a], 1e-6) for a in axes)
        # Unity orders FBX submeshes by first material occurrence, which can
        # differ from Blender's slot list after joining edited objects.
        ordered = bmesh.new()
        ordered.from_mesh(body.data)
        ordered.faces.sort(key=lambda face: face.material_index)
        ordered.faces.index_update()
        ordered.to_mesh(body.data)
        ordered.free()
        body.data.update()
        markers = []
        for name, value in [('OpeningMin', (lo[0], 0, lo[2])), ('OpeningMax', (hi[0], 0, hi[2]))]:
            marker = bpy.data.objects.new(name, None)
            bpy.context.scene.collection.objects.link(marker)
            marker.location = value
            markers.append(marker)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in [body] + markers:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = body
        filename = f'Insert_Window_{shape}_W1000_H2800_ModelGravel.fbx'
        bpy.ops.export_scene.fbx(filepath=str(target / 'Sources' / filename), use_selection=True,
            object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
            global_scale=1, bake_space_transform=True, bake_anim=False,
            use_custom_props=False, path_mode='STRIP', mesh_smooth_type='FACE')
        records.append({'object': source.name, 'file': filename, 'bounds_blender': [lo, hi],
                        'vertices': len(body.data.vertices), 'polygons': len(body.data.polygons),
                        'pane_faces': len(glass), 'frame_uv_tile': [0, .5, .25, 1]})
        # Preserve an editable, normalized source copy outside runtime Assets.
        collection = bpy.data.collections.new(f'Window{shape}')
        snapshot_scene.collection.children.link(collection)
        for obj in [body] + markers:
            obj.name = f'Window{shape}_{obj.name}'
            collection.objects.link(obj)
            obj.hide_set(True)
    source_dir = args.project / 'ArtSource/Environment/Windows/GravelGlass'
    bpy.data.libraries.write(str(source_dir / 'GravelWindows_Source.blend'), {snapshot_scene}, path_remap='ABSOLUTE', compress=True)
    if not (target / 'GravelGlass_Normal.png').exists():
        gravel_normal(target / 'GravelGlass_Normal.png')
    assert source_hash == hashlib.sha256(args.source.read_bytes()).hexdigest()
    (source_dir / 'provenance.json').write_text(json.dumps({'source': str(args.source), 'sha256': source_hash,
        'objects': records, 'normal': {'generator': 'periodic rounded gravel height field', 'seed': 220611,
        'size': 512, 'cells': 16}, 'source_modified': False}, indent=2))
    print('GRAVEL_WINDOWS_EXPORT ' + json.dumps(records))


if __name__ == '__main__':
    main()
