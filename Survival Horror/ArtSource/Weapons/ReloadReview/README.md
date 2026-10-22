# Weapon reload and rifle chambering update

All weapons require an explicit reload. Empty shots and the last round never trigger reload automatically. Reload input works while lowered or aiming. ReloadableWeaponData.AllowMovementDuringReload defaults to true and is explicitly enabled for the 1911, shotgun, revolver and flare gun; the hunting rifle disables it. Mobile reloads use the existing upper-body animation layer and constrain running, including analog auto-run. The constraint clears on exit.

Ammo + gun combination queues a reload without modifying ammunition. After the inventory closes, Motion/Aiming starts the reload for the equipped weapon. Queued unequipped guns wait until equipped. The timer completes before reserve ammunition transfers. Interrupted reloads do not grant ammunition. Requests survive inventory rearrangement, clear on successful reload, and expire when the gun is removed/reset. The complete reload fills across reserve stacks without consuming extra ammo.

Reload completion returns to Motion. If aim is still held, the input listener waits for release and a fresh press before aiming again. This removes the revolver's automatic return to aim.

The hunting rifle uses a dedicated controller override with steady aiming clips instead of shotgun pump clips. Its right arm reaches the receiver, lifts the bolt handle, pulls back, ejects a spent bottleneck brass casing, closes and locks. Left arm remains under the fore-end. Ejected brass has a neck, shoulder, rim, open mouth and physics; no shotgun shell effect is instantiated. Magazine reload remains stationary and uses its existing full-body reload clip.

## Validation

Isolated Unity 6000.3.11f1 Play-mode validation passed for all five weapons. Real mouse/keyboard input verified last-round and empty-trigger behavior, R reloads, Shift+W walk-only motion, animated feet, the rifle's movement lock, ammo transfer timing, aim-release gating, real inventory Show/Combine/Hide and one-time reserve consumption. Inventory API tests passed 105 assertions, including multi-stack transfers, unequipped requests, interruption and reset/removal. Rifle captures show four points in the cycle and a real spawned spent-brass object. See Validation_Result.txt and Rifle_Bolt_Action_Review.png.

The saved C# validators live outside Assets. Copy them into an Editor folder in an isolated checkout to run ReloadGameplayValidation.RunBatch. The test log and capture outputs default to the temporary validation project path declared in that source. The existing flare test-scene validator now explicitly presses reload after its empty shot; it no longer assumes auto-reload.
