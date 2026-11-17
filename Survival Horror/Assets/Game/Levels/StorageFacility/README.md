# Northline Storage Facility — playable blockout

Open `Scenes/StorageFacility_Blockout.unity` and press Play. The scene uses one native HECore, the project's existing CharacterPlayer spawn, native CameraPOV volumes, native map pickup/renderer, Interactive prompts, dialogs, and FlaggedState/SavableObjectState persistence. No existing level or game manager prefab is replaced.

## Route and scale

Arrival yard → utility check-in terminal → utility disconnect → entrance/reception → office/map/service module → local access shutter → storage spine → B102 feeder → distribution bay → warehouse vestibule → high-bay storage.

- Yard: 32 × 20 m, with closed exterior units, delivery parking and a service corner.
- Office: 8 × 7 m; lobby: 8 × 9 m. The office connects back to reception and provides the native facility map and a service-module progression point. The rest-point plinth is reserved for a future save/resource location; it is not presented as a functioning save station.
- Entrance dogleg: 4 m wide. Its turn hides the storage spine until the first access control is reached.
- Storage spine: 3 × 24 m with eight numbered unit bays. A102 and B102 are open; the other bays are closed storage masses. Opposing open units give a small exploration loop before the second shutter.
- Distribution bay: 9 × 9 m; warehouse vestibule: 9 × 5 m.
- Warehouse: 27 × 23 m, 7.5 m high. Three 18 m rack rows leave cross-aisles and circulation around both ends. Dispatch is a closed endpoint reserved for a future scene connection.

White/gray industrial shells, blue/teal interior shutters, orange outside doors and rack beams, simple fluorescent fixtures, concrete floors, restrained corporate signage. All are blockout materials and primitives, not final art.

## Provisional interactions

1. Register the service tablet at the yard terminal.
2. Restore Power Grid 1 at the adjacent cabinet. The entrance shutter releases.
3. Visit reception. Collect the native facility map from the desk and activate the service-module pickup point.
4. Puzzle 1: enable local corridor access at the dogleg. It requires utility power and opens the first shutter.
5. Puzzle 2: authorize feeder B inside B102. It requires power, the service module and the first access step. It opens the north corridor shutter.
6. Puzzle 3: release distribution access in the service bay. It requires the feeder and module and opens the warehouse route.

These are deliberately small teach-and-test access steps. The service tablet/module are interaction placeholders with persistent progression flags, not a replacement tablet UI or new inventory architecture. Full power allocation, item puzzle rules and enemy encounters are not finalized. Every prerequisite and released barrier is a serialized scene reference on StorageAccessPanel, which bridges native Interactive to native FlaggedState; its OnCompleted event is available for later power-system integration. Loaded native flags restore the same access states.

## Cameras and editing

There are three native CameraPOV zones: the template's existing third-person mode covers the facility, and two higher-priority fixed-angle views compose the south and north storage spine. The tracking mode resumes in open units, reception and warehouse aisles. There is no custom camera manager. A native HECorePrefabs prototype inherits the normal core and overrides only a CameraSystem prefab variant with cut blends. This avoids blending through partition walls; shared template camera assets are unchanged. Ceiling objects are grouped with the shell and can be hidden in the Scene view while editing.

The Create Blockout Scene menu under Horror Game > Storage Facility opens the scene once it exists; it does not overwrite hand edits. The editor-only builder is the initial reproducible source. BuildBatch regenerates this scene and its feature assets and is intended only for an isolated validation project, not a hand-edited scene. Keep scene asset GUIDs and the generated map registration when moving the level. It is intentionally not inserted into the main-menu start flow or build list until its place in the campaign is approved.

Background validation entry: `HorrorGame.StorageFacility.Editor.StorageFacilityValidation.RunBatch`. It checks managers, GUIDs, native spawn/map/interactions, collision-based reachability and shutter bypasses, and sampled gameplay camera visibility. Review artifacts belong in `Exports/Reviews/StorageFacility`.
