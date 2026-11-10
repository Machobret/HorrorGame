# Two-sided doors

## Procedural building doors

`Prefabs/Door_Base.prefab` is the reusable starting point. It contains `TwoSidedDoor`, the shared Door interaction data, a bidirectional interaction trigger, a separate solid leaf collider, and two prewired child exits facing away from the doorway. `Interactive.OnInteract` calls this instance's `TwoSidedDoor.Use(IInteractor)`. No exit setup is needed when choosing another visual.

The sixteen `Door_Interior_*` / `Door_Exterior_*` prefabs are native variants of this base. The existing Waterpixels door-family presets reference those variants and remain compatible with the supplied 1 m and 2 m doorway segments. Place or replace them through the normal wall-family selector, including bulk edits. Outside House edit mode, the placed door is independently selectable; edit its ordinary prefab overrides, events, exit transforms or optional lock components there. Wall rebuilds move its attachment without recreating the prefab. Explicitly changing its preset creates that preset's defaults; arbitrary overrides are not transferred to a different door.

For a custom door, create a variant of `Door_Base`, replace its `Visual`, and assign that prefab to a compatible `WallOpeningInsert`. Keep the prefab root at doorway centre/floor height, with +Z facing the module front and unit scale. The base includes an unlocked transition door, not an animated swinging-leaf controller; assign the usual Door Animation or add custom gameplay in a variant if needed. The level still needs Horror Engine's normal shared `DoorTransitionController`, UI and pause setup. Existing scenes and game-wide settings are not modified by this integration.

`Door_Glass` is the storefront variant, connected through `Glass_DoorFamily`. Its Visual has frame and glass slots. The storefront import creates it only if missing and preserves custom preset assignments. For presets with `UseWallMaterialRoles`, a two-slot `Visual` renderer inherits host finishes; explicit instance material overrides survive subsequent host finish changes. The `PrefabDoorMaterialTests` fixture checks inheritance, override preservation and serialization.

`Tools > Game > Doors > Set up procedural door prefabs` creates missing base/variant assets and connects empty bundled preset assignments. It preserves existing prefabs and custom assignments on rerun. Run it again after replacing bundled presets with a toolkit update.

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
