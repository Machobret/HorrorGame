# Tutorial mansion lighting

Open either scene under Assets/Sandbox/LightingStudies/Scenes:

- TutorialMansion_Normal.unity: warm electrical lighting.
- TutorialMansion_Blackout.unity: power off, faint blue window light and a dim red exit light.

These are separate, matching foyer/corridor blockout scenes with a preview camera, colliders, fixtures, furniture silhouettes and dedicated mansion materials. They do not replace the existing Tutorial scene or include its gameplay.

Select "Mansion Lighting - preview via component context menu". In the MansionLightingController component's context menu choose Preview Normal Lighting or Preview Blackout Lighting. Both work before Play. Start Blacked Out determines the initial state when entering Play; set it if you want to retain a previewed state.

For a tutorial power-failure event, call MansionLightingController.Blackout(). RestorePower() restores the lights. The presets Mansion_Normal and Mansion_Blackout are reusable. To use the two-state setup in your mansion scene, add MansionLightingController, assign both presets, the moonlight, and your electrical/emergency light arrays. Applying only a preset changes the environment; the controller also switches the fixtures.

The electrical fixtures use realtime lights. No lightmaps are attached: a normal-state bake would otherwise leave baked illumination visible after power loss. Moonlight remains unchanged across states; only electrical power, emergency lighting, ambient fill and fog change. Geometry is a lighting test blockout, not a finished mansion level.
