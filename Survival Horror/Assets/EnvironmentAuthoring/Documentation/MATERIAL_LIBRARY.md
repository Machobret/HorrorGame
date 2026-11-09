# Material library

Materials live under Assets/EnvironmentAuthoring/Content/Materials. Files and main-object names use **M_Surface_Description_Variant**, for example M_Brick_Red_01. Surface describes the finish, not whether it is assigned to a wall, trim or floor. Mapping and repeat dimensions remain material properties.

- Surfaces/<Surface>: 29 curated finishes, including two path-UV paving alternatives. Use a path-UV finish when paving should follow road/sidewalk direction.
- Candidates/<Surface>: six photo-repeat candidates retained for evaluation. Directional artwork and visible joins prevent treating them as unqualified defaults. Existing references remain.
- ModelSpecific/Fences: 13 baked finishes, including alpha chain-link. These depend on authored model UVs and are not general repeat surfaces.
- ModelSpecific/Openings: four door/window atlas materials. Choose pictures through the insert-family selector rather than applying the whole atlas to a wall.
- Examples: sample, flat-colour, mapping-comparison and diagnostic finishes, excluded from the curated picker.

The small **…** button beside material fields opens Unity's native material browser filtered to the Construction label, with material previews. Search M_Brick, M_Wood, etc. The object field still accepts custom/baked materials. Opening the browser alone never creates an override. Reset restores inheritance.

All 69 material assets retained their GUIDs during organization. No working scene was regenerated or saved; no materials were deleted or merged by name. Shared textures do not imply identical materials: mapping, repeat dimensions and shader settings can intentionally differ. All 80 fence FBXs now remap their 105 imported material occurrences to the 13 shared finishes instead of exposing per-model copies.

Tools/ConstructionAssets/MaterialNames.json records explicit source-recipe naming and curation. ConstructionSources.json and FenceSources.json carry current native destinations. Exporters use the naming table when regenerating manifests; Unity importers do not search legacy paths. Explicit source reimport retains its existing contract of restoring generated defaults; duplicate a finish before making independent artist-owned variations. The one-time organization preserved existing shader settings.

This selection is a starting library, not a promise of seamless patterns on every angle. World triplanar mapping can blend directional details on diagonal faces. Authored UV and path mapping remain available.

As of 2026-11-09, shared construction/sample/path finishes default to Triplanar for reuse on walls, slabs and trims. Retained SampleUV/SampleBox/PathUV filenames preserve references and do not select mapping. Baked ModelSpecific materials remain unchanged. Repeat dimensions and scale are shared material properties; duplicate a finish for independent tuning. Triplanar is world aligned, so paving no longer follows a winding path unless an explicit Authored UV material is selected.
