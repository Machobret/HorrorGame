# Flare launcher reload reference and implementation

Reference: [Orion marine FAQ](https://www.orionsignals.com/marine-faq/), sections for 12-gauge break-action launchers. The animation uses the visual sequence of opening the barrel, replacing a single signal cartridge and closing it. Gameplay aiming remains the game's fictional behavior.

`FlareGunReloadVisual` opens both the inner barrel and front outer housing; it does not leave an immobile outer tube around a rotating inner mesh. The current FBX was inspected in Blender. In its imported model frame the hinge is at `(0, .0403, .0548)` metres, just above the forward trigger frame. Original vertex positions/UVs/material slots remain unchanged when closed.

Reload phases as fractions of the gameplay reload duration:

- 0.10–0.25: Support hand opens the barrel 55 degrees.
- 0.28–0.39: Withdraw the empty red case from the breech.
- 0.46–0.58: Support hand retrieves one replacement red cartridge.
- 0.58–0.73: Push the replacement into the open breech.
- 0.76–0.89: Close the barrel.
- 0.88–1.00: Return the arms and attachment to the equipped pose.

A dedicated AnimatorOverrideController replaces the template magazine animation with a steady ready pose. The procedural arm/chamber motion runs after the Animator; walking remains the lower body's responsibility. Cancellation immediately closes the chamber, hides temporary cases and restores the authored weapon attachment. Ammunition is transferred only by `PlayerStateReload`.

The presentation exposes `IsReloading`, `NormalizedTime`, `OpenAmount`, `HasInsertedRound` for isolated capture/validation. Capturing at 0.20, 0.35, 0.62 and 0.82 covers opening, extraction, insertion and closure.
