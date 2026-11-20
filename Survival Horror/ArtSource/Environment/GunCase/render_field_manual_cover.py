import sys
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

project = Path(sys.argv[sys.argv.index("--") + 1])
destination = Path(sys.argv[sys.argv.index("--") + 2]).resolve()
destination.mkdir(parents=True, exist_ok=True)
weapons = project / "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun"
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=str(weapons / "Gun.fbx"))
art = next(obj for obj in scene.objects if obj.type == "MESH")
ink_collection = bpy.data.collections.new("Pistol illustration")
scene.collection.children.link(ink_collection)
for collection in list(art.users_collection):
    collection.objects.unlink(art)
ink_collection.objects.link(art)

points = [art.matrix_world @ vertex.co for vertex in art.data.vertices]
x_min, x_max = min(v.x for v in points), max(v.x for v in points)
z_min, z_max = min(v.z for v in points), max(v.z for v in points)
x_mid, z_mid = (x_min + x_max) / 2, (z_min + z_max) / 2
scale = 1.57 / (x_max - x_min)
art.data = art.data.copy()
for vertex, point in zip(art.data.vertices, points):
    vertex.co = Vector((.037+(point.x-x_mid)*scale, -.50+(point.z-z_mid)*scale, .30-point.y*scale))
art.matrix_world = Matrix.Identity(4)
art.data.update()

def linear(channel):
    x = channel / 255
    return x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4

def rgb(values):
    return (*[linear(ch) for ch in values], 1)

def emission_material(name, color):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = rgb(color)
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
    return material, emission

def ramp(nodes, values):
    node = nodes.new("ShaderNodeValToRGB")
    node.color_ramp.interpolation = "LINEAR"
    for i, (position, color) in enumerate(values):
        element = node.color_ramp.elements[i] if i < 2 else node.color_ramp.elements.new(position)
        element.position = position
        element.color = rgb(color)
    return node

def rectangle(name, x, y, z, w, h, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(-w/2,-h/2,0),(w/2,-h/2,0),(w/2,h/2,0),(-w/2,h/2,0)], [], [(0,1,2,3)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.location = (x, y, z)
    mesh.materials.append(material)
    return obj

def polygon(name, points, z, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(x, y, z) for x, y in points], [], [tuple(range(len(points)))])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(material)
    return obj


def set_socket(links, socket, value):
    if isinstance(value, (float, int)):
        socket.default_value = value
    else:
        links.new(value, socket)


def math_value(nodes, links, op, a, b=0, clamp=False):
    node = nodes.new("ShaderNodeMath")
    node.operation = op
    node.use_clamp = clamp
    set_socket(links, node.inputs[0], a)
    set_socket(links, node.inputs[1], b)
    return node.outputs[0]


def mix_color(nodes, links, a, b, factor):
    node = nodes.new("ShaderNodeMixRGB")
    set_socket(links, node.inputs[0], factor)
    for socket, value in zip(node.inputs[1:], (a, b)):
        if isinstance(value, tuple):
            socket.default_value = rgb(value)
        else:
            links.new(value, socket)
    return node.outputs[0]


def grain(nodes, links, position, scale, detail):
    node = nodes.new("ShaderNodeTexNoise")
    node.inputs["Scale"].default_value = scale
    node.inputs["Detail"].default_value = detail
    links.new(position, node.inputs["Vector"])
    return node.outputs["Fac"]


def print_material(name, color, faded):
    mat, emit = emission_material(name, color)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    position = nodes.new("ShaderNodeNewGeometry").outputs["Position"]
    fibers = grain(nodes, links, position, 290, 2)
    rubbed = math_value(nodes, links, "MULTIPLY_ADD", fibers, 2.4, clamp=True)
    # Tiny warm gaps make the lettering and orange band look printed into stock.
    rubbed.node.inputs[2].default_value = -1.48
    tint = mix_color(nodes, links, color, faded, rubbed)
    links.new(tint, emit.inputs["Color"])
    return mat


