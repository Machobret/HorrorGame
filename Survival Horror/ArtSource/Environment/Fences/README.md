# Fence library naming

`Names.json` is the explicit naming table for the imported fence library in this project. Family names begin with the main material and describe the construction: BrickWall, StoneWall, WoodRailFence, MetalChainLinkFence, ConcreteSlabFence, PlasterWall and WoodPicketFence. Mixed-material pieces retain their owning kit, with secondary materials in the variant name. Standalone plastic gates and the metal gate handle use their own material prefix.

Native pieces use `Family_Role_Variant`, for example `ConcreteSlabFence_Panel_ClosedFull` and `StoneWall_Post_TileCoping`. Models append `_Model`; prefab and panel-module names match the piece. Baked materials use `M_Material_SubjectAtlas_Variant`; texture names describe the subject and end in `_Albedo`.

The original pack's `plackard_wall` is a precast concrete slab fence. Its previous `M_Wood_PanelsBaked_01` classification was incorrect. Source GLB paths, hashes and material labels remain provenance, not display names. Imported FBX mesh/slot names and local file IDs are retained because native references depend on them. Post selectors show the normalized model filename rather than the embedded source mesh name.

`Assets/EnvironmentAuthoring/Content/FenceKits/Downloaded/FenceSources.json` records current native names/paths alongside `sourceName`, `sourceStyle`, original GLB paths and hashes. Its material paths point to this project's EnvironmentAuthoring content, not the source project's pre-integration roots. ConstructionSources.json uses those same current texture paths.

## Applying the naming table

Run from the Survival Horror project root. These commands maintain imported copies; they never modify the original downloaded pack or the separate Procedural Generation project.

1. `python Tools/Environment/fence_names.py prepare` writes the reviewable GUID-based plan under ignored `Exports/Reviews/FenceNaming`.
2. Run Unity 6000.3.11f1 against this project with `-batchmode -nographics -quit -executeMethod EnvironmentAuthoring.Editor.FenceLibraryNaming.Apply`. AssetDatabase performs the moves, retaining `.meta` files and GUIDs. The tool checks native bindings and exercises drawing for every family. It does not save working scenes.
3. `python Tools/Environment/fence_names.py finish` updates native manifest paths and visible reference-scene captions, then checks GUIDs, serialized references, source bytes and manifest paths.
4. Reopen Unity or run the same batch command with `EnvironmentAuthoring.Editor.FenceLibraryNaming.Verify` to check the persisted result. `python Tools/Environment/fence_names.py verify` repeats the file-level audit.

If this project is already open, do not launch a second editor against it. Write `apply` (or `verify`) to `Exports/Reviews/FenceNaming/request.txt` before a script refresh. The editor consumes this explicit request once after reload and writes `request.txt.result`. The same operations are available under Tools > Environment. There is no recurring asset watcher and no working-scene save.

Keep the prepared plan until verification finishes: it contains the pre-move identity, binding and binary-hash baseline. Re-export/import work must apply this table to the resulting native assets; do not derive materials from opaque source names or replace existing GUIDs. The separate source project's exporter is not installed in Survival Horror.

The 2026-11-11 normalization covers seven family folders/catalogs, 39 panel modules, 80 models, 80 prefabs, 13 materials and 13 textures. Geometry, material settings, source hashes, catalog ordering, native IDs and manual/repeat dispositions are unchanged. Half, single, chipped and broken slab specials remain manual prefabs; naming does not turn them into repeat modules.
