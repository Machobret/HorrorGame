# Pushable crate

Drag **PushableCrate1.prefab** (1 m cube) or **PushableCrate1.5.prefab** (1.5 m cube) into your level. This is the complete crate from Horror Engine SampleScene2, including its original mesh, solid collider, push setup, scraping sound and pressure-plate weight. Its root is reset to ground origin; root scales are 0.5 and 0.75 respectively. Scale the root uniformly if needed.

Face a crate from nearby and tap Interact to grab it. Use W to push, S to pull, and A or D to move sideways with the crate. Tap Interact again to let go. The player and crate translate together along the crate local X and Z axes. It is a push object, not an inventory pickup or carry object.

Build/rebuild with **Horror Game > Environment > Build Pushable Crate**. Reads SampleScene2 in an isolated preview scene and never opens/replaces/saves your working scene. Uses independent reusable IDs and preserves them on rebuild. Save settings are retained from the template (the sample's SavableObjectState is disabled); enable and configure it if this crate should persist its position in saved games.

## Appearance

The 1 m crate uses `PushableCrate1_FramedMesh.asset`: recessed vertical boards, a thick wooden edge frame, real X braces on all four sides, and a flat planked top. Each vertical side carries a centred 80 x 50 cm HANDLE WITH CARE sign with a person pushing a box, mounted on a thin wooden backing over the braces. The combined mesh has two material slots and remains exactly 1 x 1 x 1 m. Its solid collider and pushing/climbing components are retained. The 1.5 m crate keeps its previous appearance.

Wood is sampled from the plain interior of the existing muted gray-brown atlas, avoiding its metal border. The handling label is point-filtered at maximum 512 pixels. **Horror Game > Environment > Update Small Crate Appearance** updates only the existing 1 m prefab without saving the open scene; the general builder also applies this appearance on rebuild. **Capture Small Crate Appearance** renders two opposite review angles to ignored `Exports/Reviews/PushableCrate`. Editable geometry and label provenance/prompt are documented in `ArtSource/Environment/PushableCrate/FramedCrate.md`.

## Native pushing handoff fix

HorrorEngine/Scripts/Player/PlayerStateMotion.cs keeps PlayerMovement enabled when transitioning specifically into PlayerStatePushing. Disabling/re-enabling it clears IntendedMovement, resets PushDetector contact intent and sends the player back to walking before the push animation starts. Other exit states retain their existing movement-disable behaviour.

Assigning PushInteraction on Pushable enables the grab and skips contact-based pushing. PlayerStatePushing consumes the existing Interact action to release the grab and uses local directional input while it is active. CrateGrab sweeps the player capsule and crate box, moves the crate using its existing Rigidbody, and plants the hands with the shared arm solver. The grab pose uses dedicated procedural push, pull and sidestep animations in CrateDragAnimation. Stride progress follows actual movement, and each planted shoe stays fixed while the other foot steps. The trailing foot closes in on sideways moves without crossing the lead foot. The player transfers weight between bent knees and settles when the crate stops. A pinned crate ends the grab. SprintClimb links an optional climbable triggered by running into a crate face.

### Movement references

- The [HSE manual handling technique booklet](https://assets.hse.ie/media/documents/Technique_Booklet_for_Supporting_Manual_Handling_and_People_Handling_Training_TOu5fct.pdf#page=16), pages 14?17, informed the push and pull stances, leg-driven weight transfer, and side step with uncrossed feet.
- The [HSE pushing and pulling overview](https://www.hse.gov.uk/msd/pushpull/overview.htm) covers sliding loads without wheels. Both game crates stay grounded through the move.

## Climbing on the one-metre crate

PushableCrate1 uses the native Climbable / PlayerStateClimbing system and the template 1m obstacle animations. Sprint directly into a crate face to climb onto the top. Walk out against an edge to climb back down. PushableCrate1.5 supports the same grab controls.

ClimbRig chooses the approached cardinal face (or the faced edge when descending). ExitTop, ExitBottom and the two hand contact markers are children of the moving crate. The actor uses its existing humanoid skeleton; CrateClimbRig adds a light arm IK correction during the animation's hand-attachment phase, with no new character rig or animation replacement. The crate is held still while climbing and restored to its previous physics settings afterwards.

Before starting, ClimbClearance checks the actual player capsule along the entry, vertical travel and exit segments, and requires a supported landing. Walls, low props and overhangs block the climb. Triggers, actor colliders and the climbed crate itself are excluded from travel obstruction queries; the crate can still provide landing support. Queries respect collision-layer ignores. The existing 0.25-second edge hold is retained; a blocked edge leaves the player on top with the guard still bound.

The native Climbable exposes an optional IClimbSafety policy, implemented only by this rig. PlayerStateClimbing calls it before entering and before every animation-driven Rigidbody.MovePosition. If a new obstacle blocks a step, the climb aborts to its entry pose, restores crate physics, and restores the character controller through the normal state exit. Before restoring the controller the rig also checks the ending pose against the crate and other solids. Ordinary template climbables without a safety policy retain their original behavior.

Use **Horror Game > Environment > Enable Small Crate Climbing** to apply the rig to the existing 1m prefab without rebuilding either crate. The main builder also includes it when rebuilding the 1m version. The climb setup and interaction data are owned copies, so template assets are unchanged.

Verification: **Horror Game > Tests > Check Small Crate Climbing** in Play Mode uses a temporary rotated crate and floor, tests native climb-up/down on all four sides, restores the player and removes the fixtures. Results go to ignored Exports/Reviews/PushableCrate/ClimbValidation.txt. No level is saved.

## Edge resistance and automatic climb down

After climbing onto PushableCrate1, walking toward an edge holds the player just inside the top. Continue walking outward for 0.25 seconds to start the native climb-down animation automatically. Releasing input, walking inward, changing the exit edge, or leaving normal movement resets the hold timer. EdgeHoldDuration and EdgeInset on ClimbRig tune the delay and safe edge margin.

CrateEdgeClimbGuard attaches only to the player while on this crate. PlayerMovement has a small ref-displacement FilterMovement event before CharacterController.Move; the guard uses it to resist the edge without teleporting the player back after a fall. IntendedMovement stays intact for movement intent/animation.

Edge-delay validation: **Horror Game > Tests > Check Crate Edge Delay** checks short-hold cancellation and automatic descent on all four edges of a rotated crate. Results are saved to ignored Exports/Reviews/PushableCrate/EdgeDelayValidation.txt.
