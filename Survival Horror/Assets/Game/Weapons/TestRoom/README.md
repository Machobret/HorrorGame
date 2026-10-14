# Flare gun test room

Open Assets/Scenes/FlareGun_TestRoom.unity in Unity and press Play. The real Horror Engine player, UI, camera and weapon logic are used. The flare gun starts equipped with one loaded flare and ten spare rounds. Extra gun/ammo pickups sit at the spawn.

- Fire normally to exercise the actual gun.
- F6 / Demo launch: launch the same projectile from the marked firing line, with damage disabled.
- F7: toggle quarter-speed slow motion.
- F8: alternate overview and side cameras.
- F9: add ten spare flare rounds; reload normally.
- HUD: counts active flares and shows whether the latest projectile is flying or landed.

The targets are at 2m, 4m and 6m; floor lines continue to 10m. A dark backdrop improves contrast. Floor tiles support the project's vertex-based additional lights, so nearby vertices can show the moving red illumination. Projectile speed, gravity, rendering and gameplay damage settings are unchanged. No hostile enemy is placed in this room.

Creation source: ArtSource/Weapons/create_flare_test_scene.py. Compilation checked against the full project's current Unity C# response file, with output directed to a temporary assembly. Scene/prefab local references, external asset GUIDs and unique file IDs were checked. The scene still needs a visual Play-mode check in the open user editor; it was not opened automatically because another scene may have unsaved edits.

Full-project isolated Play-mode validation passed on 2026-10-13: real player, initial equip and ammo, demo flight/trail/light, overview and side captures, landing, and actual equipped weapon projectile launch. Visual captures are saved in ArtSource/Weapons/FlareTestRoom. A Unity Editor search-index initialization exception was excluded by its editor-only stack trace; gameplay checks reported no errors.

Character-input regression: the original attack state consumed the flare gun's last round, then interrupted the montage before its zero-delay launch coroutine resumed. The test now injects aim/fire through the Input System and character state machine. Before the fix, ammo dropped to zero with no new projectile. After the fix, the same test consumed one round and spawned exactly one projectile. Single-shot weapons and exhausted rate/burst attacks now finish their pending shot instead of interrupting it.

Flare reload movement: ReloadableWeaponData.AllowMovementDuringReload is enabled for the flare gun. PlayerStateReload enables walking/turning while the animation and 1.8-second timer run, then hands movement control back to the next state. Full input validation walked 0.85m during reload with the chamber still empty, then verified one round loaded, one reserve consumed, and movement enabled after completion. Other weapon data retain the default stationary reload.

Mobile reload animation now uses a separate upper-body masked layer. Reload-state blend callbacks are redirected only for mobile reloads, leaving locomotion in control of the hips, legs and feet. The original full-body weapon layer fades out normally. Reload exit clears the mobile animation and restores callback routing. Full-project input validation confirmed movement during reload, active upper-body layer, inactive full-body weapon layer, relative foot movements of 0.14m and 0.35m between samples, correct reload completion and animation cleanup.

Hunting rifle: firing uses the upper-body Mobile Reload layer's WeaponAttack state so locomotion and the walking feet remain active during its 1.4-second firing cycle. Reloading also allows movement. RifleMovementValidation checks real aim/fire and movement input, one consumed round, animated feet, and cleanup after the shot.

PSX flare presentation: the halo shader uses a 16x16 sprite grid, three color bands, and a checker-dithered edge. SignalFlare creates one persistent .36m glow sprite, uses hard point-light shadows at 8m range, and holds brightness steps at 12Hz with a four-step end fade. Projectile speed, collision, damage and lifetime are unchanged. FlareTestSceneValidation passed with the shader rendering in the test room; reference capture: ArtSource/Weapons/FlareTestRoom/Flare_PSX_Lighting.png.

Stylized surface lighting experiment: HEUniversalPipelineAsset_Renderer now contains a separate Stylized Flare Lighting full-screen feature. Retro Composite now matches origin and is enabled after the flare lighting pass. StylizedFlareLighting publishes up to eight live flares and clears its registry on destruction and play-mode initialization. The composite uses scene depth to restrict the effect to a 5.5m octagonal region, with inner orange-red (<1.15m), red (<3m), and dark-red bands; boundaries use 3px Bayer dithering. It remaps the visible red contribution of the existing point light so its shadows and texture variation remain visible. This is an artistic composite, not a new physical lighting model: red-painted surfaces inside the flare radius can also be affected. Highly emissive pixels are preserved.

Tune Strength, Dither pixel size, and the three colors in Assets/Game/Weapons/Materials/StylizedFlareLighting.mat. Strength 0 restores the ordinary flare lighting. The camera-resolution override remains disabled; the original Retro Composite dithering and its 320x180 virtual resolution are enabled. FlareLightingValidation compares a frozen landed flare with Strength 0/1, checks that surface pixels change, and verifies identical rendering with no active flares.
