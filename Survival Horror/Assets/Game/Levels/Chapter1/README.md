# Chapter 1: District Power Puzzle

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
