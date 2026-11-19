# Chapter 1: District Power Puzzle

## Integrated opening

In `1stLevel_Integrated`, the player starts at the far end of the street and approaches Machobret's Corner Market. The tablet is a non-blocking pickup. The player opens it with M, powers the alley intercom, and uses E at the courtyard gate. The main shutter shows **Shutter locked** until the player selects **Unlock Depot** on the tablet. Once unlocked, E takes the player through the shutter. The staff-room desk holds the circuit diagram for the storage puzzles.

`Chapter1Opening` connects the intercom, depot shutter, and storefront hours sign. The alley electrical box is the world anchor for its tablet circuit. The Corner Market scene instance uses its source layout and keeps real-time shadows off on its fixtures.

The jammed shutter at the end of storage is the demo exit. Interacting with it shows the closing note and offers a return to the main menu.

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
