# Superman player skin and CPU cape

`Superman_Player.prefab` is a gameplay variant of the existing Horror Engine player. The base player, its avatar, controller, skeleton hierarchy, and weapon attachments are preserved. This asset does not replace current scene or spawn references automatically.

`Superman_Visual.prefab` contains the same animated model without gameplay scripts. Open `Assets/Sandbox/Superman/Superman_ClothReview.unity` and press Play to review animation and cloth. Space advances animations; R resets the cape.

## Cape attachment

The body-fitted `Superman_CapeAttachment` is ordinary skinned geometry, sharing the body's surface vertices and bone weights. It also contains a stiff upper-back panel bound to the chest. The single-surface hanging `Superman_Cape` uses Unity CPU Cloth, with 13 pinned particles under that panel and 208 simulated particles below. Its object is bound directly to `DEF-spine.003` through the original bind matrix; it needs no second skinning pass. A separate attachment keeps shoulder motion independent of the solver. The hanging mesh is rebuilt to suit cloth simulation using the supplied cape's dimensions. The author's `Simulated` group is a reference, not a constraint on attachment placement.

The exported cape's second UV channel stores the simulation mask in X (zero = pinned). The builder matches Unity's welded cloth particles geometrically to that mask, rather than assuming FBX vertex indices survive import. A two-sided material renders the single simulation surface from either side.

Body collision proxies follow the torso, arms, and legs. They are triggers on Ignore Raycast, so they do not provide solid contacts or ordinary raycast targets. Cloth collision only uses explicitly assigned proxies; arbitrary level geometry is not automatically included. `SupermanCape` clears inertia after re-enable or a move over 3 metres / turn over 120 degrees in a frame.

The shoulder shell has 22 mm of rest clearance. The stiff back attachment sits 80 mm behind the body; free fabric starts at least 60 mm behind the visible body where it overlaps the skin. The collision proxies extend beyond the limbs for earlier contact. The body and shoulder shell share an explicitly triangulated surface to avoid crossing diagonals on bent shoulder quads.

Free cloth uses a generous motion envelope, no rest-surface backstop, and low damping with no bending resistance. Surface Penetration is disabled using Unity's `float.MaxValue` sentinel; zero would leave the constraint enabled. Tethers and stretch constraints retain the cape's length. This lets gravity keep the hem down when the chest pitches forward. The upper torso collision proxy follows `DEF-spine.003` so it bends with the attachment.

## Source and rebuild

Editable masters and recipes are in `ArtSource/Characters/Superman`:

- `Superman_Source.blend`: preserved copy of the user's `Superman1.blend`.
- `Superman_PlayerRig.blend`: fitted skeleton, body, shoulder attachment, and cloth sheet. Opens with flat color-only inspection settings; neutral lights are available for shaded review.
- `prepare_superman.py`: Blender rigging/export recipe. Run using Blender `--factory-startup --background --python <script> -- <source.blend>`.
- `render_review.py`: run on the rigged Blender master to generate unlit review views.

In Unity, use **Horror Game > Characters > Rebuild Superman and Cloth Review**. This updates generated assets in place and preserves their GUIDs. Existing scene placement is not retained when rebuilding the dedicated sandbox scene.

**Validate Superman Cloth** samples existing animation clips, checks deformation, hand alignment, and shoulder fit, then exercises actual CPU cloth in Play Mode. It checks the chest-bound pins against their current attachment positions, cloth movement, and teleport/re-enable resets. Batch entry point: `HorrorGame.Characters.Validation.SupermanValidation.BuildAndRunBatch` (omit `-quit`; it exits on completion). Logs, measurements, and captures go to ignored `Exports/Reviews/Superman`.

`HorrorGame.Characters.Validation.SupermanLookDownValidation.RunBatch` adds a 55-degree chest bend inside the animation stream, verifies the actual chest rotation, measures the hem against gravity, checks pinned particles, and checks recovery to upright. Its captures and report are under `Exports/Reviews/Superman/LookDown`. The showcase's `Freeze Motion` and `Look Down Degrees` inspector controls allow manual inspection of the same pose.
