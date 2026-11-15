# Two-sided doors

## Procedural building doors

Door exit markers sit 0.65 m from the doorway centreline. The interior, exterior and glass variants inherit both markers from `Door_Base`. The project's vendor edits to `Assets/AssetStore/HorrorEngine/Prefabs/Door.prefab` and `CrossSceneDoor.prefab` set their single destination marker to the same distance. Check those two offsets after updating Horror Engine.

`Prefabs/Door_Base.prefab` is the reusable starting point. It contains `TwoSidedDoor`, the shared Door interaction data, a bidirectional interaction trigger, a separate solid leaf collider, and two prewired child exits facing away from the doorway. `Interactive.OnInteract` calls this instance's `TwoSidedDoor.Use(IInteractor)`. No exit setup is needed when choosing another visual.

Choose **Door_Placeholder** in the House Toolkit door palette for the default plain-wall door. It is a variant of Door_Base with the original 4 x 2 interior atlas and its two-sided door mesh. Each toolkit opening has a stable automatic design. Select the placed prefab and set **Door Atlas > Design** to 0-7 to override it, or -1 for automatic. This changes the renderer property block and keeps the material shared.

**Inserts** also contains the eight interior and eight exterior designs and **Door_Glass**. Each uses its original gameplay prefab. Glass is the storefront default and also fits a plain wall. Both families use a 0.9 x 2.1 m door aperture. The prefab owns interaction, locks, and its exits. The house owns placement. Its Scene tool can select an insert or its surrounding wall.

## Custom door setup

Add `HorrorEngine.TwoSidedDoor` to a door object instead of `SceneDoor`. Keep custom game code under `Assets/Game/Doors`; no Horror Engine source changes are needed.

1. Create two empty child objects, one on each side of the doorway, clear of walls and colliders. Place them roughly symmetrically and rotate each to face away from the door.
2. Assign side A to **Exit Point** and side B to **Other Exit Point**. The farther point is selected when interaction starts; an exact tie selects B.
3. Keep the normal `Interactive`, collider, and interaction data setup. Wire `Interactive.OnInteract` to `TwoSidedDoor.Use` with the dynamic IInteractor argument, or wire the inherited `PlayerUse()` as existing door prefabs do. The interaction collider/detector must be reachable from both sides.
4. Assign the usual door animation if desired. The scene still needs the engine's standard `DoorTransitionController`, UI, and pause setup.

The script uses the engine's transition controller, teleport helper, OnOpened/OnLocked events, and transition messages. `MapDoor.Door` accepts this component. Actor teleport events, camera transition listeners, and actor disabling follow the normal engine path. Existing paired and cross-scene doors are unchanged.

Attach one normal `DoorLock`, `DoorLockKeyItem`, or `DoorLockOtherSide` on the same object, along with the lock's usual AudioSource/SavableObjectState dependencies. Lock state has one owner and uses the existing save system. Side A calls `OnTryToUnlock`; side B calls `OnTryToUnlockFromExit`. Consequently an other-side lock blocks A and unlocks from B. Key locks honor **Use Key Both Sides**. Existing lock semantics are preserved: unlocking can require a second interaction to actually pass through.

Distance selection assumes opposite, approximately symmetric exits. Very uneven exit placement can choose the wrong physical side. No prefab or scene is automatically converted.

## Verification

Run the Edit Mode fixture `TwoSidedDoorTests`. It covers exit selection, ties, lock-side dispatch, retry/reentrant-use protection, invalid exits, actor teleport events/rotation, and a round trip through the engine transition controller with a child PlayerInteractor.

In batch mode, the runtime fixture temporarily replaces the engine's cached WaitForEndOfFrame with a normal frame yield and restores it in finally. Unity does not resume WaitForEndOfFrame in a batch editor. This checks transition flow and messages, not rendered camera timing, visuals, audio, or door-animation assets. Check those with the final prefab in Play Mode.
