# Magnum revolver and flare update

The long-barrel Colt Python is the largest gun in the supplied PSXRevolverPack[FIXED]. The held model excludes the loose bullet and spare cylinder. The original in-place cylinder remains. Normalized length: 0.334 m.

Run Blender with `--background --python ArtSource/Weapons/build_revolver.py` to rebuild the two models and their icons. Unity menu: Horror Game / Weapons / Build Revolver. This adds the new items and pickups without rebuilding the existing guns. Runtime prefabs and textures live under Assets/Game/Weapons.

Revolver: six rounds, fires immediately on a trigger press, 1.1-second attack duration, 3-second reload, 4 body / 6 weakpoint damage, 22 m range. It uses the template pistol's animation and dedicated revolver audio. Ammo pickup defaults to three rounds; change PickupItem.Entry.Count for other quantities.

The magnum pickup UI uses RevolverAmmo_PickupIcon.png, rendered from the fixed box with opaque cardboard sides. Rebuild this image with Blender using ArtSource/Weapons/render_magnum_pickup_icon.py. The examination model is scaled to match the other ammo previews. RevolverPresentationValidation verifies a one-frame trigger press fires exactly once and the actual pickup screen displays the new icon.

Flare: visible 11 m/s projectile, gravity 9.81 m/s², bright red halo/light, wider trail, 12-second lifetime. Sticks to the first obstruction or eligible enemy. By default burns an enemy's remaining health over six seconds. SignalFlare.EnableDamage and BurnTargetCompletely are configurable on Effects/SignalFlare.prefab. Turning off BurnTargetCompletely uses BurnDamagePerSecond. No chamber-plus-one, shotgun knockdown, or progression system was added.

The built-in imagegen tool produced the three texture masters in this folder. Prompts are saved in prompts.json. Blender downsizes them for Unity and renders icons from the actual meshes. Source downloaded assets are unchanged.
