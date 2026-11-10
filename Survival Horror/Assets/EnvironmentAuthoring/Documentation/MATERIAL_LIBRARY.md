# Material library

Materials live under Assets/EnvironmentAuthoring/Content/Materials. Files and main-object names use **M_Surface_Description_Variant**, for example M_Brick_Red_01. Surface describes the finish, not whether it is assigned to a wall, trim or floor. Mapping and repeat dimensions remain material properties.

- Surfaces/<Surface>: 29 curated finishes, including two path-UV paving alternatives. Use a path-UV finish when paving should follow road/sidewalk direction.
- Candidates/<Surface>: six photo-repeat candidates retained for evaluation. Directional artwork and visible joins prevent treating them as unqualified defaults. Existing references remain.
- ModelSpecific/Fences: 13 baked finishes, including alpha chain-link. These depend on authored model UVs and are not general repeat surfaces.
- ModelSpecific/Openings: four door/window atlas materials. Choose pictures through the insert-family selector rather than applying the whole atlas to a wall.
- Examples: four shared textured finishes (brick, plaster, wood and planks), excluded from the curated picker. Walls and trims share the same plaster and brick materials; there are no separate mapping or flat-colour examples.

The small **…** button beside material fields opens Unity's native material browser filtered to the Construction label, with material previews. Search M_Brick, M_Wood, etc. The object field still accepts custom/baked materials. Opening the browser alone never creates an override. Reset restores inheritance.

The initial organization retained all 69 material GUIDs. The 2026-11-10 example consolidation removed 13 redundant materials, leaving 56 library materials. The four retained examples keep their original GUIDs and filenames: M_Brick_SampleWall_01, M_Plaster_SampleWall_01, M_Wood_SampleUV_01 and M_Wood_PlanksSampleUV_01. Duplicate/flat plaster references now use the shared plaster; flat wood uses the shared wood. Concrete and placeholder grass/ground swatches use the existing Surfaces/Concrete/M_Concrete_Grey_01 finish. This intentionally replaces the demo colours with textured surfaces. Reference scenes and the town fence catalog were updated without regenerating geometry. Sample builders reuse these finishes and no longer create mapping copies or fallback colour swatches. All 80 fence FBXs remap their 105 imported material occurrences to the 13 shared baked finishes instead of exposing per-model copies.

Each textured example assigns its existing matching Unity Normal Map texture, with Use standard normal map enabled and Normal strength = 1. The shader decodes Unity's platform-specific normal format before applying the same UV/wall/box/triplanar normal frame as packed surfaces. Packed surface maps remain linear data and work as before when the standard-normal option is off. No extra normal textures were generated. Construction Material creation preserves supported normal inputs when copying a selected material.

Tools/ConstructionAssets/MaterialNames.json records explicit source-recipe naming and curation. ConstructionSources.json and FenceSources.json carry current native destinations. Exporters use the naming table when regenerating manifests; Unity importers do not search legacy paths. Explicit source reimport retains its existing contract of restoring generated defaults; duplicate a finish before making independent artist-owned variations. The one-time organization preserved existing shader settings.

This selection is a starting library, not a promise of seamless patterns on every angle. World triplanar mapping can blend directional details on diagonal faces. Authored UV and path mapping remain available.

As of 2026-11-09, shared construction/sample/path finishes default to Triplanar for reuse on walls, slabs and trims. Retained SampleUV/PathUV filenames preserve references and do not select mapping. Baked ModelSpecific materials remain unchanged. Repeat dimensions and scale are shared material properties; duplicate a finish for independent tuning. Triplanar is world aligned, so paving no longer follows a winding path unless an explicit Authored UV material is selected.
