"""Classic64 Blender inspection/export recipe. Use --inspect for source diagnostics.

blender --background --factory-startup --disable-autoexec --python export_pilot.py -- --inspect
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
ORIGINAL = HERE / 'Original'
REVIEW = PROJECT / 'Exports/Reviews/Classic64'


def clean(value):
    return re.sub(r'[^A-Za-z0-9]+', '_', value).strip('_')


def relative(path):
    return path.relative_to(PROJECT).as_posix()


def write_texture(image, blend_path, dest):
    """Recover legacy relative links by exact basename; never guess a substitute."""
    source_name = image.filepath.replace('\\', '/').rsplit('/', 1)[-1]
    if image.packed_file:
        data = bytes(image.packed_file.data)
        origin = relative(blend_path) + '#' + image.name
    else:
        candidate = Path(bpy.path.abspath(image.filepath))
        if not candidate.is_file():
            matches = [p for p in ORIGINAL.rglob('*') if p.is_file() and p.name.casefold() == source_name.casefold()]
            if len(matches) != 1:
                raise RuntimeError(f'Missing or ambiguous image: {image.name}, {image.filepath}: {matches}')
            candidate = matches[0]
        data = candidate.read_bytes()
        origin = relative(candidate)
    extension = '.png' if data.startswith(b'\x89PNG') else '.jpg' if data.startswith(b'\xff\xd8') else None
    if not extension:
        raise RuntimeError(f'Unsupported texture bytes: {origin}')
    dest = dest.with_suffix(extension)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
    # Load the exact bytes written, including recovered legacy links.
    loaded = bpy.data.images.load(str(dest), check_existing=False)
    pixels = np.empty(len(loaded.pixels), dtype=np.float32)
    loaded.pixels.foreach_get(pixels)
    alpha = pixels[3::4]
    result = dict(path=relative(dest), source=origin, sha256=hashlib.sha256(data).hexdigest(),
                  width=loaded.size[0], height=loaded.size[1], cutout=bool(np.any(alpha < .98)))
    bpy.data.images.remove(loaded)
    return result


def export(selection):
    manifest = dict(batch='Classic64_Pilot', assets=[], materials=[], surfaces=[])
    for entry in selection['models']:
        if entry['source'].startswith(('Doors/', 'Windows/', 'Walls/')):
            raise ValueError('Standalone building geometry is outside the approved scope')
        blend_path = ORIGINAL / entry['source']
        bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False, use_scripts=False)
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.view_layer.update()
        objects = sorted([o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.hide_render], key=lambda o: o.name)
        source_matrices = {o: o.matrix_world.copy() for o in objects}
        if not objects or any(not o.data.uv_layers for o in objects):
            raise RuntimeError(f'Missing geometry/UVs in {blend_path}')
        # Transformed object bounding boxes can overestimate skewed legacy meshes.
        # Measure actual transformed vertices so the receipt matches visible geometry.
        corners = [source_matrices[o] @ v.co for o in objects for v in o.data.vertices]
        lower = Vector([min(c[i] for c in corners) for i in range(3)])
        upper = Vector([max(c[i] for c in corners) for i in range(3)])
        size = upper - lower
        factor = entry['target_height_m'] / size.z if entry['target_height_m'] else 1.0
        pivot = Vector(((lower.x + upper.x) / 2, (lower.y + upper.y) / 2, lower.z))
        if entry['subject'] == 'BrownDoor':
            pivot.x = lower.x  # ground-level hinge edge, retained for later gameplay integration
        name = 'Classic64_' + entry['subject']
        folder = PROJECT / 'Assets/Game/Environment' / entry['family']
        folder.mkdir(parents=True, exist_ok=True)
        materials = list(dict.fromkeys(s.material for o in objects for s in o.material_slots if s.material))
        material_names = []
        for index, mat in enumerate(materials):
            original_name = mat.name
            images = list(dict.fromkeys(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image))
            if len(images) != 1:
                raise RuntimeError(f'Expected one authored texture in {entry["source"]}: {mat.name}')
            texture = write_texture(images[0], blend_path, folder / f'{name}_{clean(original_name)}_Albedo')
            mat.name = f'{name}_{clean(original_name)}'
            material_names.append(mat.name)
            record = dict(name=mat.name, path=relative(folder / (mat.name + '.mat')), texture=texture,
                          cutout=texture['cutout'], double_sided=True, source_material=original_name)
            manifest['materials'].append(record)
            # Legacy Blender Internal image-to-output links are not valid modern shaders.
            # Rebuild only the in-memory export material, leaving the original untouched.
            mat.use_nodes = True
            nodes = mat.node_tree.nodes
            nodes.clear()
            output = nodes.new('ShaderNodeOutputMaterial')
            shader = nodes.new('ShaderNodeBsdfPrincipled')
            shader.inputs['Roughness'].default_value = .9
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(PROJECT / texture['path']), check_existing=True)
            tex.interpolation = 'Closest'
            mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
            if texture['cutout']:
                mat.node_tree.links.new(tex.outputs['Alpha'], shader.inputs['Alpha'])
            mat.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
        original_names = [o.name for o in objects]
        geometry = []
        for index, obj in enumerate(objects):
            if obj.modifiers:
                raise RuntimeError(f'Modifiers require deliberate review: {obj.name}')
            uv_before = [tuple(loop.uv) for loop in obj.data.uv_layers.active.data]
            mesh = obj.data.copy()
            mesh.transform(Matrix.Scale(factor, 4) @ Matrix.Translation(-pivot) @ source_matrices[obj])
            obj.parent = None
            obj.matrix_world = Matrix.Identity(4)
            obj.data = mesh
            obj.name = f'{name}_Part{index:02d}'
            if uv_before != [tuple(loop.uv) for loop in mesh.uv_layers.active.data]:
                raise RuntimeError('UV coordinates changed')
            geometry.append(dict(source_object=original_names[index], exported_object=obj.name,
                                 vertices=len(mesh.vertices), polygons=len(mesh.polygons), uv_corners=len(uv_before)))
        # Export objects have world-space geometry baked; transforms and root scale are identity.
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.hide_set(False)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        model_path = folder / f'{name}_Model.fbx'
        bpy.ops.export_scene.fbx(filepath=str(model_path), use_selection=True, object_types={'MESH'},
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                                 use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
                                 bake_anim=False, path_mode='STRIP', use_custom_props=False)
        manifest['assets'].append(dict(name=name, source=relative(blend_path), family=entry['family'],
                                        model_path=relative(model_path), prefab_path=relative(folder / f'{name}_Visual.prefab'),
                                        material_names=material_names, geometry=geometry,
                                        bounds_m=[size.x * factor, size.z * factor, size.y * factor],
                                        original_bounds_m=list(size), scale_correction=factor,
                                        pivot_source=list(pivot), pivot='HingeEdge' if entry['subject'] == 'BrownDoor' else 'GroundCenter'))
        print('CLASSIC64_EXPORTED', name, 'parts', len(objects), 'scale', round(factor, 6), flush=True)
    for entry in selection['surfaces']:
        folder = PROJECT / 'Assets/Game/Environment' / entry['family']
        name = 'Classic64_' + entry['subject']
        source = ORIGINAL / entry['source']
        image = bpy.data.images.load(str(source), check_existing=False)
        texture = write_texture(image, source, folder / f'{name}_Albedo')
        texture['source'] = relative(source)
        record = dict(name=name, path=relative(folder / f'{name}.mat'), texture=texture,
                      cutout=texture['cutout'], double_sided=False, source_material='Standalone surface')
        manifest['materials'].append(record)
        manifest['surfaces'].append(dict(name=name, material_path=record['path']))
    (HERE / 'PilotManifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('CLASSIC64_EXPORT_COMPLETE', len(manifest['assets']), 'props', len(manifest['materials']), 'materials', flush=True)


def inspect(selection):
    records = []
    for entry in selection['models']:
        bpy.ops.wm.open_mainfile(filepath=str(ORIGINAL / entry['source']), load_ui=False, use_scripts=False)
        record = dict(entry)
        record['unit_scale'] = bpy.context.scene.unit_settings.scale_length
        record['objects'] = [dict(name=o.name, type=o.type, dimensions=list(o.dimensions),
                                  location=list(o.location), rotation=list(o.rotation_euler), scale=list(o.scale),
                                  vertices=len(o.data.vertices), faces=len(o.data.polygons),
                                  hidden_render=o.hide_render, hidden_viewport=o.hide_get(),
                                  parent=o.parent.name if o.parent else None,
                                  world_bounds=[list(o.matrix_world @ __import__('mathutils').Vector(c)) for c in o.bound_box],
                                  uv_layers=[u.name for u in o.data.uv_layers],
                                  materials=[s.material.name if s.material else None for s in o.material_slots])
                             for o in bpy.context.scene.objects if o.type == 'MESH']
        record['materials'] = [dict(name=m.name, use_nodes=m.use_nodes, diffuse=list(m.diffuse_color),
                                   nodes=[dict(name=n.name, type=n.type,
                                               image=n.image.name if n.type == 'TEX_IMAGE' and n.image else None)
                                          for n in m.node_tree.nodes] if m.node_tree else [])
                               for m in bpy.data.materials]
        record['images'] = [dict(name=i.name, filepath=i.filepath, packed=bool(i.packed_file), size=list(i.size))
                            for i in bpy.data.images if i.type == 'IMAGE']
        records.append(record)
    REVIEW.mkdir(parents=True, exist_ok=True)
    (REVIEW / 'SourceInspection.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
    print('CLASSIC64_INSPECTED', len(records), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--inspect', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    selection = json.loads((HERE / 'PilotSelection.json').read_text(encoding='utf-8'))
    if args.inspect:
        inspect(selection)
    else:
        export(selection)


if __name__ == '__main__':
    main()
