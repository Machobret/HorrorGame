"""Export the supplied Railing with its Wireframe modifier, leaving the master unchanged.

Run Blender --background --factory-startup --disable-autoexec --python this_file -- --project PROJECT.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--quick', action='store_true', help='Export without validation or a review render.')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    project = args.project.resolve()
    source_dir = project / 'ArtSource/Environment/Railings'
    source = source_dir / 'Railing_Source.blend'
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    obj = bpy.data.objects.get('Railing')
    if not args.quick:
        assert obj and obj.type == 'MESH', 'The source must contain the Railing mesh.'
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated)
    # Bake authored object scale but discard the reference sheet's display placement.
    mesh.transform(obj.matrix_world.to_3x3().to_4x4())
    lo = Vector(tuple(min(v.co[i] for v in mesh.vertices) for i in range(3)))
    hi = Vector(tuple(max(v.co[i] for v in mesh.vertices) for i in range(3)))
    dimensions = hi - lo
    height = round(dimensions.z, 3)
    if not args.quick:
        assert abs(dimensions.x - 2) < .0001 and abs(dimensions.y - .1) < .0001
    for vertex in mesh.vertices:
        vertex.co = Vector((vertex.co.x - lo.x, vertex.co.y - (lo.y + hi.y) / 2,
            (vertex.co.z - lo.z) * height / dimensions.z))
    target = project / 'Exports/Reviews/RailingFamily/Models'
    target.mkdir(parents=True, exist_ok=True)
    # Retire only this recipe's disposable staging exports, never imported Assets or metadata.
    for previous in target.glob('Wall_Railing_*.fbx'):
        previous.unlink()
    scene = bpy.data.scenes.new('Railing export')
    bpy.context.window.scene = scene
    outside = bpy.data.materials.new('Outside')
    inside = bpy.data.materials.new('Inside')
    for material in [outside, inside]:
        material.diffuse_color = (.16, .19, .2, 1)
    records = []
    exports = []
    for width in [2, 1, .5]:
        body_mesh = mesh.copy()
        bm = bmesh.new()
        bm.from_mesh(body_mesh)
        if width < 2:
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                dist=.000001, plane_co=(width, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
            boundary = [edge for edge in bm.edges if edge.is_boundary]
            if boundary:
                bmesh.ops.holes_fill(bm, edges=boundary)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        nonmanifold = None if args.quick else sum(not edge.is_manifold for edge in bm.edges)
        if not args.quick:
            assert nonmanifold == 0, (width, 'Non-manifold edges', nonmanifold)
        bm.to_mesh(body_mesh)
        bm.free()
        body_mesh.materials.clear()
        body_mesh.materials.append(outside)
        body_mesh.materials.append(inside)
        uv = body_mesh.uv_layers.get('UVMap') or body_mesh.uv_layers.new(name='UVMap')
        body_mesh.update()
        for polygon in body_mesh.polygons:
            polygon.material_index = 0 if polygon.center.y >= -1e-7 else 1
            normal = polygon.normal
            axis = max(range(3), key=lambda i: abs(normal[i]))
            axes = [(1, 2), (0, 2), (0, 1)][axis]
            for loop in polygon.loop_indices:
                co = body_mesh.vertices[body_mesh.loops[loop].vertex_index].co
                uv.data[loop].uv = (co[axes[0]], co[axes[1]])
        body = bpy.data.objects.new('Body', body_mesh)
        scene.collection.objects.link(body)
        body.matrix_world = Matrix.Identity(4)
        bpy.ops.object.select_all(action='DESELECT')
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        name = f'Wall_Railing_Solid_01_W{round(width * 1000):04d}_H{round(height * 1000):04d}_T100'
        bpy.ops.export_scene.fbx(filepath=str(target / (name + '.fbx')), use_selection=True,
            object_types={'MESH'}, axis_forward='-Z', axis_up='Y', global_scale=1,
            bake_space_transform=True, bake_anim=False, use_custom_props=False,
            path_mode='STRIP', mesh_smooth_type='FACE', use_triangles=True)
        records.append(dict(file=name + '.fbx', width=width, height=height, thickness=.1,
            vertices=len(body_mesh.vertices), faces=len(body_mesh.polygons), nonmanifold_edges=nonmanifold))
        body.name = name
        body.hide_render = width != 2
        body.hide_set(width != 2)
        exports.append(body)
    (source_dir / 'provenance.json').write_text(json.dumps(dict(source_file='Railing_Source.blend',
        original_path=r'C:\Users\manta\Downloads\Procedural Toolkit Demos\Fence_Family_Reference.blend',
        source_sha256=before, source_object='Railing', source_modified=False,
        evaluated_dimensions_metres=list(dimensions), height_normalization_metres=height,
        modifier='Wireframe (evaluated, including boundary edges)', exports=records), indent=2))
    print('RAILING_EXPORT ' + json.dumps(records))
    if args.quick:
        return
    # A small, reproducible visual check of the actual evaluated source geometry.
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new('Railing review world')
    scene.world.color = (.25, .25, .25)
    for material in [outside, inside]:
        material.use_nodes = True
        shader = material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (.16, .19, .2, 1)
        shader.inputs['Metallic'].default_value = .5
        shader.inputs['Roughness'].default_value = .4
        albedo = material.node_tree.nodes.new('ShaderNodeTexImage')
        albedo.image = bpy.data.images.load(str(project / 'Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Metal/RustyMetal04/RustyMetal04_Albedo.jpg'), check_existing=True)
        material.node_tree.links.new(albedo.outputs['Color'], shader.inputs['Base Color'])
        normal_texture = material.node_tree.nodes.new('ShaderNodeTexImage')
        normal_texture.image = bpy.data.images.load(str(project / 'Assets/EnvironmentAuthoring/Content/Materials/Surfaces/Metal/RustyMetal04/RustyMetal04_Normal.jpg'), check_existing=True)
        normal_texture.image.colorspace_settings.name = 'Non-Color'
        normal = material.node_tree.nodes.new('ShaderNodeNormalMap')
        material.node_tree.links.new(normal_texture.outputs['Color'], normal.inputs['Color'])
        material.node_tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
    light_data = bpy.data.lights.new('Key', 'AREA')
    light_data.energy = 900
    light_data.shape = 'DISK'
    light_data.size = 5
    light = bpy.data.objects.new('Key', light_data)
    scene.collection.objects.link(light)
    light.location = (1, -3, 5)
    camera_data = bpy.data.cameras.new('Camera')
    camera = bpy.data.objects.new('Camera', camera_data)
    scene.collection.objects.link(camera)
    camera.location = (3.2, -5, 2.5)
    camera.rotation_euler = (Vector((1, 0, height / 2)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 3
    scene.camera = camera
    review = project / 'Exports/Reviews/RailingFamily'
    review.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(review / 'Railing.png')
    bpy.ops.render.render(write_still=True)
    assert before == hashlib.sha256(source.read_bytes()).hexdigest()


if __name__ == '__main__':
    main()
