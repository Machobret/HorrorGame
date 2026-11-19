"""Export the devices, electrical box, and their mounts and antennas from Props.blend."""

import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


DEVICES = {
    "Screen": "SecurityScreen",
    "SpeakerBox": "IntercomSpeaker",
    "Button": "ControlButton",
    "Keycard": "KeycardReader",
}


def world_vertices(obj, material=None):
    if material is None:
        indices = range(len(obj.data.vertices))
    else:
        indices = {
            index for face in obj.data.polygons
            if obj.material_slots[face.material_index].material.name == material
            for index in face.vertices
        }
    return [obj.matrix_world @ obj.data.vertices[index].co for index in indices]


def export(obj, name, origin, target, surface_name):
    mesh = obj.data.copy()
    mesh.transform(Matrix.Translation(-origin) @ obj.matrix_world)
    old_slots = [face.material_index for face in mesh.polygons]
    slot_map = {}
    old_materials = list(mesh.materials)
    mesh.materials.clear()
    for index in sorted(set(old_slots)):
        material = old_materials[index]
        name_in_export = surface_name if material.name == "Material" else material.name
        if name_in_export not in {"DeviceSurface", "ElectricalBox", "Light_Universal", "Light_Red", "Light_Green"}:
            raise ValueError(f"Unexpected material on {obj.name}: {material.name}")
        export_material = bpy.data.materials.get(name_in_export) or bpy.data.materials.new(name_in_export)
        slot_map[index] = len(mesh.materials)
        mesh.materials.append(export_material)
    for face, index in zip(mesh.polygons, old_slots):
        face.material_index = slot_map[index]
    model = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(model)
    bpy.ops.object.select_all(action="DESELECT")
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    bpy.ops.export_scene.fbx(
        filepath=str(target / (name + "_Model.fbx")),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        bake_anim=False,
        path_mode="STRIP",
        mesh_smooth_type="FACE",
        use_triangles=True,
    )
    bpy.data.objects.remove(model, do_unlink=True)


def main():
    source = Path(bpy.data.filepath)
    if source.name != "Props.blend":
        raise ValueError("Open ArtSource/Environment/Props.blend before running this export.")
    target = source.parents[2] / "Exports/Reviews/InteractableDevices/Models"
    target.mkdir(parents=True, exist_ok=True)

    antenna = bpy.data.objects["Interactible_Antenna"]
    antenna_origin = antenna.matrix_world.translation.copy()
    atlas = bpy.data.images["Devices_base_color.png"]
    (target / "Device_BaseColor.png").write_bytes(atlas.packed_file.data)
    box_atlas = bpy.data.images["ElectricityBox_base_color.png"]
    (target / "ElectricityBox_BaseColor.png").write_bytes(box_atlas.packed_file.data)

    source_emitter = antenna.data.materials[1].node_tree.nodes["Principled BSDF"]
    color = list(source_emitter.inputs["Emission Color"].default_value)
    strength = source_emitter.inputs["Emission Strength"].default_value
    export(antenna, "DeviceAntenna", antenna_origin, target, "DeviceSurface")
    tip_indices = {i for face in antenna.data.polygons if face.material_index == 1 for i in face.vertices}
    tip = [antenna.matrix_world @ antenna.data.vertices[i].co - antenna_origin for i in tip_indices]
    tip_center = sum(tip, Vector()) / len(tip)

    records = []
    for subject, name in DEVICES.items():
        obj = bpy.data.objects[f"Interactible_{subject}"]
        vertices = world_vertices(obj, "Material")
        low = Vector(min(v[i] for v in vertices) for i in range(3))
        high = Vector(max(v[i] for v in vertices) for i in range(3))
        # The positive Y face is the rear wall plane in the authored lineup.
        origin = Vector(((low.x + high.x) / 2, high.y, (low.z + high.z) / 2))
        export(obj, name, origin, target, "DeviceSurface")
        records.append(dict(name=name, source=obj.name, size=list(high - low), pivot=list(origin)))
    mount = next(record for record in records if record["name"] == "IntercomSpeaker")
    screen = next(record for record in records if record["name"] == "SecurityScreen")
    post = bpy.data.objects["Interactible_Post"]
    post_origin = Vector(screen["pivot"])
    post_vertices = world_vertices(post)
    export(post, "DevicePost", post_origin, target, "DeviceSurface")
    box = bpy.data.objects["Cube.003"]
    box_antenna = bpy.data.objects["Cube.004"]
    box_vertices = world_vertices(box, "ElectricalBox")
    low = Vector(min(v[i] for v in box_vertices) for i in range(3))
    high = Vector(max(v[i] for v in box_vertices) for i in range(3))
    box_origin = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    export(box, "ElectricityBox", box_origin, target, "ElectricalBox")
    export(box_antenna, "ElectricityBox_Antenna", box_origin, target, "ElectricalBox")
    (target / "Export.json").write_text(json.dumps(dict(
        devices=records,
        mountOffset=list(antenna_origin - Vector(mount["pivot"])),
        tipCenter=list(tip_center),
        emissionColor=color,
        emissionStrength=strength,
        box=dict(
            source=box.name,
            antennaSource=box_antenna.name,
            casingSize=list(high - low),
            redCenter=list(sum(world_vertices(box, "Light_Red"), Vector()) / 8 - box_origin),
            greenCenter=list(sum(world_vertices(box, "Light_Green"), Vector()) / 8 - box_origin),
        ),
        post=dict(
            source=post.name,
            footY=min(v.z for v in post_vertices) - post_origin.z,
            topY=max(v.z for v in post_vertices) - post_origin.z,
        ),
    ), indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