paper, paper_emission = emission_material("Worn cream cardstock", (224,199,153))
nodes, links = paper.node_tree.nodes, paper.node_tree.links
position = nodes.new("ShaderNodeNewGeometry").outputs["Position"]
coords = nodes.new("ShaderNodeSeparateXYZ")
links.new(position, coords.inputs[0])
coarse = grain(nodes, links, position, 6.4, 3)
fine = grain(nodes, links, position, 240, 2.5)
coat = ramp(nodes, [(.08,(186,147,98)), (.52,(223,196,151)), (.94,(241,223,182))])
mixed_grain = nodes.new("ShaderNodeMixRGB")
mixed_grain.inputs[0].default_value = .42
links.new(fine, mixed_grain.inputs[1])
links.new(coarse, mixed_grain.inputs[2])
links.new(mixed_grain.outputs[0], coat.inputs["Fac"])
x_abs = math_value(nodes, links, "ABSOLUTE", coords.outputs["X"])
y_abs = math_value(nodes, links, "ABSOLUTE", coords.outputs["Y"])
edge_x = math_value(nodes, links, "SUBTRACT", .966, x_abs)
edge_y = math_value(nodes, links, "SUBTRACT", 1.456, y_abs)
edge = math_value(nodes, links, "MINIMUM", edge_x, edge_y)
ragged = math_value(nodes, links, "ADD", edge, math_value(nodes, links, "MULTIPLY", fine, .018))
edge_wear = math_value(nodes, links, "MULTIPLY_ADD", ragged, -19, clamp=True)
edge_wear.node.inputs[2].default_value = .90
aged = mix_color(nodes, links, coat.outputs["Color"], (108,65,38), edge_wear)

spine = math_value(nodes, links, "ADD", coords.outputs["X"], .894)
crease = math_value(nodes, links, "ABSOLUTE", spine)
fold = math_value(nodes, links, "MULTIPLY_ADD", crease, -38, clamp=True)
fold.node.inputs[2].default_value = .72
fold = math_value(nodes, links, "MULTIPLY", fold, coarse)
aged = mix_color(nodes, links, aged, (135,84,52), fold)
rub = math_value(nodes, links, "ADD", spine, -.022)
rub = math_value(nodes, links, "ABSOLUTE", rub)
rub = math_value(nodes, links, "MULTIPLY_ADD", rub, -65, clamp=True)
rub.node.inputs[2].default_value = .55
aged = mix_color(nodes, links, aged, (247,224,173), math_value(nodes, links, "MULTIPLY", rub, coarse))
links.new(aged, paper_emission.inputs["Color"])

edge_board, _ = emission_material("Brown binding edge", (65,44,33))
leaves, _ = emission_material("Aged page ends", (167,135,92))
back, _ = emission_material("Deep brown backdrop", (47,37,29))
rectangle("Backdrop", 0, 0, -.08, 2.1, 3.1, back)
outline = [(-.965,-1.396),(-.944,-1.429),(-.912,-1.449),(-.475,-1.456),
    (.27,-1.452),(.89,-1.443),(.945,-1.413),(.967,-1.365),(.965,-.52),
    (.96,.25),(.964,1.338),(.945,1.407),(.902,1.452),(.47,1.456),
    (-.442,1.452),(-.884,1.456),(-.930,1.428),(-.963,1.386),(-.965,.7),
    (-.961,-.34)]
polygon("Back cover", [(x+.008,y-.014) for x,y in outline], -.045, edge_board)
polygon("Page block", [(x+.004,y-.009) for x,y in outline], -.024, leaves)
polygon("Manila front", outline, 0, paper)

def crease_line(name, points, material, radius):
    data = bpy.data.curves.new(name, "CURVE")
    data.dimensions = "3D"
    data.bevel_depth = radius
    data.bevel_resolution = 0
    stroke = data.splines.new("POLY")
    stroke.points.add(len(points) - 1)
    for point, (x, y) in zip(stroke.points, points):
        point.co = (x, y, .012, 1)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    data.materials.append(material)


