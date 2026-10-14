"""Three solid, low-poly signal cartridges with an explicit material UV layout."""
from pathlib import Path
import math
import bpy

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'Assets/Game/Weapons/Models/FlareAmmo'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1
atlas = bpy.data.images.load(str(ROOT / 'ArtSource/Weapons/FlareAmmo/FlareAmmo_Atlas_Source.png'))
atlas.scale(512, 512)
atlas.filepath_raw = str(FOLDER / 'FlareAmmo_Albedo.png')
atlas.file_format = 'PNG'
atlas.save()
material = bpy.data.materials.new('Material.032')
material.use_nodes = True
texture = material.node_tree.nodes.new('ShaderNodeTexImage')
texture.image = atlas
material.node_tree.links.new(texture.outputs['Color'], material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])

vertices, faces, coordinates = [], [], []
def face(points, uvs):
    start = len(vertices)
    vertices.extend(points)
    faces.append(tuple(range(start, start + len(points))))
    coordinates.append(uvs)

def swatch(count, brass=True):
    center = (.615, .125) if brass else (.87, .12)
    radius = (.112, .112) if brass else (.07, .08)
    return [(center[0] + radius[0]*math.cos(2*math.pi*i/count), center[1] + radius[1]*math.sin(2*math.pi*i/count)) for i in range(count)]

segments = 16
for x, height, stagger in [(-.0126, .012, -.004), (.0126, .012, .004), (0, .0338, 0)]:
    rings = [(-.045, .0123), (-.042, .0123), (-.041, .0115), (-.027, .0115), (-.026, .0109), (.039, .0109), (.043, .0101)]
    def point(y, radius, t):
        angle = -math.pi/2 + t*2*math.pi
        return (x+radius*math.cos(angle), y+stagger, height+radius*math.sin(angle))
    for ring_index, ((a, ra), (b, rb)) in enumerate(zip(rings, rings[1:])):
        for i in range(segments):
            t0, t1 = i/segments, (i+1)/segments
            points = [point(a, ra, t0), point(a, ra, t1), point(b, rb, t1), point(b, rb, t0)]
            if ring_index < 4:
                va, vb = .05+.16*ring_index/4, .05+.16*(ring_index+1)/4
                uvs = [(.04+.40*t0,va),(.04+.40*t1,va),(.04+.40*t1,vb),(.04+.40*t0,vb)]
            else:
                # Lettering runs along the case rather than wrapping upside down
                # around its circumference. The seam is on the underside.
                u0, u1 = (a+.026)/.069, (b+.026)/.069
                uvs = [(u0,.27+.72*t0),(u0,.27+.72*t1),(u1,.27+.72*t1),(u1,.27+.72*t0)]
            face(points, uvs)
    face([point(rings[0][0], rings[0][1], i/segments) for i in reversed(range(segments))], list(reversed(swatch(segments))))
    # Dark shallow crimp in the front, surrounded by a red lip.
    face([point(rings[-1][0], rings[-1][1], i/segments) for i in range(segments)], swatch(segments, False))
    # Recessed primer at the brass base.
    primer_uv = [(.615+.028*math.cos(2*math.pi*i/segments),.125+.028*math.sin(2*math.pi*i/segments)) for i in range(segments)]
    face([point(rings[0][0]-.0002, .0034, i/segments) for i in reversed(range(segments))], list(reversed(primer_uv)))

# Narrow worn retaining band around the three-round triangular bundle.
outline = [(-.0265,.001),(.0265,.001),(.0265,.015),(.010,.0458),(-.010,.0458),(-.0265,.015)]
for i, (x,z) in enumerate(outline):
    nx,nz = outline[(i+1)%len(outline)]
    face([(x,-.020,z),(nx,-.020,nz),(nx,-.011,nz),(x,-.011,z)], swatch(4,False))

mesh = bpy.data.meshes.new('FlareAmmoBundle')
mesh.from_pydata(vertices, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name='UVMap')
for polygon, points in zip(mesh.polygons, coordinates):
    for loop_index, point in zip(polygon.loop_indices, points):
        uv.data[loop_index].uv = point
obj = bpy.data.objects.new('FlareBullet', mesh)
bpy.context.collection.objects.link(obj)
obj.data.materials.append(material)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.export_scene.fbx(filepath=str(FOLDER/'FlareAmmo.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    bake_space_transform=True, bake_anim=False, path_mode='STRIP')
print('FLARE_AMMO_BUILD_OK', len(mesh.polygons), 'faces', tuple(obj.dimensions))
