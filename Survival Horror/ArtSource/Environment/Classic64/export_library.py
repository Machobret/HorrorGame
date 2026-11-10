"""Audit and export the complete Classic64 pack, retaining the approved pilot.

Use Blender --background --factory-startup --disable-autoexec --python-exit-code 1
--python this.py -- --audit. Exports are staged in the isolated validation project.
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
REVIEW = PROJECT / 'Exports/Reviews/Classic64/Library'
STAGE = PROJECT / 'Exports/Reviews/Classic64/UnityValidation'
IMAGE_FILES = [p for p in ORIGINAL.rglob('*') if p.suffix.lower() in ('.png', '.jpg')]
sys.path.insert(0, str(HERE))
from prepare_library import FAMILIES

# Verified rename pairs in the supplied pack; no substitute artwork is invented.
IMAGE_ALIASES = {
    'sign_no_smoking.png': 'Signs/Materials/sign_no_smoking_01.png',
    'sign_confined_space.png': 'Signs/Materials/sign_confined_space_01.png',
    'sign_no_parking.png': 'Signs/Materials/sign_no_parking_01.png',
    'metal_bar.png': 'Metal/Materials/metal_bar_01.png',
    'metal_bar_2.png': 'Metal/Materials/metal_bar_02.png',
    'metal_diamond_plate.png': 'Metal/Materials/metal_diamondplate_01.png',
}
QUALITY_EXCLUDED_SOURCES = {
    'Buildings/shed_01.blend',
    'Units/generator_02 (unfinished).blend',
    'Units/walkin_cooler_display (unfinished).blend',
    'Vehicles/bike_vintage (unfinished).blend',
    'Vehicles/car_parts_brakes (unfinished).blend',
}


def clean(value):
    return re.sub('[^A-Za-z0-9]+', '_', value).strip('_')


def subject(value):
    return ''.join(part[:1].upper() + part[1:] for part in re.split('[^A-Za-z0-9]+', value) if part)


def family_for(path):
    category = path.parts[0]
    name = path.stem.lower()
    if category == 'Industrial' and any(t in name for t in ('guard_rail', 'manhole', 'yellow_post')):
        return 'RoadFurniture'
    if category == 'Wood' and 'pallet' in name:
        return 'StorageContainers'
    if category == 'Misc' and any(t in name for t in ('garbage', 'pvc_pipe')):
        return 'StorageContainers' if 'garbage' in name or 'pallet' in name else 'ToolsEquipment'
    if category == 'Products':
        return 'HouseholdDecor'
    return FAMILIES.get(category, 'Furniture')


def rel(path):
    return path.relative_to(PROJECT).as_posix()


def resolve_image(image):
    if image.packed_file:
        return dict(kind='Packed', image=image.name, sha256=hashlib.sha256(bytes(image.packed_file.data)).hexdigest(), size=list(image.size))
    name = image.filepath.replace('\\', '/').rsplit('/', 1)[-1]
    candidate = Path(bpy.path.abspath(image.filepath))
    if not candidate.is_file():
        matches = [p for p in IMAGE_FILES if p.name.casefold() == name.casefold()]
        if len(matches) != 1:
            return dict(kind='Missing' if not matches else 'Ambiguous', image=image.name, path=image.filepath,
                        candidates=[p.relative_to(ORIGINAL).as_posix() for p in matches])
        candidate = matches[0]
    return dict(kind='File', image=image.name, path=candidate.resolve().relative_to(ORIGINAL).as_posix(),
                sha256=hashlib.sha256(candidate.read_bytes()).hexdigest(), size=list(image.size))


class LibraryExporter:
    def __init__(self):
        pilot = json.loads((HERE / 'PilotManifest.json').read_text(encoding='utf-8'))
        self.pilot_sources = {a['source'].split('/Original/', 1)[1] for a in pilot['assets']}
        self.manifest = dict(batch='Classic64_Library', assets=[], materials=[], surfaces=[],
                             textures=[], source_models=[], source_images=[], repairs=[], notes=[])
        previous_path = HERE / 'LibraryManifest.json'
        previous = json.loads(previous_path.read_text(encoding='utf-8')) if previous_path.exists() else {}
        self.known_textures = {t['sha256']: t for t in previous.get('textures', [])}
        self.known_materials = {(m['texture']['sha256'], m['double_sided']): m for m in previous.get('materials', []) if m.get('texture')}
        self.textures = {}
        self.materials = {}
        for record in pilot['materials']:
            record = dict(record, preserve_existing=True)
            texture = dict(record['texture'], preserve_existing=True)
            record['texture'] = texture
            self.textures.setdefault(texture['sha256'], texture)
            self.materials[(texture['sha256'], record['double_sided'])] = record
        self.pilot_materials = list(self.materials.values())

    def texture(self, data, origin, family, name):
        sha = hashlib.sha256(data).hexdigest()
        if sha in self.textures:
            return self.textures[sha]
        if sha in self.known_textures:
            record = dict(self.known_textures[sha])
            if (PROJECT / record['path']).is_file():
                record['preserve_existing'] = True
                self.textures[sha] = record
                return record
        ext = '.png' if data.startswith(b'\x89PNG') else '.jpg' if data.startswith(b'\xff\xd8') else None
        if not ext:
            raise ValueError(f'Unsupported image format: {origin}')
        path = Path('Assets/Game/Environment') / family / ('Classic64_' + clean(name) + '_Albedo' + ext)
        target = STAGE / path
        if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() != sha:
            # Same-named JPG/PNG variants are distinct source artwork.
            path = path.with_name(path.stem + '_' + sha[:8] + ext)
            target = STAGE / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        image = bpy.data.images.load(str(target), check_existing=False)
        pixels = np.empty(len(image.pixels), dtype=np.float32)
        image.pixels.foreach_get(pixels)
        alpha = pixels[3::4]
        record = dict(path=path.as_posix(), source=origin, sha256=sha, width=image.size[0], height=image.size[1],
                      cutout=bool(np.any(alpha < .98)), preserve_existing=False)
        if not record['width'] or not record['height']:
            raise ValueError(f'Image did not load: {origin}')
        bpy.data.images.remove(image)
        self.textures[sha] = record
        return record

    def textured_material(self, texture, family, name, double_sided=True):
        key = (texture['sha256'], double_sided)
        if key in self.materials:
            return self.materials[key]
        if key in self.known_materials:
            record = dict(self.known_materials[key], texture=texture)
            record['preserve_existing'] = (PROJECT / record['path']).is_file()
            self.materials[key] = record
            return record
        mat_name = 'Classic64_' + clean(name)
        existing_paths = {m['path'] for m in self.materials.values()}
        existing_names = {m['name'] for m in self.materials.values()}
        if mat_name in existing_names:
            mat_name = 'Classic64_' + family + '_' + clean(name)
        path = f'Assets/Game/Environment/{family}/{mat_name}.mat'
        if path in existing_paths or mat_name in existing_names:
            mat_name += '_' + texture['sha256'][:8]
            path = f'Assets/Game/Environment/{family}/{mat_name}.mat'
        record = dict(name=mat_name, path=path, source_material=name, texture=texture,
                      cutout=texture['cutout'], double_sided=double_sided, preserve_existing=False)
        self.materials[key] = record
        return record

    def source_texture(self, image, blend_path, family, name):
        if image.packed_file:
            return self.texture(bytes(image.packed_file.data), rel(blend_path) + '#' + image.name, family, name)
        if image.source == 'GENERATED':
            # These are three unpainted BLANK placeholders in the explicitly
            # unfinished generator source, not missing downloadable artwork.
            self.manifest['notes'].append(dict(source=rel(blend_path), image=image.name,
                                                note='Unpainted generated BLANK image; retain source shader base color'))
            return None
        result = resolve_image(image)
        if result['kind'] == 'File':
            path = ORIGINAL / result['path']
        else:
            old = image.filepath.replace('\\', '/').rsplit('/', 1)[-1]
            if old not in IMAGE_ALIASES:
                raise ValueError(f'Unresolved image in {blend_path}: {image.filepath}')
            path = ORIGINAL / IMAGE_ALIASES[old]
            self.manifest['repairs'].append(dict(source=rel(blend_path), old=image.filepath, resolved=rel(path)))
        return self.texture(path.read_bytes(), rel(path), family, name)

    def material(self, original, blend_path, family, asset_name):
        if original is None:
            original_name = 'UnassignedSourceMaterial'
            images = []
            color = [.8, .8, .8, 1]
        else:
            original_name = original.name
            images = list(dict.fromkeys(n.image for n in original.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)) if original.node_tree else []
            color = list(original.diffuse_color)
            for node in original.node_tree.nodes if original.node_tree else []:
                if node.type == 'BSDF_PRINCIPLED':
                    color = list(node.inputs['Base Color'].default_value)
                    break
        if len(images) > 1:
            valid = [image for image in images if resolve_image(image)['kind'] in ('Packed', 'File')]
            if len(valid) != 1:
                raise ValueError(f'Multi-image material needs review: {blend_path}: {original_name}')
            images = valid
        texture = self.source_texture(images[0], blend_path, family, asset_name + '_' + clean(original_name)) if images else None
        if texture:
            record = self.textured_material(texture, family, texture['path'].split('/')[-1].rsplit('_Albedo', 1)[0].removeprefix('Classic64_'))
        else:
            raise ValueError(f'Untextured source is outside finished-asset scope: {blend_path}: {original_name}')
        # Create an export-only material named exactly as the Unity remap target.
        mat = bpy.data.materials.get(record['name'])
        if mat is None:
            mat = bpy.data.materials.new(record['name'])
            mat.use_nodes = True
            shader = mat.node_tree.nodes.get('Principled BSDF')
            if texture:
                node = mat.node_tree.nodes.new('ShaderNodeTexImage')
                node.image = bpy.data.images.load(str((PROJECT if texture['preserve_existing'] else STAGE) / texture['path']), check_existing=True)
                mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
            else:
                shader.inputs['Base Color'].default_value = color
        return mat, record

    def export_asset(self, path, source_objects, family, asset_subject, sandbox=False, scale_axis=None, target_size=None):
        name = 'Classic64_' + asset_subject
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        snapshots = []
        # Evaluate the complete source hierarchy before detaching any objects.
        for obj in source_objects:
            for modifier in obj.modifiers:
                modifier.show_viewport = modifier.show_render
        bpy.context.view_layer.update()
        for obj in source_objects:
            evaluated = obj.evaluated_get(depsgraph)
            mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
            if not mesh.polygons:
                raise ValueError(f'No evaluated faces: {path}: {obj.name}')
            snapshots.append((obj, mesh, obj.matrix_world.copy()))
        vertices = [matrix @ v.co for _, mesh, matrix in snapshots for v in mesh.vertices]
        lower = Vector([min(v[i] for v in vertices) for i in range(3)])
        upper = Vector([max(v[i] for v in vertices) for i in range(3)])
        size = upper - lower
        factor = target_size / size[scale_axis] if scale_axis is not None else 1.0
        pivot = Vector(((lower.x + upper.x) / 2, (lower.y + upper.y) / 2, lower.z))
        folder = Path('Assets/Sandbox/Classic64/Examples') if sandbox else Path('Assets/Game/Environment') / family
        (STAGE / folder).mkdir(parents=True, exist_ok=True)
        exported = []
        geometry = []
        material_names = set()
        for index, (obj, mesh, matrix) in enumerate(snapshots):
            before_uv = [tuple(u.uv) for u in mesh.uv_layers.active.data] if mesh.uv_layers.active else []
            if not before_uv:
                raise ValueError(f'No source UVs: {path}: {obj.name}')
            used_indices = sorted({p.material_index for p in mesh.polygons})
            slot_map = {}
            export_materials = []
            for slot in used_indices:
                original = mesh.materials[slot] if slot < len(mesh.materials) else None
                mat, record = self.material(original, path, family, name)
                slot_map[slot] = len(export_materials)
                export_materials.append(mat)
                material_names.add(record['name'])
            for polygon in mesh.polygons:
                polygon.material_index = slot_map[polygon.material_index]
            mesh.materials.clear()
            for mat in export_materials:
                mesh.materials.append(mat)
            mesh.transform(Matrix.Scale(factor, 4) @ Matrix.Translation(-pivot) @ matrix)
            if before_uv != [tuple(u.uv) for u in mesh.uv_layers.active.data]:
                raise ValueError('Authored UV coordinates changed')
            part = bpy.data.objects.new(f'{name}_Part{index:03d}', mesh)
            bpy.context.scene.collection.objects.link(part)
            exported.append(part)
            geometry.append(dict(source_object=obj.name, exported_object=part.name, vertices=len(mesh.vertices),
                                 polygons=len(mesh.polygons), uv_corners=len(before_uv),
                                 baked_modifiers=[m.type for m in obj.modifiers if m.show_render]))
        bpy.ops.object.select_all(action='DESELECT')
        for obj in exported:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = exported[0]
        model = (folder / (name + '_Model.fbx')).as_posix()
        bpy.ops.export_scene.fbx(filepath=str(STAGE / model), use_selection=True, object_types={'MESH'},
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                                 bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type='FACE',
                                 add_leaf_bones=False, bake_anim=False, path_mode='STRIP', use_custom_props=False)
        record = dict(name=name, source=rel(path), family=family, model_path=model,
                      prefab_path=(folder / (name + '_Visual.prefab')).as_posix(),
                      material_names=sorted(material_names), geometry=geometry,
                      bounds_m=[size.x * factor, size.z * factor, size.y * factor], original_bounds_m=list(size),
                      scale_correction=factor, pivot_source=list(pivot), pivot='GroundCenter',
                      authored_unfinished='unfinished' in path.stem.lower(), sandbox_example=sandbox)
        self.manifest['assets'].append(record)
        for obj in exported:
            bpy.data.objects.remove(obj, do_unlink=True)
        return record

    def run(self):
        # Archive every supplied PNG/JPEG as an available runtime texture/material.
        # Exact-byte pilot matches retain their authoritative existing paths.
        for path in sorted(IMAGE_FILES):
            source = path.relative_to(ORIGINAL)
            family = family_for(source)
            texture = self.texture(path.read_bytes(), rel(path), family, subject(path.stem))
            material = self.textured_material(texture, family, Path(texture['path']).stem.removesuffix('_Albedo').removeprefix('Classic64_'))
            self.manifest['source_images'].append(dict(source=source.as_posix(), texture_path=texture['path'], material_path=material['path']))
            if material['path'] not in {s['material_path'] for s in self.manifest['surfaces']}:
                self.manifest['surfaces'].append(dict(name=material['name'], material_path=material['path']))
        for path in sorted(ORIGINAL.rglob('*.blend')):
            source = path.relative_to(ORIGINAL).as_posix()
            if source.startswith(('Doors/', 'Windows/', 'Walls/')):
                self.manifest['source_models'].append(dict(source=source, status='ExcludedByUserGeometryScope', runtime_paths=[]))
                continue
            if source in QUALITY_EXCLUDED_SOURCES or 'unfinished' in path.stem.lower():
                self.manifest['source_models'].append(dict(source=source, status='ExcludedByUserQualityScope', runtime_paths=[]))
                continue
            if source in self.pilot_sources:
                self.manifest['source_models'].append(dict(source=source, status='ExistingPilot'))
                continue
            bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
            if bpy.context.object and bpy.context.object.mode != 'OBJECT':
                bpy.ops.object.mode_set(mode='OBJECT')
            bpy.context.view_layer.update()
            objects = sorted([o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.hide_render], key=lambda o: o.name)
            family = family_for(path.relative_to(ORIGINAL))
            generated = []
            if source == 'Industrial/guard_rail.blend':
                generated.append(self.export_asset(path, [o for o in objects if o.name != 'Guardrail_Example'], family, 'GuardRail', scale_axis=2, target_size=1.0))
                generated.append(self.export_asset(path, [o for o in objects if o.name == 'Guardrail_Example'], family, 'GuardRailExample', sandbox=True, scale_axis=2, target_size=1.0))
            elif source == 'Food Shelf Scene.blend':
                generated.append(self.export_asset(path, objects, 'Furniture', 'FoodShelfExample', sandbox=True))
            elif source == 'Walls/wall_pieces_wood_planks_2.blend' or source == 'Units/generator_02 (unfinished).blend':
                for index, obj in enumerate(objects):
                    generated.append(self.export_asset(path, [obj], family,
                                                        ('WoodWallPiece' if source.startswith('Walls/') else 'GeneratorUnfinishedVariant') + f'{index + 1:02d}'))
            else:
                scale = dict(scale_axis=1, target_size=4.6) if source == 'Vehicles/volvo.blend' else dict(scale_axis=2, target_size=3.0) if source == 'Buildings/shed_01.blend' else {}
                generated.append(self.export_asset(path, objects, family, subject(path.stem), **scale))
            self.manifest['source_models'].append(dict(source=source, status='Exported',
                                                        runtime_paths=[a['prefab_path'] for a in generated]))
            print('CLASSIC64_LIBRARY_EXPORTED', source, 'prefabs', len(generated), flush=True)
        self.manifest['materials'] = list(self.materials.values())
        self.manifest['textures'] = list(self.textures.values())
        for key in ('name', 'path'):
            values = [m[key] for m in self.manifest['materials']]
            if len(values) != len(set(values)):
                raise ValueError(f'Duplicate material {key} in complete library')
        self.manifest['counts'] = dict(source_blend_files=len(self.manifest['source_models']), source_image_files=len(IMAGE_FILES),
                                       new_prefabs=len(self.manifest['assets']), existing_pilot_prefabs=len(self.pilot_sources),
                                       excluded_source_models=sum(s['status'].startswith('ExcludedByUser') for s in self.manifest['source_models']),
                                       materials=len(self.materials), textures=len(self.textures))
        (HERE / 'LibraryManifest.json').write_text(json.dumps(self.manifest, indent=2), encoding='utf-8')
        target = STAGE / 'ArtSource/Environment/Classic64/LibraryManifest.json'
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(HERE / 'LibraryManifest.json', target)
        print('CLASSIC64_LIBRARY_EXPORT_COMPLETE', json.dumps(self.manifest['counts']), flush=True)


def audit():
    records = []
    for path in sorted(ORIGINAL.rglob('*.blend')):
        bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.view_layer.update()
        record = dict(source=path.relative_to(ORIGINAL).as_posix(), unit_scale=bpy.context.scene.unit_settings.scale_length,
                      objects=[], materials=[], unfinished='unfinished' in path.stem.lower())
        materials = set()
        for obj in sorted(bpy.context.scene.objects, key=lambda o: o.name):
            if obj.type != 'MESH':
                continue
            coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
            lower = [min(v[i] for v in coords) for i in range(3)] if coords else [0, 0, 0]
            upper = [max(v[i] for v in coords) for i in range(3)] if coords else [0, 0, 0]
            record['objects'].append(dict(name=obj.name, vertices=len(obj.data.vertices), faces=len(obj.data.polygons),
                                         uv_layers=[u.name for u in obj.data.uv_layers],
                                         modifiers=[dict(name=m.name, type=m.type, render=m.show_render) for m in obj.modifiers],
                                         materials=[s.material.name if s.material else None for s in obj.material_slots],
                                         used_material_indices=sorted(set(p.material_index for p in obj.data.polygons)),
                                         hidden_render=obj.hide_render, hidden_viewport=obj.hide_get(),
                                         lower=lower, upper=upper))
            materials.update(s.material for s in obj.material_slots if s.material)
        for material in sorted(materials, key=lambda m: m.name):
            images = list(dict.fromkeys(n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)) if material.node_tree else []
            record['materials'].append(dict(name=material.name, diffuse=list(material.diffuse_color),
                                             images=[dict(resolve_image(image), image_source=image.source,
                                                          generated_type=image.generated_type) for image in images],
                                             shaders=[dict(type=n.type, color=list(n.inputs['Base Color'].default_value)
                                                           if 'Base Color' in n.inputs else None,
                                                           image=n.image.name if n.type == 'TEX_IMAGE' and n.image else None)
                                                      for n in material.node_tree.nodes] if material.node_tree else []))
        records.append(record)
        print('CLASSIC64_AUDIT', record['source'], len(record['objects']), flush=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    (REVIEW / 'SourceAudit.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
    print('CLASSIC64_AUDIT_COMPLETE', len(records), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    if args.audit:
        audit()
    else:
        LibraryExporter().run()
