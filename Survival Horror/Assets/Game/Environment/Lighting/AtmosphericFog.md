# Atmospheric fog

Drag AtmosphericFog.prefab into the active scene, or choose GameObject > Horror Game > Atmospheric Fog. Use one fog controller per active scene. Position does not matter: this is scene-wide distance fog, not a local volume.

The prefab is a real scene GameObject: enabling it injects fog immediately in both editor and gameplay. Disable or delete the GameObject to restore the previous scene fog immediately. No preview mode or bake is required. The Inspector also has Show Fog / Remove Added Fog buttons. Scene View's fog/effects display must be enabled to see fog there; Game cameras receive the actual scene fog.

Defaults give close, dense gray-green, Silent Hill-inspired distance haze at density 0.08. Density 0.02 is lighter, 0.045 is moderate, and 0.12 is very dense. Adjust Fog Color to suit the scene. Fog distance is measured from each rendering camera's view depth, not from the character or the fog object's position. Switching fixed cameras automatically changes the viewpoint from which fog is evaluated. This uses Unity fog-compatible shaders, not volumetric smoke; custom shaders which ignore Unity fog will need shader support.

Fog Open Sky is enabled by default: Game and Scene View cameras render empty background/sky in the same color as distant fog. This surrounds the view even where no geometry exists. While fog is active, the sky is fully obscured (it is treated as infinitely far away); geometry still has distance-based falloff. Disable Fog Open Sky to retain the ordinary sky. Camera backgrounds are overridden only for rendering and then restored. Skybox assets, ambient lighting, reflection cameras and depth-only camera overlays are unchanged. At zero density or when the fog is disabled, the original sky returns.

Use GameObject.SetActive(true/false) to inject/remove the object instantly during gameplay. ToggleFog() and SetFogEnabled(bool) also switch instantly. Optional FadeIn()/FadeOut() use Fade Seconds (scaled game time). Disabling/deleting the component always restores immediately.

Removing the added fog restores the base scene settings, which may already include a light haze. MansionLightingController and HorrorLightingPresetApplier update that base while the overlay is active, so a blackout does not erase the fog and removing it restores the latest mood. No tutorial timing or scene assets are changed automatically.

The component operates on the active scene's RenderSettings. For additive scenes, place it in the scene designated active; do not expect separate fog per room or camera.

MapRenderer cameras and cameras rendering only UI/MapSystem layers are excluded. Their original background is retained and surface fog is suspended for their render, then restored for world cameras. The darkness/material adaptation manager also excludes map meshes so player markers keep their unlit materials.
