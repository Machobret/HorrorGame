# Environment authoring architecture

Readiness refinement (2026-11-09): House background freshness is a coalesced FIFO, with at most one source checked per editor update. Explicit flushes drain it; commands/rebuilds publish synchronously through the existing native Undo owner. New audits wait for the current round to drain. This bounds read-only whole-level work without introducing delayed edit jobs.

For plain slab panels, the builder may replace immutable horizontal meshes while retaining native filter/renderer/collider bindings only for the same source face/plan, floor/ceiling role, vertical band, expected part layout and identity pose, without Boolean state. Height/owner/structural changes retain replacement. The existing full-hierarchy Undo publisher is unchanged; a tested diff-recording alternative failed Boolean restoration and was rejected. Retained records, not merely roots, remain in the next output list so retirement cannot delete the live binding.

`EnvironmentProjectSettings` owns only initial project content choices, persisted by Unity's ScriptableSingleton in ProjectSettings. The standard SettingsProvider and the non-editing palette expose those values. Existing creation commands copy references into House/path source before the existing Undo/publication boundary; project settings never become runtime or inherited per-object state. Example recipes remain explicit reference/test utilities, not a fallback content registry.

Current owner map, 2026-11-03. [Project decisions](PROJECT_DECISIONS.md) owns scope; [implemented contracts](REBUILD_CONTRACTS.md) owns behavior; [verification](RESTART_CHECKPOINT.md) records evidence and open issues. Earlier migration proposals are [historical](history/ARCHITECTURE_PLAN_before_2026-11-03.md), not additional requirements.

## Dependency direction

Views/tools request edits ? commands prepare and validate authored data ? one publication boundary records the accepted edit ? builders derive native output. Runtime contains no editor UI dependencies. Read-only interpretation does not call a viewport, selection controller or edit command.

| Concern | Owner | Does not own |
| --- | --- | --- |
| Planar topology, rooms, wall presence, openings, IDs and finishes | `FloorPlan` and its named partials | Native objects, mouse input or Undo |
| Levels, shared slabs/dividers/cuts, Boolean volumes and House defaults | `HouseStack`, `HouseVerticalLayout`, registered `HouseBuilding`/`HousePlan` components | A second editable room polygon or local floor heights |
| Compatible geometry and insert models | `WallFamily`, `WallModuleDefinition`, `WallOpeningInsert`, `WallKit` | Surface colours or room classification |
| 3D road/sidewalk/fence graph and settings | `TownPath`, focused `TownAuthoring` subclasses | House topology or game rules |
| Read-only derived geometry | `HouseGenerationData`, `HouseSlabLayout`, `HouseHeightWalls`, runtime `HouseWallLayout` | Scene-view caches or saved state repair |
| Candidate validity and accepted edits | Named House command adapters, `HouseEditPreview`, `HouseFinishDraft`, `HouseEditValidation`, `HouseEditCommit`; `TownCommands` | Pointer capture or field layout |
| Native output publication | `GeneratedOutputUndo`, with House/path root adapters | Geometry algorithms or new authored settings |
| Reconciliation and scheduling | `HouseRebuildCoordinator`, `TownRebuild` | Intermediate drag positions as a queue of rebuild jobs |
| Mesh identity, cache and disposal | `GeneratedMeshResources` | Deciding which walls/rooms to keep |
| Active editor and temporary visibility | House/Town editing contexts, `HouseLevelView` | Saved floor/material data |
| Gesture lifetime | House controller; Town editing context; shared `ScenePointerInput` native policy | Persistent semantic selection |
| Selection and pending intent | `HouseEditorSession`, `HouseEditSelection`, `HousePendingEdit` | Native control capture or renderer ownership |
| Picking and feedback | `HouseSceneQueries`, `HouseSceneVisibility`, `HouseSceneFeedback` | Mutating topology or rebuilding output |
| Properties/toolbar presentation | `HousePropertiesWindow`, shared field helpers and Inspector adapters | Alternative validation or inheritance rules |

## Follow a House edit

1. A tool retains pointer coordinates; selection is logical IDs. Clicking/hovering does not allocate a geometry candidate.
2. A drag's latest coordinates replace its previous coordinates. Preview feedback is lightweight; expensive validation is bounded and final release validates the actual release position. No backlog of generated movement jobs is stored.
3. `HouseEditPreview` isolates topology/stack/kit changes; `HouseFinishDraft` restricts finish edits. Commands reject stale source, invalid geometry and ambiguous property transfer before publication. No-op/rejected edits leave history unchanged.
4. `HouseEditCommit` records changed authored components, applies accepted values and flushes source recording. Preparation restores managed poses only.
5. `HouseOutputUndo` publishes output in that same user action. It records initialized new hierarchy, changed builder-owned bindings and retired native objects. Source creation/deletion adapters use this same owner. Background checks never create/delete output or append history; external changes wait for explicit Rebuild.
6. Builders share one `HouseGenerationData` during the pass. Native output changes invalidate views; they do not recursively create authored edits.

