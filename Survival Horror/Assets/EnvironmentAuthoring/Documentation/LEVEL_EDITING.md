# Using the toolkit inside your level

Open your existing Unity level. Use the persistent **Environment Tools** Scene palette: **House, Wall, Road, Sidewalk**. Creation actions appear outside edit mode; while editing, the palette shows only the current object tools and **Done**. Done returns to creation. Wall also creates fences through its Wall / fence family selector. Click a creation button, then click a surface (or the ground plane) to place the first drawing point. Escape cancels before creation. Shift bypasses the grid. The matching **GameObject > Environment** menu items use the same placement tool. Edit those objects in place and assign their shared kit, model and material assets. Save your level normally; no starter scene or scene replacement is required.

Choose **Project defaults** in the creation palette (or **Project Settings > Environment Authoring**) to assign the initial House families, wall/fence model and finishes explicitly. These references live in `ProjectSettings/EnvironmentAuthoring.asset`; include that file and the referenced content when sharing a project. Creation copies the choices into the new object's existing settings. Later project-default edits leave existing objects alone. A project without configured defaults can still draw layouts and assign its own assets locally; the creation status explains the missing setup. No sample folder name or automatic family search determines normal creation.

The scenes under `Assets/Examples` are optional demonstrations and asset references. The readiness demonstration under `Assets/EnvironmentAuthoring/Reference/StarterLevel` combines two Houses, roads, sidewalks, a wall, a fence and cuts. Its walking controller and manually placed stairs belong to that demonstration; they are not added to your level when you create a toolkit object. Use your game's own player, lighting, terrain and other scene content.

## Buildings

Choose **House** in Environment Tools and click in your loaded level. The next click draws the first wall. The camera stays where it is.

Select a House and enter its edit mode through the Scene controls. Use the toolbar to select, draw walls or wall-less boundaries, add openings, edit slabs, attach trims or place Boolean cuts. Use the level selector to change storeys and Add Above/Below to extend the stack; choose the layout inheritance you need. Each storey has uniform height. Each storey owns its room/wall contour. Moving upper walls reshapes that room without automatically drawing a balcony around its old position. Shared slabs still cover the floor above and ceiling below; uncovered portions become exposed roof/underside faces. Create balconies explicitly with room boundaries and Outdoor classification.

House defaults supply materials, with level, room and individual wall-side overrides. Reset removes an override. Geometry comes from complete wall families; material assignments are independent. Door/window styles must match their rigid opening segment width and compatible insert model. The reference kit provides 1 m and 2 m opening segments and 0.5/1/2 m blanks.

Select one or more building walls and assign **Wall / fence family** to change their geometry to any Wall Family or Fence Family. Reset restores the level's Interior/Exterior default. Fence conversions preserve room boundaries, use the family's first panel and first available post at their authored heights, and remove door/window openings. Wall trims are suppressed while a fence is assigned. Fences stay horizontal on the storey plane; this supports landing and stairwell railings, not inclined handrails along a stair flight.

Click logical walls, points or floor areas to edit their properties. F frames selected walls. Shift bypasses the grid while retaining geometry attraction. Node/wall drags display a neutral outline; full geometric validation and the single Undo edit happen on release. An invalid release leaves the building unchanged. Escape cancels the gesture. Delete acts on supported logical elements while editing; it does not delete the House through an empty selection. Leave edit mode before deliberately deleting the whole object in the Hierarchy. Outside House edit mode, Scene clicks on generated walls, slabs and trims select the whole House root. Enter edit mode to select individual walls or rooms; explicit Hierarchy selection remains available for inspection.

A room's Cut away floor setting follows that room's changing contour. Independently drawn slab cuts remain independent. Box and Icosphere Boolean volumes cut generated walls/slabs and leave logical layout editable; newly exposed surfaces use the house-wide cut finish. Keep openings and cuts modest while laying out the level, then inspect the actual collision route in Play.

Trims reach from the wall centerline to the exterior face plus Projection. Shared wall/slab/trim construction materials use Triplanar with physical repeat dimensions. Authored UV mode remains available for baked details. World-registered projection can change texture phase when a building moves, and arbitrary corners can have seams.

## Roads, sidewalks, walls and fences

Choose **Wall**, **Road** or **Sidewalk** in Environment Tools, then click to place its first point. Drawing starts immediately. For fences, choose a Wall / fence family in properties. Use Draw to chain spans; right-click or Escape ends the chain. Select and drag points or spans, or use the Move gizmo. Delete removes the selected spans or a point and its attached spans. Settle down raycasts selected points (all points if none are selected) onto surfaces below, in one Undo action.

Roads and standalone sidewalks generate surfaces from straight connected XYZ paths. Roads need a width and material,
with optional left/right sidewalks; bends and T/X intersections require no junction assets. Select points or spans
and use the XYZ Move gizmo (W) to adjust elevation or horizontal position. Drawing raycasts surfaces; Settle down
places points on surfaces below. Road/sidewalk slopes follow point heights rather than automatically draping over
terrain. Width outlines show the proposed geometry before placing the next point. Curves, banking and overpasses
inside one path object are unsupported.

Walls and fences use shared panels and optional posts. Panels step horizontally on slopes; small ground gaps are an accepted limitation. Settle is an explicit edit, not a continuous terrain attachment. Surface picking uses Unity's native visible-geometry picker; point and path controls remain directly selectable.

## Scene workflow and limits

Keep House and town roots upright at unit scale. Move and yaw-rotate the roots to position them. Duplicate the entire root, including its generated children. Edit source layouts/settings rather than hand-editing generated output. Unknown substituted meshes are preserved, and may require deliberate restoration before regeneration.

Normal tool edits update immediately with Undo. External asset/script changes show a stale notice; use Rebuild to apply them. Deliberately forcing unused-asset collection during native deletion history can invalidate transient meshes too; Rebuild restores that output.

The reference project's PC renderer disables optional GPU Resident Drawer because the pinned Unity version produces shutdown allocation warnings with it enabled. Regular rendering remains in use; this is a project renderer choice, not a toolkit installation requirement. Large House edits can still pause on release; the measured costs and open performance work are recorded in the checkpoint.

Save your own scene, reopen it and check it in Play before doing a large amount of detailing. Generated meshes, renderers and colliders are saved scene content; the generators are editor tools. Prefab instances must currently be unpacked before layout editing. Existing top slabs provide flat roofs. Roof slopes, automated stairs, terrain deformation, streaming and procedural furnishing are outside this workflow.

The readiness demonstration is an acceptance fixture, not the basis required for your level or a large-town performance guarantee. Exact verification, measured dense-edit costs and unresolved physical interaction/native-allocation reports are recorded in [RESTART_CHECKPOINT.md](RESTART_CHECKPOINT.md). Use the [wall kit guide](WALL_KIT_AUTHORING.md) and [town module guide](TOWN_TOOLKIT.md) when assigning your own assets.

Prefab-backed doors are independently selectable outside House edit mode. Pick their family/preset using the existing opening controls; then use Done and click the door to edit its scripts and native prefab overrides. Wall rebuilds move the attachment and preserve the instance. The game supplies the preconfigured door prefab; this toolkit does not create exit points or gameplay scripts.
