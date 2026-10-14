# Weapon shot audio and magnum carton fixes

Custom guns now use WeaponShotAudio for independent 2D shot voices routed through the original effects mixer. ShotAttack delegates to IWeaponShotAudio when present, with the original AudioSource fallback for template weapons. FlareGunAttack uses the same voice at projectile launch. Voices finish through montage interruption, reload or weapon deactivation and remove themselves after playback. WeaponAssetBuilder and WeaponPresentationBuilder preserve this setup.

The rifle's clean existing clip was reshaped with gentle saturation for more body: RMS rose from -20.2 to -13.2dB, with a -0.8dB peak and no sample clipping. boost_rifle_audio.py rebuilds from the backed-up original processed clip. Full source extraction in build_audio.py applies the same shaping when its original source arrays are available.

The magnum carton sides use an explicit opaque white texture tinted cardboard brown, zero alpha cutoff, and double-sided rendering. The lid keeps its label. Material GUIDs, mesh geometry, item IDs and pickup references are unchanged.

Validation: isolated Unity 6000.3.11f1 Play Mode verified all three shot voices remain playing through AudioSource.Stop and weapon deactivation, use 2D playback, and clean themselves up. Verified both ammo-box submeshes and materials, explicit side texture, opaque tint, zero cutoff and double-sided rendering. Prefab shot component owners and data/clip GUIDs also checked.
