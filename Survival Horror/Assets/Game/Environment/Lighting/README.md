# Horror lighting library

This folder contains an authored Unity 6 / URP lighting library for a survival-horror tone: very low ambient fill, cool moon/mercury contrast, dense localized fog, and small warm practicals that lead the player through darkness.

## Create and bake

In Unity, select **Horror Game > Lighting > Create and Bake Horror Lighting Library**. This generates (or preserves) the following content beneath this folder:

- `Scenes/Evening` — dusk street with amber practicals
- `Scenes/Night` — moonlit alley and emergency-light accents
- `Scenes/Fog` — close-range navigation in dense cold fog
- `Scenes/DestroyedZone` — collapsed street, debris and firelight
- `Scenes/AbandonedZone` — decayed interior with chandelier, lamps, and lantern
- `Light Sources` — Fire, Street Lamp, Lantern, Flashlight, Lighter, Table Lamp, Chandelier, Candle Holder, Emergency Red, Flicker Tube, and Moon Window
- `Presets` — editable ambient, fog, and key-light mood assets
- `Skyboxes` — procedural dusk, moonlit, foggy, smoky, and sickly-overcast skyboxes

The generated geometry is intentionally simple and marked **Contribute GI** so each scene stays fast to bake and can be replaced with production environment art. The bake command opens each scene, uses Unity Progressive Lightmapper, and saves the resulting LightingData asset beside its scene.

Use **Horror Game > Lighting > Update Skyboxes and Sun Lights** after pulling changes to update existing generated scenes. It assigns a scene-specific procedural skybox and a baked directional dusk sun, moon, clouded moon, smoke sun, or overcast key light, then restores the scenes you had open. Run **Bake All Lighting Test Scenes** afterwards to refresh indirect lighting.

## Use in gameplay scenes

Drag a prefab from `Light Sources` into a scene. Keep character-held lights (flashlight and lighter) dynamic/mixed; retain the environment, static lamps, and fire bounce lighting as baked/mixed. `HorrorLightSource` adds a subtle, optional Perlin flicker without animator overhead. Use `HorrorLightingPresetApplier` to apply a matching mood at runtime.

The Village desk lamps and House floor and table lamps use atlas masks for their fabric shades and bulbs. A directly placed prefab reads the light on the prefab, and a House Toolkit placement uses the fixture preset. Regenerate the masks with `ArtSource/Environment/LampShades/BuildEmissionMasks.ps1` after changes to their source atlas regions.

Find **Floodlight** under **Fixtures & utilities** in the Asset Library or under **Fixture** in the Environment Tools. It sits on its original stand and uses an adjustable spotlight aimed through the diffuser. The source FBX is preserved in `ArtSource/Environment/Floodlight`. The imported model remaps the original **Yellow** and **Emission** slots to existing project materials.

The Chandelier prefab includes `HorrorChandelierSwing`, which provides a subtle configurable pendulum sway during play. Use **Horror Game > Lighting > Create or Refresh Light Source Prefabs** to add the Candle Holder and update existing Chandeliers without opening any scene.