Renaming a level is source-only and needs no geometry publication. Asset settings use Unity's SerializedObject editor for source recording, then the same output publication. Structural creation records native authored objects before generated bindings refer to them. These are native API differences, not parallel geometry policies.

## Interpretation and mesh ownership

`HouseGenerationData` shares slab patches, logical/fitted height sections and source signatures for one synchronous read. Scene queries borrow a read within one GUI event; edits/Undo/output changes invalidate it. Wall-height interpretation never calls Scene picking. Picking's simplified wall layout intentionally omits opening fitting; rendering/collision use the complete validated layout.

Wall/collision height freshness reads that storey's graph and the slab immediately above; an unrelated lower contour does not invalidate an upper wall. The same signature writer retains a complete-stack scope for shared slab output. Storey poses/spans and malformed shared-surface diagnostics still participate. Within one generation read, a storey's pre-cut ceiling coverage is united once; wall-height fitting reads that outline at its uniform storey height instead of repeatedly splitting/sampling every room/material patch. Slab finishes keep their independent patches. Undivided room surfaces follow their room identity directly; partitioned and unowned surfaces retain geometric lineage matching. Material resolution runs once per logical wall side, with repeated modules only selecting their orientation.

`HouseInputSignatures` describes House geometry/appearance dependencies. `GeneratedMeshResources.SourceSignature` owns mesh freshness: imported subasset identity plus dependency hash, or raw content hash for transient/non-readable sources. `SourceKey` composes fitted variants without inventing another freshness rule. Geometry cache keys exclude materials.

Builders identify generated roots with explicit owners and stable group keys. A unique misplaced root can be recovered; duplicates produce a bounded diagnostic. Wall retention belongs to `HouseWallOutputBuilder`: unknown painted replacements are hidden and retained when a fit cannot safely replace them. The resource owner recognizes module sources and serialized mesh ownership; content equality never grants disposal permission.

Only proven generated meshes enter disposal. Persistent assets remain AssetDatabase-owned. Immutable scene-participating meshes, including restored owners, are evicted from our cache when unused but left to Unity's native asset collection after history releases them. Hierarchy lifetime uses native Undo; meshes do not need separate creation records. This avoids competing lifetime records for the same restored bindings.

## Input and view lifetime

The active editing context enables logical picking; changing its native EditorTool does not add a second hidden enabled state. Camera and foreign handle capture suspend input for their gesture. One controller owns each gesture's operation, SceneView and control ID. Tools own coordinates, not independent control-loss logic. Normal release may commit; navigation, consumed/ignored release, capture loss, replacement press, view/context loss, Undo and exceptions cancel the gesture. Only the captured view's matching native control is released. Release is immediate within that view; cancellation elsewhere queues one release for its next beforeSceneGui event, avoiding nested OnGUI dispatch and preserving foreign controls. No timeout or global hot-control reset is used.

Native camera input reaches Unity before logical Escape handling. Delete is consumed without editing while the camera owns input, protecting the authoring root. An idle Hand tool suspends pointer picking but still permits logical keyboard commands. No mirrored camera state or recovery flag is stored.

Hover is one read-only winner per event. Selection persists independently. Navigation is current native input state, never eased camera movement. Property drafts do not disable picking; changed drafts require Apply/Discard before a geometry edit. Queries cache only for the current event and cannot repair authored data.

Cutaway/isolation and native highlight suppression belong to the active context. They must be reapplied after both rebuilt output and native Undo restoration; Undo may restore renderers without running a builder. Changing floors preserves context tool intent, including Undo fallback after a floor disappears, while canceling gestures and clearing floor-local selection. Fresh context entry selects the default tool. Floor guides/targets use authored wall-less boundaries and floor dividers only; derived room seams have no separate target. All wall-point handles use the wall base. F frames selected wall bounds inside edit mode. Outside editing, Scene selection resolves generated House parts to their House root.

## Roads, sidewalks and fences

These are separate objects placed in an existing Unity level, not a world/scene container. `TownCommands` stages authored fields only, validates a copied path and joins compatible-height crossings. Settings staging uses JsonUtility so native component bookkeeping and transient references do not leak into the candidate transaction.

`TownPathGeometry` owns corridor/union and path UV interpretation. `TownSurfaceMesh` owns one low-poly planar topology, continuous height sampling, closed extrusion and shared collision surface. Road surfaces/optional sidewalks and standalone sidewalks reuse it. Fences place upright authored panels/posts; they do not bend panels into a road mesh. The output adapter retains keyed parts and builds accepted source. House/path adapters share only native output lifetime publication. Undo restores snapshots; background checks report staleness. A universal source transaction/scheduler framework is unnecessary.

## Maintenance checks

Before a change, locate its state, interpretation, edit and presentation owners above. Prefer removing duplicate policy over extracting another generic helper. Preserve IDs, manual overrides, no-op behavior, native Undo and save/reopen. Review changed call sites and lifecycle paths after tests; test counts alone do not establish architecture, physical interaction or target-PC performance.

Use [TESTING.md](TESTING.md) and the hidden disposable Unity runner. Never run destructive native test fixtures in the working editor. Current reports and unresolved issues belong only in [package notes](../README.md).
