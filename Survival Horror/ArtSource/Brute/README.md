# Blobber

A hunched, heavy-bellied creature with distorted human features, darkened bruised flesh, worn charcoal pants, and green acid boils.

## Files

- `Blobber.blend`: editable mesh, 17-bone deform rig, skin weights, animation takes, and preview lighting.
- `Brute_Flesh_Atlas_Source.png`: full-resolution texture generated with the built-in imagegen tool.
- `build_brute.py`: reproducible Blender generator. Reuses the flesh atlas, exports the FBX and 256 x 256 atlas, and renders pose previews. Run from the Unity project root with Blender's background Python mode. The script currently targets this project's absolute path.
- `Previews/Brute_Final_ThreeQuarter.png`: current model preview. The matching Walk and Attack previews show the hunched rig in motion. Earlier previews preserve the upright design.

Unity assets live at `Assets/Game/Enemies/Resources/Brute/`. Use the existing `Assets/Game/Enemies/Blobber.prefab` for gameplay.

## Rig and integration

The mesh has 2,586 authored vertices and 4,912 triangles. Flat normals and UV seams increase Unity's imported vertex count. Every vertex is weighted; the rig has pelvis, spine, chest, neck, head, and three bones per limb.

The FBX includes idle, standing idle, walk, attack, hurt, death, and compatibility grab takes (13 clips). Grab states remain disabled on the Brute prefab. These are initial authored animations, not motion capture.

`BruteVisualModel` creates the hunched model in the editor and at runtime. It keeps the template's hidden Animator for combat events and mirrors its state and normalized time onto an AnimatorOverrideController containing the new rig's clips. The old renderer is hidden, and the original enemy logic still controls movement, cooldowns, damage, and death. The visible model is generated and not serialized into the prefab. Edit its source FBX/material, not the generated preview child.

Pants use a separate point-filtered cloth texture and material, preserved by BruteVisualModel. The skin material is darkened to 0.58 RGB in both Blender and Unity.

The Brute retains 8 HP, its damage toggle, and acid death effect. Body/head hitboxes and the main capsule have been adjusted to the new silhouette. Scene navigation still requires a baked NavMesh.

## Skin

The latest skin is pale gray-beige with mauve bruising, blue-violet veins, and stretch marks. The atlas is imported at 256 x 256 with point filtering and no mipmaps. Acid boils, dark recesses, and teeth have dedicated atlas regions. The skin revision preserves the UV layout. The subsequent posture revision bends the mesh and rest skeleton together: a rounded upper back, lowered shoulders, and a forward head. Acid boils are placed on the final hunched mesh using surface intersections and sampled skin weights. The raised green centers extend above the skin, and all animation takes use the hunched rest rig. Head damage and sight positions have been adjusted to the new posture.

## Validation

An isolated Unity 6000.3.11f1 batch project successfully imported the FBX, mapped all controller clips, instantiated the visible hunched model, found 17 bound bones, and sampled an attack pose with approximately 100 degrees of upper-arm rotation. All original model renderers were hidden. Full project C# compilation passed. Neutral, walk, and attack poses were rendered in Blender and inspected. Gameplay in the user's open scene has not been tested.

## Arm and foot revision

The shoulder, upper arm, forearm, wrist, and palm use one connected ring mesh per side, with blended weights at the elbow and wrist. Feet have broad planar soles at ground level, bevelled edges, and low insteps. Generator assertions check sole height and outward arm/trouser normals.
