# A / D pivot stepping

PlayerTurnAnimation adds a procedural turn-in-place cycle to the humanoid player. A and D retain their existing movement/rotation controls. Feet alternate between a planted foot and a lifted pivot step, driven by actual yaw rotation. A short blend returns to idle; direction changes retain the current foot positions. Normal walking retains its existing locomotion animation.

PlayerMovement.Start installs the component once on the main humanoid Animator. No Animator controller layers or weapon overrides were replaced. The solver runs before weapon-handling solvers and wrist continuity, and acts only on the legs. Interaction, hurt, quick-turn, movement-disabled and rotation-constrained states do not start pivot stepping.

Default tuning: 38 degrees per step, 5cm clearance, 0.12s blend. Fields are on PlayerTurnAnimation. All rotation and translation remain owned by PlayerMovement; this component has no root motion and does not move the character controller.

Validated in Unity 6000.3.11f1 using real virtual keyboard input. The 59 checks cover left/right unarmed, MP5 held/aimed and 1911 aimed; visible foot lift, planted-foot stability, zero added translation, input release, and handoff to walking. A separate reversal capture checks rapid A-to-D input and idle settling. Its maximum per-frame foot movement was about 1.8cm and final yaw returned to the starting direction. Sources for both test/capture helpers are retained here outside Assets.
