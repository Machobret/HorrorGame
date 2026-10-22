# Flare gun and revolver reload review

## Changes

- Flare gun: 2.5 second break-action sequence, moving outer housing and inner barrel, visible spent case and replacement red flare, support-hand handling, controlled closure.
- Revolver: 3 second swing-out-cylinder sequence, spent-case ejection, replacement cartridges and loader for a full reload, support-hand handling and controlled closure. Partial reloads show only the incoming rounds.
- Dedicated Animator Override Controllers replace the template magazine-reload movement with a stable base pose; procedural arm and mechanism posing supplies the weapon-specific action.
- Both presentations use the existing reload state. They do not transfer ammunition themselves. Manual reload, walking with sprint blocked, inventory-close queuing and aim-release behavior remain in force.
- Cancellation closes the mechanism, hides temporary cartridges and restores the weapon attachment.

## References

- [Orion marine FAQ](https://www.orionsignals.com/marine-faq/): break-barrel launcher geometry and single-cartridge loading sequence.
- [Colt Python operator manual](https://www.colt.com/wp-content/uploads/2023/02/2020pythonmanual.pdf): swing-out cylinder and cartridge-handling sequence, loading pages 21-24 and unloading pages 31-32.

These are stylized procedural animations on the existing low-poly character. The character uses mitten-like hands; this does not add individually animated fingers or a new character rig.

## Captures and validation

GIFs and phase sheets are rendered from the real player and weapon prefabs in Play Mode, using a separate mechanism-review camera in FlareGun_TestRoom. They show a close-up of the existing PSX models, rather than generated concept art. The main scene is not modified by the capture helper.

`ReloadPresentationValidation.cs` is an isolated editor helper kept outside Assets. It drives real aim/reload/movement input and checks full reload, partial reload through the actual inventory UI, walk-only movement, aim latch, and cancellation reset. Captures use 24 game frames per second. See the saved validation log for results.

Validation completed in Unity 6000.3.11f1 on 2026-10-19: both presentation checks passed; all five weapons passed the reload gameplay regression; 105 inventory reload assertions passed. Prefab reserialization in the isolated project only reordered the added component blocks.

## Lower elbows and closer reload pose

Revised both presentations after video review:

- Sea Tow Foundation, How Do I Use a Flare Gun?: https://www.youtube.com/watch?v=NImL2cHvcaA (opening/insertion/closing around 0:10-0:20).
- Gun Talk / Colt, Revolver Reloading Tips: https://www.youtube.com/watch?v=Ccbz3nTO7IM (compact handling near the abdomen, visible around 0:35 and 1:10 onward).

The arm solver now blends toward explicit, low character-space elbow poles rather than preserving the raised aiming elbow plane. The flare gun is placed using the actual firing-hand grip at local (.13, 1.24, .24), rather than a small offset from the extended aiming weapon root. The revolver firing-hand target moves from .31 to .22 metres forward of the character origin. Both poles and hand position are editable on their reload presentation components. Blending keeps the equipped grip and smoothly restores the animation pose at the end.

Refreshed both GIFs and phase sheets from Play Mode. The updated pose passed both weapon-specific validation runs again: full manual reload, walk-only movement, partial inventory reload, aim latch and interruption reset. See PoseValidation.log. The all-five-weapon regression log above predates this pose-only adjustment.

## Aimed and lowered reload variants

All five equipped weapons now record whether reload began from an aimed or lowered pose (`PlayerStateReload.StartedAimed`). The template pistol, shotgun and rifle reload actions have a relaxed pose variant using both arms together at lower height and closer to the torso; dedicated flare and revolver IK has separate lowered working orientations/hand targets. The lower-body movement policy and ammunition transaction are unchanged.

The reload animation layer starts fading into ordinary idle/walking during the final 18 percent of the action, instead of holding an aiming-ready pose until the state exits. Both variants finish lowered, preserving the existing requirement to release and press aim again after reloading.

The flare gun now blends back toward the current underlying Animator pose rather than its captured starting aim pose. Its support hand withdraws to the left/down before the firing hand lowers. The revolver uses the same ordered withdrawal principle. This prevents the two forearms from crossing during the return.

The clips retain normal Animator time progression so existing magazine/socket animation events are not replayed by seeking the clip every frame. The pose differences are procedural animation layers on the existing weapon-specific clips, not ten independent FBX animation files.

`ReloadPoseVariantsValidation.cs` drives all ten real-input combinations. It captures the entire reload and 0.25 seconds after completion, records local hand/elbow/weapon metrics, and checks source classification, movement policy, completion counts and return to locomotion without snapping back to aim. Final previews are in `Variants/`; the older GIFs at the parent directory are earlier pose reviews.

Final validation: all ten aimed/lowered cases passed in Unity 6000.3.11f1. The all-five-weapon gameplay regression and 105 inventory assertions passed again. Flare final-return captures retain at least 7.8 cm lateral separation between the wrists in the aimed variant and 8.8 cm in the lowered variant; phase images were also reviewed for arm crossing.

## Current behavior: return to aim when aim remains held

This revision supersedes the earlier always-lowered completion rule. A reload that begins in the aiming state returns to that state when aim has remained held. Releasing aim at any point during reload cancels the return, including release during the last animation blend. Reloads begun lowered finish lowered. A fresh aim press is required if aim was released and pressed again during the reload.

`PlayerStateAiming.BlendReloadReturn` previews only the aiming animation in the final reload blend. It does not enable aiming inputs, auto-targeting, shooting, or movement before the reload transaction completes. Actual aiming-state entry follows the ammo transfer. Flare and revolver retain their ordered support-hand withdrawal and use the underlying aiming pose as their return target.

Validated in Unity 6000.3.11f1: all 15 cases (five weapons times held aim, released aim at 90 percent of the reload, and lowered start) passed source-state, movement-policy, ammo-count and final-state checks. Captures include the final 0.25 seconds; see AimReturn/All_Weapons_Return_Poses.jpg, the two GIFs, PoseMetrics.csv and Validation.log. Earlier comparison previews document previous behavior.

## Timing and elbow continuity revision

Current reload durations: 1911 1.50 s, shotgun 2.10 s, hunting rifle 1.30 s, revolver 2.35 s, flare gun 2.30 s. See Timing/README.md for measured audible intervals and the flare click placement map. The flare sound retains its existing GUID; an original backup and rebuild script preserve the source.

ReloadPlaybackSpeed is a per-state Animator multiplier for the two reload states. It scales the actual weapon override clip to the available reload duration, leaving locomotion and other states at their normal speed. Clip events advance naturally; there is no per-frame seeking. The multiplier resets on state exit/cancellation. WeaponAssetBuilder retains the new timings when rebuilding game weapons.

ReloadArmIK solves against a stable elbow pole, then blends local bone rotations against the animation pose. This removes the unstable bend-plane handoff from fading both the target and pole. Flare grip compensation follows the actual blended firing hand. Closing and withdrawing the support hand now use continuous paths. HandgunSupportPose begins its aiming support pose underneath the fading reload solver and carries its weight into Aiming, rather than starting a second elbow solution after reload ends. Interrupted aim previews are cancelled on exit.

Validation: all fifteen weapon/input-mode cases passed; six flare/revolver cases were captured at 60 fps through completion; the all-five-weapon gameplay regression plus 105 inventory assertions passed again. Timing/ElbowContinuity.json and PoseMetrics_60fps.csv record sampled return movement. The 60 fps previews mux the assigned reload WAV at the measured reload-start timestamp; they demonstrate the reload sound without room ambience or other game audio. Both MP4s were decoded to verify video frames and non-silent audio. Rebuild them with assemble_reload_audio_video.py (Pillow, NumPy, PyAV).

## Pocket reach and long-gun contact

The 1911, shotgun and hunting rifle lowered reloads now use the same stable-pole/local-bone-blend solver as the custom reloads. ReloadArmIK.cs and its existing GUID moved into HorrorEngine/Scripts/Animation so engine and game presentation code share the solver without an engine-to-game dependency. The pocket reach is handled separately from the gun-lowering transform: the hand stays beside the hip instead of being rotated inward/down with the weapon. An outward elbow pole avoids the near-collinear pole case as the wrist descends toward the pocket.

LongGunSupportPose supplies model-specific fore-end wrist anchors for the shotgun and hunting rifle. Their source mesh coordinate frames differ substantially (the shotgun is scaled and points along its local X axis); anchors and wrist orientation were measured from each live prefab. The complete visuals/socket hierarchy is seated five millimetres toward the firing palm. The support solve releases for pocket/loading motions and blends back underneath the return animation, then holds during idle, aiming, firing and rifle bolt cycling. Enable/disable restores the original visual transform. WeaponAssetBuilder explicitly reconfigures the rifle anchor after cloning the shotgun template.

Both sides of each long gun were captured before and after. An initial larger seating offset was reduced because it drove the stock into the sleeve. Final held/returned wrist-to-anchor residuals are approximately 5-11 mm on this nonuniformly scaled character skeleton; this is a wrist-bone alignment measurement, not a hand-mesh surface distance. The final grip images were visually checked for fore-end contact.

All nine 1911/shotgun/rifle input-mode cases passed. The all-five-weapon regression passed firing/manual-only reload, walking with animated feet, stationary rifle reload, held-aim return and actual inventory-close reload. The 105 inventory assertions passed. Current previews and numerical probes are under GripCorrections/.

## Shoulder participation in lowered reloads

The 1911, shotgun and hunting rifle pocket reaches now rotate the clavicle as part of the arm chain. The collarbone follows the target by a bounded amount (up to 14 degrees on the support side, 5.6 on the firing side), then the upper arm and forearm solve from that new shoulder position. All four joints blend against the underlying animation using local rotations. The solver never translates or scales bones to gain reach.

The pocket target has a minimum height of 0.94 actor-local metres. The extended-arm solve retains a 25-degree bend reserve. During the pocket phase the hand keeps its animation-local wrist orientation relative to the forearm, rather than trying to retain its old world orientation while the forearm rotates. Dedicated flare/revolver presentation callers retain their existing behavior through default optional arguments.

ShoulderCorrection/ contains a new side-by-side GIF, a pocket-action phase sheet and 60 fps pose records. The observed left elbow angles at the low pocket point are approximately 97 degrees (1911), 105 (shotgun) and 90 (rifle); local collarbone orientation differs from idle by approximately 12, 8 and 14 degrees respectively. These are bone measurements from this character, not a guarantee about every mesh surface. The phase sheet was visually checked. ReloadShoulderValidation.cs captures these three lowered cases; all nine aimed-held/aim-released/lowered cases also passed LongGunGripValidation.

Final gameplay regression: all five weapons passed manual-only reload, movement policy with animated walking feet, held-aim return and actual inventory-close reload. All 105 inventory assertions passed. Source hashes in the isolated validation project match the two modified runtime files.

## Raised elbow revision for 1911 and shotgun

The Gun and Shotgun data assets now select a higher outward elbow pole throughout the lowered reload, with the retrieving wrist held at least 1.13 actor-local metres high. This raises the upper arm while keeping the hand near the waist. The hunting rifle follows exactly the previous path. Dedicated flare/revolver handling and aimed reloads are unchanged.

RaisedArmCorrection/ is the current preview. Three lowered cases passed at 60 fps, including completion ammo counts, movement policy and return state. Matched-frame measurements against ShoulderCorrection/ show up to 19.3 cm elbow lift for the 1911 and 11.1 cm for the shotgun during the pocket window. Rifle hand and elbow positions in that window are unchanged. The new phase sheet was visually inspected; these measurements describe joint positions, not mesh-surface guarantees.

## Stronger shoulder-axis rotation

For the 1911 and shotgun lowered reloads, the support-arm solver now swivels the elbow plane outward by 40 degrees around the shoulder-to-wrist axis. This rotates the upper arm at the shoulder while retaining the loading wrist target. Existing local-rotation blending fades the swivel with the reload pose. The optional swivel defaults to zero for all other callers. The rifle remains on its previous path.

ShoulderSwivelCorrection/ contains the current GIF, phase images, 60 fps metrics and Unity validation log. All three lowered reload cases passed ammo, movement policy and completion-state checks. The rifle sampled hand and elbow coordinates match RaisedArmCorrection/ exactly. Phase captures were visually reviewed; appearance remains subject to the user's animation review.

## Wrist continuity across weapon states

The current transition review is WristTransitions/. Handgun and long-gun support weights now fade when releasing their pose instead of dropping or resetting immediately. PlayerWristContinuity, installed once by PlayerEquipment, finishes arm/wrist handoffs after the presentation solvers using a 0.14-second actor-relative correction. Reachable two-bone IK handles discontinuous hand positions; orientation blending handles wrist flips. Animation timings, gameplay transactions and limb lengths are retained.

All five weapons were exercised at 60 fps through equip, aim/lower, firing return, lowered reload, held/released aim reload and walking/stopping. WristTransitions/ contains before/after metrics and the review GIF. The all-five-weapon gameplay regression plus 105 inventory assertions passed. The dedicated README documents measurement scope and the preview capture setup. The previous shoulder/pocket previews describe earlier pose changes; the current work smooths their state transitions.
