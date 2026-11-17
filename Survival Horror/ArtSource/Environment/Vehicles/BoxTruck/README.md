# Box truck

Place `Assets/Game/Environment/Vehicles/BoxTruck.prefab`. There is one prefab, one shared truck mesh, and one shared cargo material. The cab reuses the existing `Vehicles_Opaque_A` material.

`TruckCargoAtlas` follows `StorageBoxAtlas`: editor placements and duplicates receive a random design when their placement identity changes; the choice persists with the placement. Play Mode and runtime clones select a fresh design once per object lifetime. Disabling/re-enabling or moving an object preserves its current runtime choice. Random repeats are expected. Edit **Cargo Design** or use **Randomize Cargo Design** in the component context menu to select or reroll a design.

The six designs, top to bottom, are white riveted, blue, galvanized, red, green and white panel. The shader preserves authored UVs and offsets V by `-design / 6`. The atlas uses Repeat wrapping, including the authored dark wheel-well patches. No UV insets or scaling are added. The component sets only the instanced `_CargoDesign` property on material slot 1 through a `MaterialPropertyBlock`; slot 0 (cab) is untouched. Materials are shared; no runtime material copies are created. Keep the truck out of static batching, as with the random storage boxes. The prefab has no collider.

`BoxTruck.blend` is the user's modeled `Downloads/Cars_Car8.blend`, copied unchanged on 2026-11-17. `BoxTruck_Cargo_Albedo.png` is the supplied `Trucks_base_color.png`, copied unchanged. The hidden `Cube.001` helper is excluded. Cab faces use the source `AtlasUV`; cargo faces use `UVMap`, combined into the exported FBX primary UVs.

Run `export_box_truck.py` with Blender in background to regenerate `BoxTruck_Model.fbx`. `CreateTruckPrefabs.cs` records the Unity generation method for an isolated project with the truck assets and runtime component copied into `Assets/Truck`; generated mesh/prefab files and their metadata are copied back. Preserve existing asset metadata when regenerating. Review logs and renders are ignored under `Exports/Reviews/BoxTruck`.
