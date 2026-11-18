"""Build the Corner Market's low-poly refrigerated cases and their shared pixel atlas."""

import argparse
import math
from pathlib import Path
import random
import sys

ROOT = Path(__file__).resolve().parents[3]
STORE = ROOT / "Assets/Game/Environment/NeighborhoodStore"

COLORS = [
    (198, 186, 151), (40, 52, 51), (96, 119, 125), (122, 68, 48),
    (98, 102, 93), (214, 207, 180), (151, 62, 44), (198, 125, 51),
    (73, 103, 126), (111, 143, 157), (130, 76, 99), (103, 130, 85),
    (66, 47, 35), (55, 88, 86), (170, 195, 185), (106, 105, 97),
]


def make_atlas(dependencies):
    if dependencies:
        sys.path.insert(0, dependencies)
    from PIL import Image, ImageDraw, ImageFont

    atlas = Image.new("RGB", (256, 256))
    font_path = ROOT / "Assets/Game/UI/Fonts/ExaltFallUI-Bold.ttf"
    rng = random.Random(219)
    legends = {2: "DAIRY", 3: "COLD SODA", 5: "MILK", 6: "COLA", 7: "ORANGE",
               8: "SELTZER", 9: "CULTURED", 10: "BERRY", 11: "APPLE",
               12: "FUDGE", 13: "JUICE", 14: "ICE CREAM"}
    headers = (2, 3, 13, 14)
    for index, base in enumerate(COLORS):
        tile = Image.new("RGB", (64, 64), base)
        draw = ImageDraw.Draw(tile)
        for _ in range(410):
            shade = rng.randrange(-16, 11)
            draw.point((rng.randrange(64), rng.randrange(64)),
                       fill=tuple(max(0, min(255, c + shade)) for c in base))
        if index in headers:
            strip = Image.new("RGB", (64, 16), base)
            label = ImageDraw.Draw(strip)
            color = (32, 48, 47) if index == 14 else (241, 223, 170)
            label.rectangle((1, 1, 62, 14), outline=color)
            label.text((32, 8), legends[index], font=ImageFont.truetype(str(font_path), 10),
                       fill=color, anchor="mm")
            tile = strip.resize((64, 64), Image.Resampling.NEAREST)
        elif index in legends:
            draw.rectangle((0, 17, 63, 44), fill=(222, 207, 170))
            draw.line((0, 20, 63, 20), fill=base, width=2)
            draw.line((0, 41, 63, 41), fill=base, width=2)
            for cx in (16, 48):
                draw.text((cx, 31), legends[index], font=ImageFont.truetype(str(font_path), 7),
                          fill=(63, 53, 45), anchor="mm")
        if index == 4:
            for y in range(5, 64, 7):
                draw.line((2, y, 61, y), fill=(39, 48, 44), width=3)
        elif index == 15:
            for y in range(9, 64, 17):
                draw.line((6, y, 41, y - 6), fill=(122, 121, 104))
        atlas.paste(tile, ((index % 4) * 64, (index // 4) * 64))
    atlas.save(STORE / "Textures/CornerMarket_ColdCaseAtlas.png")


def make_models():
    import bpy

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    atlas = bpy.data.images.load(str(STORE / "Textures/CornerMarket_ColdCaseAtlas.png"))
    atlas.pack()
    materials = []
    for name, alpha in (("ColdCase_Atlas", 1), ("ColdCase_Glass", .13), ("ColdCase_Header", 1)):
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        nodes = material.node_tree.nodes
        surface = nodes.get("Principled BSDF")
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = atlas
        texture.interpolation = "Closest"
        material.node_tree.links.new(texture.outputs["Color"], surface.inputs["Base Color"])
        if name == "ColdCase_Header":
            material.node_tree.links.new(texture.outputs["Color"], surface.inputs["Emission Color"])
            surface.inputs["Emission Strength"].default_value = .31
        surface.inputs["Alpha"].default_value = alpha
        surface.inputs["Roughness"].default_value = .75 if alpha == 1 else .24
        materials.append(material)

    class Case:
        def __init__(self, name):
            self.name = name
            self.collection = bpy.data.collections.new(name)
            bpy.context.scene.collection.children.link(self.collection)
            self.parts = [[], []]

        def mesh(self, name, vertices, faces, tile, glass=False, face_uvs=None, emissive=False):
            mesh = bpy.data.meshes.new(name)
            mesh.from_pydata(vertices, [], faces)
            mesh.update()
            mesh.materials.append(materials[2 if emissive else int(glass)])
            uv = mesh.uv_layers.new(name="UVMap")
            left, bottom = tile % 4 / 4, 1 - (tile // 4 + 1) / 4
            square = ((0, 0), (1, 0), (1, 1), (0, 1))
            for polygon in mesh.polygons:
                coordinates = face_uvs[polygon.index] if face_uvs else square
                for loop, (u, v) in zip(polygon.loop_indices, coordinates):
                    uv.data[loop].uv = left + (.009 + .982 * u) / 4, bottom + (.009 + .982 * v) / 4
            obj = bpy.data.objects.new(name, mesh)
            self.collection.objects.link(obj)
            self.parts[int(glass)].append(obj)
            return obj

        def box(self, name, pos, size, tile, glass=False, emissive=False):
            x, y, z = (v / 2 for v in size)
            vertices = [(-x,-y,-z),(x,-y,-z),(x,-y,z),(-x,-y,z),
                        (-x,y,-z),(x,y,-z),(x,y,z),(-x,y,z)]
            faces = [(0,1,2,3),(1,5,6,2),(5,4,7,6),(4,0,3,7),(3,2,6,7),(4,5,1,0)]
            item = self.mesh(name, vertices, faces, tile, glass, emissive=emissive)
            item.location = pos
            return item

        def container(self, name, pos, profile, label, sides=8):
            angles = [math.tau * i / sides for i in range(sides)]
            vertices = [(r * math.cos(a), r * math.sin(a), z) for z, r in profile for a in angles]
            height = profile[-1][0]
            faces, uvs = [], []
            for row, ((z0, _), (z1, _)) in enumerate(zip(profile, profile[1:])):
                for side in range(sides):
                    following = (side + 1) % sides
                    first, second = side / sides, (side + 1) / sides
                    faces.append((row*sides+side, row*sides+following, (row+1)*sides+following, (row+1)*sides+side))
                    uvs.append(((first,z0/height),(second,z0/height),(second,z1/height),(first,z1/height)))
            obj = self.mesh(name, vertices, faces, label, face_uvs=uvs)
            obj.location = pos
            for z, r in (profile[0], profile[-1]):
                ring = [(r * math.cos(a), r * math.sin(a), z) for a in angles]
                cap = self.mesh("Lid" if z else "Base", ring,
                                [tuple(range(sides)) if z else tuple(reversed(range(sides)))], 5)
                cap.location = pos

        def sign(self, center_y, height, width, tile):
            x = width * .5
            self.mesh("Printed header", [(-x, -.351, center_y-height*.5), (x, -.351, center_y-height*.5),
                      (x, -.351, center_y+height*.5), (-x, -.351, center_y+height*.5)], [(0,1,2,3)], tile, emissive=True)

        def export(self):
            meshes = []
            for part_type, parts in enumerate(self.parts):
                bpy.ops.object.select_all(action="DESELECT")
                for part in parts:
                    part.select_set(True)
                bpy.context.view_layer.objects.active = parts[0]
                if len(parts) > 1:
                    bpy.ops.object.join()
                obj = bpy.context.object
                obj.name = self.name + ("_Glass" if part_type else "_Body")
                bpy.context.scene.cursor.location = (0, 0, 0)
                bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
                meshes.append(obj)
            bpy.ops.object.select_all(action="DESELECT")
            for obj in meshes:
                obj.select_set(True)
            bpy.ops.export_scene.fbx(filepath=str(STORE / "Models" / f"{self.name}.fbx"),
                                     use_selection=True, object_types={"MESH"}, add_leaf_bones=False,
                                     bake_anim=False, axis_forward="-Z", axis_up="Y", path_mode="STRIP")
            return meshes

    rng = random.Random(713)
    for kind, header, trim in (("Soda", 3, 1), ("Dairy", 2, 1), ("Juice", 13, 1)):
        case = Case(kind + "Cooler")
        for x in (-.31, .31):
            for y in (-.23, .23):
                case.box("Foot", (x, y, .034), (.1, .1, .068), 1)
        case.box("Back", (0, .29, .97), (.82, .055, 1.8), 0)
        for x in (-.386, .386):
            case.box("Insulated side", (x, -.015, .97), (.048, .66, 1.8), 0)
        case.box("Compressor", (0, -.013, .18), (.724, .665, .224), 0)
        case.box("Vent", (0, -.352, .184), (.64, .012, .145), 4)
        case.box("Canopy", (0, -.014, 1.955), (.82, .66, .17), trim)
        case.sign(1.955, .112, .71, header)
        for x in (-.361, .361):
            case.box("Door upright", (x, -.357, 1.0775), (.04, .032, 1.575), trim)
            case.box("Interior shelf rail", (x*.9, .249, 1.08), (.016, .018, 1.5), 4)
        case.box("Cabinet light strip", (-.333, -.271, 1.085), (.017, .012, 1.41), 0, emissive=True)
        for z in (.308, 1.849):
            case.box("Door crossbar", (0, -.357, z), (.71, .032, .033), trim)
        for z in (.89, 1.25):
            case.box("Handle mount", (.299, -.381, z), (.025, .042, .031), 1)
        case.box("Pull handle", (.299, -.41, 1.07), (.026, .025, .391), 1)
        case.box("Door glass", (0, -.353, 1.0775), (.68, .006, 1.501), 15, True)
        for level, z in enumerate((.342, .695, 1.048, 1.401)):
            for y in (-.26, .225):
                case.box("Shelf price rail", (0, y, z), (.72, .023, .021), trim)
            for index in range(12):
                case.box("Shelf wire", (-.33 + index*.06, -.0175, z), (.008, .485, .008), 4)
            for row, row_y in enumerate((.126, -.115)):
                for index in range(7 if kind == "Dairy" else 8):
                    if rng.random() < (.10 if row == 0 else .42):
                        continue
                    x = (-.283 if kind == "Dairy" else -.294) + index*(.094 if kind == "Dairy" else .084)
                    y = row_y + rng.uniform(-.013, .013)
                    base = z + .015
                    if kind == "Dairy" and level < 2:
                        case.box("Milk carton", (x, y, base+.085), (.078, .081, .17), 5)
                        case.box("Folded carton top", (x, y, base+.176), (.058, .076, .015), 9)
                    elif kind == "Dairy":
                        case.container("Yogurt", (x, y, base), ((0,.026),(.057,.037),(.064,.037)), 9 if index%2 else 10)
                    elif kind == "Juice":
                        case.container("Juice bottle", (x, y, base),
                                       ((0,.032),(.015,.035),(.171,.035),(.204,.016),(.224,.016),(.232,.021)),
                                       (7, 10, 11)[(index//3 + level)%3])
                    else:
                        case.container("Soda can", (x, y, base), ((0,.031),(.009,.035),(.116,.035),(.124,.031)),
                                       (6, 7, 8)[(index//3 + level)%3])
        case.export()

    case = Case("IceCreamFreezer")
    case.box("Toe kick", (0, 0, .09), (1.04, .52, .15), 1)
    case.box("Compressor shell", (0, 0, .26), (1.12, .61, .24), 0)
    for x in (-.534, .534):
        case.box("Enamel side", (x, 0, .58), (.05, .61, .42), 0)
    for y in (-.28, .28):
        case.box("Enamel face", (0, y, .57), (1.02, .046, .40), 0)
        case.box("Lid track", (0, y, .795), (1.136, .044, .037), 1)
    for x in (-.548, 0, .548):
        case.box("Lid rail", (x, 0, .797), (.027, .545, .031), 1)
    for x in (-.269,.269):
        case.box("Sliding glass", (x, 0, .793), (.522, .518, .009), 15, True)
        case.box("Lid grip", (x, -.172, .81), (.091, .022, .029), 1)
    case.box("Front vent", (-.346, -.315, .24), (.29, .013, .12), 4)
    case.box("Printed freezer badge", (.12, -.309, .604), (.57, .012, .126), 14, emissive=True)
    for col in range(7):
        for row in range(2):
            x = -.435 + col*.146
            y = -.119 + row*.21
            if (col + row) % 3 == 0:
                case.container("Ice cream tub", (x, y, .424), ((0,.042),(.125,.049),(.143,.051)), 10 if col%2 else 9)
            else:
                item = case.box("Wrapped freezer stock", (x,y,.464), (.11,.163,.08), 12 if col%2 else 10)
                item.rotation_euler.z = rng.uniform(-.14,.14)
    case.export()
    for index, collection in enumerate(bpy.context.scene.collection.children):
        for obj in collection.objects:
            obj.location.x += index * 1.35
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name("CornerMarket_ColdCases.blend")))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--atlas", action="store_true")
    parser.add_argument("--models", action="store_true")
    parser.add_argument("--deps")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    if args.atlas:
        make_atlas(args.deps)
    if args.models:
        make_models()


if __name__ == "__main__":
    main()