crease_shadow, _ = emission_material("Corner crease shadow", (168,125,82))
exposed_fiber, _ = emission_material("Exposed paper fibers", (231,205,155))
for crease_points in (
    [( .781,1.451),(.837,1.426),(.873,1.390),(.899,1.361),(.919,1.333),(.962,1.299)],
    [(-.963,-1.292),(-.929,-1.327),(-.904,-1.355),(-.868,-1.396),(-.829,-1.421),(-.805,-1.451)],
):
    crease_line("Folded corner", crease_points, crease_shadow, .0023)
    crease_line("Fibers along fold", [(x-.005,y+.003) for x,y in crease_points[1:-1]], exposed_fiber, .0026)

ink = print_material("Charcoal letterpress", (42,31,25), (155,116,70))
orange = print_material("A-04 burnt orange", (158,69,34), (223,155,88))
light = print_material("Paper lettering", (244,223,180), (188,125,73))

for x in (-.84,.913):
    rectangle("Outer vertical rule", x, .001, .02, .008, 2.735, ink)
for x in (-.815,.888):
    rectangle("Inner vertical rule", x, .001, .02, .0035, 2.69, ink)
for side in (-1, 1):
    rectangle("Outer horizontal rule", .0365, side*1.37, .02, 1.76, .008, ink)
    rectangle("Inner horizontal rule", .0365, side*1.345, .02, 1.705, .0035, ink)
rectangle("Orange title band", .0365, .365, .04, 1.71, .245, orange)
rectangle("Footer rule", .0365, -1.116, .04, 1.58, .0045, ink)

bold = bpy.data.fonts.load("C:/Windows/Fonts/arialbd.ttf")
def text(label, y, size, material, maximum=None, tracking=1):
    data = bpy.data.curves.new(label, "FONT")
    data.body = label
    data.font = bold
    data.align_x = "CENTER"
    data.align_y = "CENTER"
    data.size = size
    data.space_character = tracking
    data.resolution_u = 10
    obj = bpy.data.objects.new(label, data)
    scene.collection.objects.link(obj)
    obj.location = (.0365,y,.07)
    data.materials.append(material)
    bpy.context.view_layer.update()
    if maximum and obj.dimensions.x > maximum:
        data.size *= maximum / obj.dimensions.x
    return obj
text("FIELD MANUAL", .98, .212, ink, 1.62, 1.05)
text("NO. 04", .738, .212, ink, 1.5, 1.04)
text("1911 PISTOL", .365, .147, light, 1.58, 1.04)
text("A-04", -1.26, .12, orange, 1, 1.11)

printed, printed_emission = emission_material("Sepia engraving", (201,174,133))
nodes, links = printed.node_tree.nodes, printed.node_tree.links
texture = nodes.new("ShaderNodeTexImage")
texture.image = bpy.data.images.load(str(weapons / "Gun_PSX_Albedo.png"))
texture.interpolation = "Linear"
gray = nodes.new("ShaderNodeRGBToBW")
links.new(texture.outputs["Color"],gray.inputs["Color"])
tone = ramp(nodes, [(0.008,(46,32,25)),(.045,(119,91,63)),(.105,(217,187,143)),(.32,(235,209,166))])
links.new(gray.outputs["Val"],tone.inputs["Fac"])
links.new(tone.outputs["Color"],printed_emission.inputs["Color"])
art.data.materials.clear()
art.data.materials.append(printed)
for poly in art.data.polygons:
    poly.material_index = 0

camera_data = bpy.data.cameras.new("Cover")
camera = bpy.data.objects.new("Cover", camera_data)
scene.collection.objects.link(camera)
camera.location = (0,0,6)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 3
scene.camera = camera
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1536
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "Standard"
scene.render.use_freestyle = True
layer = bpy.context.view_layer
layer.use_freestyle = True
lines = layer.freestyle_settings.linesets[0]
lines.select_by_collection = True
lines.collection = ink_collection
lines.select_crease = True
lines.select_border = True
lines.select_silhouette = True
lines.linestyle = bpy.data.linestyles.new("Engraving lines")
lines.linestyle.color = tuple(linear(c) for c in (39,29,24))
lines.linestyle.thickness = 2.2
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGB"
scene.render.filepath = str(destination/"FieldManual04_Cover.png")
bpy.ops.render.render(write_still=True)
print("FIELD MANUAL COVER RENDERED FROM GAME MODEL")

