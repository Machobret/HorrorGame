# A / D pivot stepping

PlayerTurnAnimation adds a procedural turn-in-place cycle to the humanoid player. A and D retain their existing movement/rotation controls. Feet alternate between a planted foot and a lifted pivot step, driven by actual yaw rotation. A short blend returns to idle; direction changes retain the current foot positions. Normal walking retains its existing locomotion animation.

PlayerMovement.Start installs the component once on the main humanoid Animator. No Animator controller layers or weapon overrides were replaced. The solver runs before weapon-handling solvers and wrist continuity, and acts only on the legs. Interaction, hurt, quick-turn, movement-disabled and rotation-constrained states do not start pivot stepping.

Default tuning: 38 degrees per step, 5cm clearance, 0.12s blend. Fields are on PlayerTurnAnimation. All rotation and translation remain owned by PlayerMovement; this component has no root motion and does not move the character controller.

Validated in Unity 6000.3.11f1 using real virtual keyboard input. The 59 checks cover left/right unarmed, MP5 held/aimed and 1911 aimed; visible foot lift, planted-foot stability, zero added translation, input release, and handoff to walking. A separate reversal capture checks rapid A-to-D input and idle settling. Its maximum per-frame foot movement was about 1.8cm and final yaw returned to the starting direction. Sources for both test/capture helpers are retained here outside Assets.

## Walk, run and aim

The main character's existing Character controller uses one blend point per gait and controller speed. The stock walk and run clips retain their footstep events and paths, with softer arm bends and reduced torso roll. Forward walk playback follows the current movement speed more closely.

PlayerAimStance on the Male43 Animator resolves a shorter stationary stance before pivot stepping. It retains the authored foot heights and limits the drop in pelvis height when looking up. The handgun support solver uses character-relative elbow poles, and the MP5 grip follows its aim pitch. Wrist continuity still blends state and equipment handoffs.

## Forward walk reference pass

The forward walk clips use a lower swing, a longer rear-toe support phase, and a small lateral transfer toward the supporting leg. The unarmed upper-arm curves give the hands room to swing back below the shoulders. The two-handed walk uses the same foot timing and a smaller chest rotation. The forward blend point runs at 1.27× for the existing 2.5 m/s pace, with sound and dust events aligned to the leading foot contacts.

Real-world references were the University of Utah filmed natural gait, the University of Oklahoma Health Sciences Center's stance and swing phase breakdown, and the Collins et al. study of arm swing during human walking. The footage guided the poses and contact sequence for a brisk in-game walk. [Natural gait video](https://neurologicexam.med.utah.edu/adult/html/gait_normal.html#02) · [Gait phases](https://www.ouhsc.edu/bserdac/dthompso/web/gait/terms.htm) · [Arm swing experiment](https://biomechatronics.stanford.edu/dynamic-arm-swinging-videos)

## Arm animation

`LongGunSupportPose` eases the firing hand during locomotion, before solving the support wrist on the weapon. The sprint-start check in Unity 6000.3.11f1 reduced the maximum support wrist step from 9.5 cm to 2.4 cm for the shotgun and hunting rifle. The Play Mode review covered unarmed movement and all six equipped weapons, including high and low aim, fire, and both reload entry states.

The idle clip adds a small forearm bend.

## Main player rig

The regular player spawns from Male43_Player.prefab through CharacterPlayer, CharacterData, and Chapter1PlayerStart. The nested model uses the imported Male_43_PlayerRig.fbx Humanoid Avatar and its skinned mesh with the game's Male_43_Player material. The beanie, flashlight, and hand sockets are aligned to the FBX bind pose. The Superman variant uses the template Character.prefab as its base.

Unity Play Mode checks confirmed the edited left and right upper-arm lengths, the matching FBX body, and all six weapons through aim, firing, and relaxed and aimed reloads. PlayerRig_Review loads the same edited rig and supports pose freeze, frame stepping, and the rig-only view.

## Sprint to pivot

PlayerTurnAnimation blends the last moving hip and leg pose into the stationary pose over 0.22 seconds. Turn input and speed remain owned by PlayerMovement. In the unarmed A and D sprint-stop tests at 60 fps, the largest first-frame ankle displacement dropped from 59 cm to 1.1 cm.
