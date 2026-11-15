# Storage door keypad

Owned adaptation of Horror Engine's SamplePuzzleKeypad, attached to the storage door in Electricity_Puzzle_Dom. The original sample is unchanged.

- Default PIN: **1998**. Select `Door_Keypad` and edit the `Sequence Puzzle > Solution` array (one digit per entry).
- Interact with the locked **door** to open the keypad. The panel's separate world interaction is disabled, including on existing scene instances at runtime.
- Type digits with either the top number row or numpad. Arrows/WASD or gamepad directions move the highlight in the visible grid; Confirm/Enter presses the selected button. Escape/Cancel closes the panel. Backspace removes a digit; Delete clears the entry. The `C` and `<` buttons offer the same edit actions.
- Incorrect entries play the sample error feedback and keep the door locked. A correct code shows OPEN, unlocks the door, and closes the view. Interact with the door again to pass through it normally.
- Both the solved puzzle and door lock participate in native save/load with separate unique IDs.
- The sample's old camera was replaced by an owned Unity 6 CinemachineCamera. Gameplay pauses and the player is hidden while the keypad is open.
- The controller frames the whole panel for the current aspect ratio with a margin and owns a temporary Cinemachine camera override while viewing it. It updates in unscaled time, preventing paused blends from freezing at a cropped intermediate angle. The previous camera time setting is restored afterward. Input waits until the close-up camera activates.
- Native SequencePuzzle.Clear now also resets its entry count, so cancellation/clear after a partial PIN does not trigger an early error on the next attempt.

`Horror Game > Doors > Build and Attach Storage Keypad` attaches to the selected TwoSidedDoor, or the specific storage door anchor when none is selected. It preserves existing locks and skips an already attached keypad. Scene changes are undoable and marked dirty; save the scene normally to retain its placement.

Prefab: `Assets/Game/Doors/Keypad/Door_Keypad.prefab`.
Runtime controller: DoorKeypad; lock adapter: KeypadDoorLock.
Play check: `Horror Game > Tests > Check Storage Keypad`, while playing. It exercises wrong/correct PINs through the real button events, clear/backspace, cancel, and input/time restoration. Run on a fresh unsolved Play session; stop Play afterward to discard its solved test state.

Noninteractive regression checks: Unity `-batchmode -nographics -projectPath ".../Survival Horror" -executeMethod HorrorGame.Tests.KeypadTraversalBatchChecks.Run`. Run with the working editor closed. They check grid navigation, synthetic keyboard/numpad input, framing, paused camera selection, and crate clearance; results go to `Exports/Reviews/TraversalFix/BatchChecks.txt`. No scene is saved. The keypad prefab's redundant approach trigger is disabled as part of migration.

Vendor integration: Interaction3DContext adds opt-in external input, selection/press accessors, and a cancellation guard for its deferred activation. Other template 3D contexts keep their existing input behavior.
