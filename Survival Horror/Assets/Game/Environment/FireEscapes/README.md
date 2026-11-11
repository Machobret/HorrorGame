# Fire escapes

`FireEscape_Repeat.prefab` and `FireEscape_Top.prefab` are the two visual models from the supplied fire-escape archive. Drag either prefab into a scene. Their original metre scale and orientation are preserved, with ground-centred pivots and unit root scales. No colliders or gameplay behavior are added.

The source generated-box metal texture is baked into each model's albedo/UV0. Original UVs remain in UV1. Each FBX remaps its source material to the matching native URP Lit surface. The repeating model's Array modifier has source count 1; no extra sections are generated.

Original Blender/texture sources and archive hashes live in `ArtSource/Environment/FireEscapes`. The separately supplied rusty metal is available in the construction material collection as `M_Metal_Rusty_04`; the prefab surfaces retain the source appearance.
