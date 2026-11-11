# Storage door keypad

Owned adaptation of Horror Engine's SamplePuzzleKeypad, attached to the storage door in Electricity_Puzzle_Dom. The original sample is unchanged.

- Default PIN: **1998**. Select `Door_Keypad` and edit the `Sequence Puzzle > Solution` array (one digit per entry).
- Interact with the locked door or wall keypad to enter the close-up. Use the normal UI directions and Confirm to press buttons; Cancel returns to gameplay. `C` clears; `<` removes the last digit.
- Incorrect entries play the sample error feedback and keep the door locked. A correct code shows OPEN, unlocks the door, and closes the view. Interact with the door again to pass through it normally.
- Both the solved puzzle and door lock participate in native save/load with separate unique IDs.
- The sample's old camera was replaced by an owned Unity 6 CinemachineCamera. Gameplay pauses and the player is hidden while the keypad is open.
- Native SequencePuzzle.Clear now also resets its entry count, so cancellation/clear after a partial PIN does not trigger an early error on the next attempt.

`Horror Game > Doors > Build and Attach Storage Keypad` attaches to the selected TwoSidedDoor, or the specific storage door anchor when none is selected. It preserves existing locks and skips an already attached keypad. Scene changes are undoable and marked dirty; save the scene normally to retain its placement.

Prefab: `Assets/Game/Doors/Keypad/Door_Keypad.prefab`.
Runtime controller: DoorKeypad; lock adapter: KeypadDoorLock.
Play check: `Horror Game > Tests > Check Storage Keypad`, while playing. It exercises wrong/correct PINs through the real button events, clear/backspace, cancel, and input/time restoration. Run on a fresh unsolved Play session; stop Play afterward to discard its solved test state.
