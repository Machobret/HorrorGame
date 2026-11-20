# Chapter 1: District Power Puzzle

## Integrated opening

In `1stLevel_Integrated`, the Maintenance Schedule on the alley electricity box points to the tablet at Machobret's Corner Market. After picking up the tablet, use the electricity box's lightning icon on the alley map to authorize the lever. Pull the lever up to light the intercom green and unlock the gate. Use E at the gate to enter.

The electricity box shows red when disconnected on the tablet, yellow when connected with the lever down, and green when connected with the lever up. The tutorial highlights the electricity box until the supply is on.

`Chapter1Opening` ties courtyard passage and the intercom indicator directly to the alley breaker's supply. The intercom has no map marker or interaction. Its indicator is dark when disconnected, red when connected without power, and green when powered. Enable **Requires Unlock** on `IntercomStatus` for a powered intercom that should show yellow until an external control calls `SetUnlocked`.

Connected fixtures go dark during an outage. Motorized shutters hold their position when the breaker cuts power and resume their requested motion when it returns. Authorization, lever position, and progression persist in saves.

`DocumentDB` registers the Maintenance Schedule for inventory saves. The breaker uses the standard pickup animation's reach and finish timing. The supply changes when the lever interaction completes.

## Storage facility

The storage office contains a pistol, matching ammunition and the circuit diagram. The player comments on the pistol the first time they enter while it is still there. The first gun pickup opens the equip, aim, fire and reload tutorial. Tab opens inventory, and each new document shows a reminder for the Documents tab. The office button powers the main shutter's tablet control. The main shutter raises physically when selected on the tablet.

The first two storage bay shutters operate independently. Their tablet requests apply on close in the order selected. The Runner spawns before the first shutter rises, and the other bay contains the key. The encounter uses the local bake in `Scenes/StorageEncounter_NavMesh.asset`. Navigation links connect the bays to the corridor when their shutters are open.

The reverse of the Shutter Notice marks the overloaded bays with an X and tells the player to switch off their power. Each numbered bay button switches its interior light and local load. Turning off the marked bays powers the two corridor shutter controls. Either control moves the same physical shutter. The overhead fixtures retain their authored colors. Each of the eight storage bays has an exterior security screen that shows yellow while its shutter is closed and green once it has fully opened. The tablet markers stay at the centers of the shutters.

Author additional installations by assigning an `ElectricalBoxPower` to the local `DistrictPowerGrid`, `ElectricalFixtureFeed`, or `ElectricalDeviceFeed`. Assign a unique Horror Engine save ID and `SavableObjectState` to each cabinet that should persist. Fixture feeds hold explicit light references so unrelated lighting keeps its own supply. Horror Engine's `Interactive` checks an optional power callback before running a linked device's local action.

The revised cabinet and lever sources are in `ArtSource/Environment/ElectricalBox/Revised`. The assembly recipe is `Tools/Environment/BuildIntegratedBreaker.cs`. Stage the recipe in `Assets/Editor` of an isolated copy and set `BREAKER_ASSET_SOURCE` to the revised source folder before running `BuildIntegratedBreaker.Run`.

The jammed shutter at the end of storage is the demo exit. Interacting with it shows the closing note and offers a return to the main menu.

Press Interact at the crate to enter the stance. Move toward the crate to push or away from it to pull. Press Interact again to release it.

## Integrated tablet maps

Run **Horror Game > Chapter 1 > Rebuild Integrated Tablet Maps** after editing the store floor, courtyard, storage rooms, or circuit fixtures. The command projects the ground-floor geometry onto the courtyard approach and storage maps, updates each circuit from its linked world fixture, then saves the scene and tablet assets. The starting view covers the courtyard and its immediate gate approach.

## Dom scene third-person camera

`Scenes/Electricity_Puzzle_Dom.unity` uses `District_ThirdPersonCamera`. Its `DistrictThirdPersonCamera` component binds to the native registered player and follows at chest height with smooth turning. `CinemachineThirdPersonFollow` controls distance (3.2 m), shoulder offset and obstacle avoidance; the lens uses a 65-degree field of view. Adjust these components on the scene object to tune the view.

The scene's old fixed-camera object and standalone camera rendering/audio listener are disabled. HECore supplies the native camera output during Play Mode, so the Game view may say "No cameras rendering" outside Play Mode. Interaction closeups retain priority over the follow camera. Other scenes and shared camera prefabs are unchanged.

To reapply the setup to this open scene, use **Horror Game > Chapter 1 > Use Third Person Camera in Dom Scene**. This command saves the scene and preserves the existing layout.

Run **Horror Game > Chapter 1 > Rebuild District Puzzle Assets** in Unity to create or refresh the authored assets.

The generated test scene is `Assets/Chapter1/Scenes/Puzzle_Test/PuzzleTest.unity`.

## Authoring entry points

- `DistrictPowerGrid` holds the 100-power capacity and the eleven circuit definitions.
- `DistrictPowerTablet` is attached to the fuse-box tablet. Interact with it (or click it in the greybox) to open its power panel. A demand above capacity shuts the grid off; reset it at the same panel.
- `DistrictPuzzleMap` contains editable named polygon shapes for the power room, pharmacy, laundromat/apartments, and truck blockade.
- `PoweredKeyItemInteractable.prefab` has inspector fields for **Required Circuit**, **Needs Key Item**, **Consumes Required Item**, **Gives Key Item**, and **Starts Fire Alarm**. It deliberately has no washing-machine mesh.
- `ActivatableDoorShutter.prefab` and `ActivatableLamp.prefab` only require a grid reference and circuit id to be placed/configured.
- `FireSafetyLadderController` controls the pre-existing Horror Engine climbable ladder: it is active only when the confirmed alarm is on and the apartment emergency-exit circuit is powered.

## Generated puzzle wiring

1. Pharmacy shutter (10) + refrigerator (50) opens the pharmacy and permits collecting `CorrosiveChemical.asset`.
2. Laundromat shutter (10) + washers (50) permits the chemical insertion and raises the alarm.
3. Switching washers off and emergency exit (50) on drops the exterior climbable ladder.

Lights are individually configurable and use the remaining capacity. Enabling enough of them overloads the grid without changing the solution state.
