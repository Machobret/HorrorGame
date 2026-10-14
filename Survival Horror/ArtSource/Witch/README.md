# Witch art revision

The updated model and baked diffuse texture are in `Assets/Game/Enemies/Resources/Witch`. Existing instances of `Monster_Witch.prefab` load them automatically.

- One joined skin surface with blended shoulders, hips, wrists and ankles.
- Rounded hips that flow into the thighs, without the old flat thigh caps.
- Flat soles at ground height, lower feet and joined toe shapes.
- Curved fingers with knuckles, rounded tips and connected thumbs.
- Eyes, nose, mouth and teeth painted into the skin; no separate facial parts.
- A 512x512 baked diffuse texture with bruising, veins, rib shading and blended borders, using point filtering in Unity.
- 4,200 triangles, the existing 18-bone rig and seven animation clips.

`Witch.blend` contains the editable exported model, packed texture and all seven animation actions. `build_witch.py` reconstructs this source scene and preview from the validated game FBX. The painted atlas source was generated with the built-in image-generation tool; its prompt is recorded in `TextureGenerationPrompt.txt`.

Before export, mesh verification found one connected surface, zero nonmanifold edges, zero unweighted vertices and normalized skin weights. All 21 isolated Unity checks passed on this FBX, including import, actual bone animation and existing combat behavior. Results are in `UnityValidation.txt`.

To use the enemy, place `Monster_Witch.prefab` on a baked NavMesh. Behavior and damage settings remain on Witch Controller. Play-test camera angles and final balance in the actual level.
