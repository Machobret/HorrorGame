# Weapon grip review

Captured all five weapons on the actual player in idle and aiming poses, from the side and front. After captures require the actual PlayerStateAiming state. Dedicated lights and a close camera expose the hand/model contact clearly; these inspection cameras do not change game assets.

FlareGun: moved 3.5 cm down and 1.5 cm back in the visual model's axes, seating the larger curved grip inside the palm.
Revolver: moved 2.5 cm down and 0.5 cm back to close the smaller grip gap.
1911, double-barrel shotgun, and hunting rifle: inspected without changing their attachment offsets. Idle long-gun poses use the existing carry animation; the support hand is more engaged in the aiming pose. The character uses the existing blocky hand animation, so this adjustment does not add individual trigger-finger articulation.

Only the two item-data CharacterAttachment.Position fields changed. WeaponAssetBuilder preserves those exact calibrated offsets. Mesh scale, rotation, pickup presentation, IDs and weapon timing remain unchanged. Muzzle sockets follow the equipped weapon automatically.

The saved WeaponGripAudit.cs harness runs in an isolated project when placed under an Editor folder. Before/Gun_Aim images were omitted because the first preliminary render was captured before the aiming transition completed. All After images have verified aiming states.

## Handgun size and support-hand update

The equipped 1911 is now 15% larger (attachment scale .0115) and the flare gun is 18% smaller (.0082). The revolver retains its size. All three handgun equip prefabs use HandgunSupportPose: a short blended two-bone arm adjustment moves the support wrist underneath the firing hand while aiming/firing. The authored wrist orientation is preserved. Reloads, hurt, locomotion, and other actions use their existing animation; unequipping clears the cached skeleton. This does not add individual finger animation. WeaponAssetBuilder retains the flare scale factor and handgun support component.

Fresh Play-mode captures are in ArtSource/Weapons/Progress. Handguns_Before_After.png places the previous screenshots above the new shots, using the same camera, crop, lighting, and dithering. Weapons_And_Flare_Labeled.png also includes the unchanged long guns and flare illumination. Flare firing, collisions, reload movement, foot animation, and ammo use were rechecked with the support pose enabled.
## Revised grips after visual review

The previous zero-intersection placement was rejected: the weapons sat beside the firing palm and the support hand looked like a flat platform. Its geometric results are retained only as historical data in Rejected_Clearance_Report.json; they do not describe the current grip.

The revised 1911 keeps its 15% size increase and sits 1.5 cm lower than the original attachment. Revolver and flare gun positions/rotations return to their original palm-centred offsets; the flare gun now uses attachment scale .0074 (about 10% smaller than the reviewed .0082 version), following the requested additional size reduction. All three use the support wrist offset (0.06, 0.005, 0) in the firing-hand frame, with matching wrist orientation. The former support rotation and downward roll correction are removed. This seats both existing mitten-shaped hands around the grip. The character has no mapped finger bones, so this revision changes the wrist/arm pose without adding finger articulation.

Validation captures idle, neutral/up/down aiming and three firing samples for each handgun in the isolated Unity project. Side and front views are reviewed for visible grip contact. Source: WeaponGripAudit.cs. WeaponAssetBuilder retains the attachment and support-pose settings. Other character states keep their existing authored animation.
