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
| `Assets/Game/Environment/NeonSigns` | Animated neon sign prefabs and their materials and frame atlases, grouped by design |
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

The active Horror Engine URP asset sets the additional-light shadow atlas limit to 4096 for the game. In the integrated Chapter 1 scene, select Cameras > Chapter 1 Lamp Shadow Distance to tune indoor lamps. Entries inherit the 15 m cutoff and 3 m fade band. Set a positive Distance Override for an individual lamp, or zero to inherit the default. Assign new indoor emitters to the list.

The Horror Engine map renderer always draws the project?s navigation grid, and its map list shows discovered maps only. The unused runtime grid toggle and unknown-map label were removed from the vendor scripts.

The Horror Engine UI input listener opens pinned documents from gameplay or the Reporter inventory. The inventory stays paused underneath so closing the overlay restores the same view.

Horror Engine room discovery uses all outline components on each room, matching the map renderer. Adjacent outlines can share edges without being treated as holes, and additional contours have their own line renderers. Circuit hover only highlights rooms the map has already drawn. The shared map and document databases register Chapter 1's integrated tablet assets for save and load.

Horror Engine interactions can filter prompts and activation by player position. The ControlButton prefab checks the front face and an unobstructed path for all child interactions. The guard-office button also uses its room outline. The pistol and flashlight item assets opt into direct inventory equip on Enter or double-click. The equipped flashlight prefab uses the project-owned switch to toggle its beam and battery drain together.

The player interaction detector gives reachable interactions with higher **Focus Priority** first choice of the prompt and Interact input. The Maintenance Schedule uses a scene override of 1 so it takes precedence beside the alley breaker.

Horror Engine camera selection recognizes both the legacy fixed-camera prefabs and current Cinemachine cameras. The shared output camera defaults to the perspective projection used by the fixed-camera preview. Camera Relative movement uses the input direction for travel while the character turns toward it. Held input keeps its movement axes through camera cuts and tracking rotation. A fresh movement button press, stick re-engagement from neutral, or deliberate stick turn adopts the live camera orientation. Fresh input in the first 100 ms after a cut uses the previous movement axes. Maintain Distance cameras anchor their travel limit to the authored virtual camera position so the camera can follow again when the player backtracks into the Chapter 1 shutter corridor.

The integrated Chapter 1 tablet pickup unlocks its map after the item splash is dismissed. Horror Engine's UIItem fits the splash sprite without stretching, and UIInputListener routes the Inventory and Map shortcuts through their shared open and close paths. The original tablet art and regeneration instructions are documented in the Tablet README.

The live inventory prefab opts into the reporter menu under `Assets/Game/UI/ReporterMenu`. Horror Engine's `UIInventory` and `UIDocs` keep existing entry points and forward into that menu when opted in. The separate pickup document reader retains its shared UI actions. Existing document IDs remain the save keys and the inventory save carries ordered pin and archive state.

The inventory prefab passes the optional ReporterMenuArt asset through Horror Engine's UIInventory entry point. Point-filtered preview copies live with ReporterMenu and leave the shared item textures available to their other consumers.

The main menu renders its Chapter 1 backdrop from the **Main Menu Exterior** hierarchy in `Assets/Game/UI/MainMenu/Scenes/MainMenu.unity`. The backdrop shares source assets with Chapter 1 and stores generated meshes and material overrides in the adjacent `MainMenuExteriorVisuals.asset`. The Horror Engine menu prefab draws its controls over the camera and uses its scene transition for the fade.

Horror Engine uses Tab as the default keyboard inventory binding. Crate prompts take priority when the player faces a pushable crate within reach. Forward input pushes and backward input pulls while in the crate-moving stance. Interacting again releases the crate. Weapon obstruction rays check solid colliders.

The Horror Engine GameManager prefab and pickup implementation use the shared clips in `Assets/Game/Sounds` for document and item pickups, including pickups inherited from vendor prefabs.

Sandbox folders remain imported by Unity. Referenced sandbox content or Resources assets can still be included in builds. Promotion into Game requires checking ownership and updating dependencies; moving into Sandbox alone does not make content unused.

## Verification after moves

1. Confirm every moved asset retains its GUID and imports successfully.
2. Check path consumers and compare unresolved GUID references against the pre-move baseline, including package references.
3. Compile the project, open enabled build scenes and representative sandboxes, and check for missing scripts.
4. Run relevant existing gameplay and Edit Mode checks. Keep logs and captures outside runtime folders.
5. Review Git changes for unrelated edits and unintended vendor changes.

`Assets/HorrorEngineSettings.asset` is an intentional root exception: Horror Engine's settings provider hardcodes its path. Keep it there unless the vendor integration is deliberately changed.

The local Classic64 pebble surfaces use matching names in the source archive, vendor changelog, Unity materials and import catalog. The source preparation script recognizes the original download by texture checksum.

English and Korean are the active game locales. The Horror Engine language setting, startup locale and string table collections reference the project-owned Korean assets in `Assets/Game/UI/Localization`. `GameText` resolves legacy Horror Engine `LocalizableText` values and custom UI text from the English/Korean catalog in that folder. Add an entry there for new legacy labels or add the translation to the corresponding Unity table for table-backed text.
