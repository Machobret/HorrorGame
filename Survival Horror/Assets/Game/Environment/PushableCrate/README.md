# Pushable crate

Drag **PushableCrate1.prefab** (1 m cube) or **PushableCrate1.5.prefab** (1.5 m cube) into your level. This is the complete crate from Horror Engine SampleScene2, including its original mesh, solid collider, push setup, scraping sound and pressure-plate weight. Its root is reset to ground origin; root scales are 0.5 and 0.75 respectively. Scale the root uniformly if needed.

Use the native player and walk into a box face, continuing toward it to start the pushing animation. Keep floor and travel space unobstructed. PushAxis on the root limits movement to the crate local X and Z axes. It is a push object, not an inventory pickup or carry object.

Build/rebuild with **Horror Game > Environment > Build Pushable Crate**. Reads SampleScene2 in an isolated preview scene and never opens/replaces/saves your working scene. Uses independent reusable IDs and preserves them on rebuild. Save settings are retained from the template (the sample's SavableObjectState is disabled); enable and configure it if this crate should persist its position in saved games.

## Appearance

The 1 m crate uses `PushableCrate1_FramedMesh.asset`: recessed vertical boards, a thick wooden edge frame, real X braces on all four sides, and a flat planked top. Each vertical side carries a centred 80 x 50 cm HANDLE WITH CARE sign with a person pushing a box, mounted on a thin wooden backing over the braces. The combined mesh has two material slots and remains exactly 1 x 1 x 1 m. Its solid collider and pushing/climbing components are retained. The 1.5 m crate keeps its previous appearance.

Wood is sampled from the plain interior of the existing muted gray-brown atlas, avoiding its metal border. The handling label is point-filtered at maximum 512 pixels. **Horror Game > Environment > Update Small Crate Appearance** updates only the existing 1 m prefab without saving the open scene; the general builder also applies this appearance on rebuild. **Capture Small Crate Appearance** renders two opposite review angles to ignored `Exports/Reviews/PushableCrate`. Editable geometry and label provenance/prompt are documented in `ArtSource/Environment/PushableCrate/FramedCrate.md`.

## Native pushing handoff fix

HorrorEngine/Scripts/Player/PlayerStateMotion.cs keeps PlayerMovement enabled when transitioning specifically into PlayerStatePushing. Disabling/re-enabling it clears IntendedMovement, resets PushDetector contact intent and sends the player back to walking before the push animation starts. Other exit states retain their existing movement-disable behaviour.

## Climbing on the one-metre crate

PushableCrate1 uses the native Climbable / PlayerStateClimbing system and the template 1m obstacle animations. Press the normal interaction button beside the crate to climb onto its top; face an edge on top and interact again to climb down. Walking into it still pushes. PushableCrate1.5 remains push-only.

ClimbRig chooses the approached cardinal face (or the faced edge when descending). ExitTop, ExitBottom and the two hand contact markers are children of the moving crate. The actor uses its existing humanoid skeleton; CrateClimbRig adds a light arm IK correction during the animation's hand-attachment phase, with no new character rig or animation replacement. The crate is held still while climbing and restored to its previous physics settings afterwards. Keep the chosen side and landing space unobstructed.

Use **Horror Game > Environment > Enable Small Crate Climbing** to apply the rig to the existing 1m prefab without rebuilding either crate. The main builder also includes it when rebuilding the 1m version. The climb setup and interaction data are owned copies, so template assets are unchanged.

Verification: **Horror Game > Tests > Check Small Crate Climbing** in Play Mode uses a temporary rotated crate and floor, tests native climb-up/down on all four sides, restores the player and removes the fixtures. Results go to ignored Exports/Reviews/PushableCrate/ClimbValidation.txt. No level is saved.

## Edge resistance and automatic climb down

After climbing onto PushableCrate1, walking toward an edge holds the player just inside the top. Continue walking outward for 0.25 seconds to start the native climb-down animation automatically. Releasing input, walking inward, changing the exit edge, or leaving normal movement resets the hold timer. Manual interaction descent remains available. EdgeHoldDuration and EdgeInset on ClimbRig tune the delay and safe edge margin.

CrateEdgeClimbGuard attaches only to the player while on this crate. PlayerMovement has a small ref-displacement FilterMovement event before CharacterController.Move; the guard uses it to resist the edge without teleporting the player back after a fall. IntendedMovement stays intact for movement intent/animation. This is the only new vendor integration for edge resistance; other scenes and unguarded movement are unaffected.

Edge-delay validation: **Horror Game > Tests > Check Crate Edge Delay** checks short-hold cancellation and automatic descent on all four edges of a rotated crate. Results are saved to ignored Exports/Reviews/PushableCrate/EdgeDelayValidation.txt.
