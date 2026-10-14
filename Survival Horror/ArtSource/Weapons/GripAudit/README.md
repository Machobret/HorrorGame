# Weapon grip review

Captured all five weapons on the actual player in idle and aiming poses, from the side and front. After captures require the actual PlayerStateAiming state. Dedicated lights and a close camera expose the hand/model contact clearly; these inspection cameras do not change game assets.

FlareGun: moved 3.5 cm down and 1.5 cm back in the visual model's axes, seating the larger curved grip inside the palm.
Revolver: moved 2.5 cm down and 0.5 cm back to close the smaller grip gap.
1911, double-barrel shotgun, and hunting rifle: inspected without changing their attachment offsets. Idle long-gun poses use the existing carry animation; the support hand is more engaged in the aiming pose. The character uses the existing blocky hand animation, so this adjustment does not add individual trigger-finger articulation.

Only the two item-data CharacterAttachment.Position fields changed. WeaponAssetBuilder preserves those exact calibrated offsets. Mesh scale, rotation, pickup presentation, IDs and weapon timing remain unchanged. Muzzle sockets follow the equipped weapon automatically.

The saved WeaponGripAudit.cs harness runs in an isolated project when placed under an Editor folder. Before/Gun_Aim images were omitted because the first preliminary render was captured before the aiming transition completed. All After images have verified aiming states.
