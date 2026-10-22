# Weapon wrist transitions

Current change: support-hand correction weights fade in and out instead of dropping to zero on aim exit or jumping to full weight on reload entry. PlayerWristContinuity runs after the presentation solvers, detects actor/Animator state changes, equipment rebinding and abrupt target-pose changes, and decays an actor-relative pose offset over 0.14 seconds.

The position correction uses the existing two-bone solver with a blended elbow pole. It does not translate or scale joints to extend the arm. Wrist rotation is blended relative to the actor, so body turning is preserved and a forearm handoff does not turn into a wrist flip. The filter settles completely; it is not a permanent extra grip offset. Equip/unequip explicitly notifies the filter. Disabled Animator/ragdoll animation does not receive corrections.

Validation uses real input at 60 fps for all five weapons: equip, aim/lower, fire/return, lowered reload, held-aim reload, released-aim reload, walking and stopping. It checks actual reload completion/ammo and final state, and records both wrist positions and quaternions. Peaks in the reports cover an entire named phase, including entry/return; they are not thresholds for every authored motion. Intended recoil/loading movement remains present.

Measured improvements include handgun aim exit around 94 to 14 degrees/frame and walk-stop wrist-position peaks around 33 to 6 cm/frame. Flare reload position peaks around 40-48 cm/frame fall to approximately 6-9 cm/frame. Small nonzero per-frame motion is expected while animating. Some originally smooth motions have slightly different peak speeds during the blend. This is smoothing of existing clips/procedural poses, not replacement animation assets.

WristTransitionValidation.cs and analyze_wrist_transitions.py reproduce the checks in the isolated Unity project. assemble_wrist_transitions.py creates the review GIF from transition windows. The flare panel is captured separately from the starting room position because the walking test moved the camera behind a wall during the combined run; its full five-weapon metric records remain in After_WristPoses_60fps.csv.
Final all-five-weapon gameplay regression passed: manual-only reload, animated walking without sprint, stationary rifle reload, held-aim return, actual inventory-close reload and 105 inventory assertions.
