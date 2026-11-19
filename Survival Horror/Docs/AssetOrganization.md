# Asset organization

## Ownership

| Location | Purpose |
| --- | --- |
| `Assets/Game` | Project-owned gameplay and reusable content |
| `Assets/Game/Weapons/<Weapon>` | Weapon model, materials, prefabs, audio, data, scripts and animation |
| `Assets/Game/Weapons/Ammunition/<Type>` | Ammo-specific content |
| `Assets/Game/Weapons/Ammunition/Shared` | Shared cartridge materials, packaging and side-atlas content |
| `Assets/Game/Weapons/Shared` | Cross-weapon scripts, editor builders and animation states |
| `Assets/Game/Enemies/<Enemy>` | Enemy implementation and content |
| `Assets/Game/Environment/ProjectedFinishes` | Shared building materials grouped by surface, with the House Toolkit finish library at the root |
| `Assets/Game/Environment/Lighting` | Reusable lighting systems, presets, skyboxes and fixtures |
| `Assets/Game/Environment/Editor/AssetLibrary` | Editor-only cross-pack prefab catalogue and browser; Window > Game > Asset Library |
| `Assets/Game/Rendering` | Presentation shaders, materials and scripts |
| `Assets/Game/UI` | Menus, fonts, branding and UI scripts |
| `Assets/Game/Levels/Chapter0` | Chapter-owned scenes, maps, scripts and prefabs |
| `Assets/Sandbox` | House/weapon playgrounds, lighting studies, recovery and unsorted assets |
| `Assets/AssetStore` | Existing vendor imports; retain package layouts |
| `Assets/Settings` | Render and input settings assets |
| `Assets/AddressableAssetsData` | Addressables-managed configuration |
| `ArtSource` | Editable masters, provenance and asset-specific generation recipes |
| `Tools` | Shared project automation and path utilities |
| `Exports/Reviews` | Ignored reproducible captures and validation output |

## Naming and folder rules

- Prefer `Subject_Role`: `Revolver_Pickup`, `Revolver_Examination`, `Revolver_Albedo`. Use PascalCase without spaces for new owned folders.
- Organize first by feature or asset family, then by type when useful. Four related files do not need four subfolders.
- Give an asset one authoritative home. Shared means multiple real consumers, not possible future reuse.
- Keep the current approved runtime version under Assets. Retain source masters and selected reference material outside Assets; use Git for revisions.
- Add a README for a feature with non-obvious entry points or rebuild steps. Update links when moving it.
- Existing numbered materials, legacy spacing, and old-looking variants are retained to avoid guessing their purpose. Rename or remove them only with verified ownership and usage.
- Do not create empty folder scaffolding, `Misc`, `New Folder`, or numbered backup copies.

## Paths and regeneration

Weapon builders use `WeaponAssetPaths.Content(category, name)`; art recipes use `Tools/Art/weapon_paths.py`. Both group generated output by its owning item. Update these ownership rules together when introducing a new family. Item identifiers and GUIDs must not change during reorganization.

Existing source recipes remain beside their source art so their relative-source relationships remain intact. Some recipes still require the original downloaded packs, Blender or local audio sources; reorganization does not supply those external inputs. Historical review bundles under ArtSource are preserved. Put new reproducible captures in `Exports/Reviews` instead.

Witch and Brute retain `Resources/Witch/...` and `Resources/Brute/...` beneath their enemy folders; runtime Resources keys are unchanged. Moving a Resources folder's parent is different from changing the key beneath it.

Use Unity's Project window or AssetDatabase to move Assets, preserving metadata. Update literal paths and dynamically assembled paths in builders, tests, source recipes, Build Settings and documentation. Keep scene bake data next to its owning scene.

## Vendor and sandbox boundaries

Horror Engine example scenes are in Build Settings. Game systems, item databases, core prefabs and source tools also reference vendor content. Do not delete ExampleContent or treat AssetStore as pristine without auditing existing project changes. New customization should prefer owned assets and prefab variants where practical.

The Horror Engine map renderer always draws the project?s navigation grid, and its map list shows discovered maps only. The unused runtime grid toggle and unknown-map label were removed from the vendor scripts.

Horror Engine room discovery uses all outline components on each room, matching the map renderer. Adjacent outlines can share edges without being treated as holes, and additional contours have their own line renderers. The shared map and document databases register Chapter 1's integrated tablet assets for save and load.

Horror Engine camera selection recognizes both the legacy fixed-camera prefabs and current Cinemachine cameras. Camera-relative movement uses the live output camera until a virtual camera is selected. Maintain Distance cameras anchor their travel limit to the authored virtual camera position so the camera can follow again when the player backtracks into the Chapter 1 shutter corridor.

The integrated Chapter 1 tablet pickup unlocks its map without showing the item splash. Inventory ignores the opening key on the frame the screen appears so the same press cannot close it immediately.

Horror Engine documents use the shared UI actions for page navigation, confirmation, and dismissal on keyboard and controller.

Sandbox folders remain imported by Unity. Referenced sandbox content or Resources assets can still be included in builds. Promotion into Game requires checking ownership and updating dependencies; moving into Sandbox alone does not make content unused.

## Verification after moves

1. Confirm every moved asset retains its GUID and imports successfully.
2. Check path consumers and compare unresolved GUID references against the pre-move baseline, including package references.
3. Compile the project, open enabled build scenes and representative sandboxes, and check for missing scripts.
4. Run relevant existing gameplay and Edit Mode checks. Keep logs and captures outside runtime folders.
5. Review Git changes for unrelated edits and unintended vendor changes.

`Assets/HorrorEngineSettings.asset` is an intentional root exception: Horror Engine's settings provider hardcodes its path. Keep it there unless the vendor integration is deliberately changed.

The local Classic64 pebble surfaces use matching names in the source archive, vendor changelog, Unity materials and import catalog. The source preparation script recognizes the original download by texture checksum.
