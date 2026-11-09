# Environment Authoring 0.1.0

Import this single .unitypackage into an existing Unity 6000.3 URP project.
Verified with Unity 6000.3.11f1, URP 17.3.0 and Collections 2.6.2. Install these
dependencies in Package Manager first. Do not also install the embedded/UPM
version: it has the same assemblies and GUIDs.

1. Run Tools > Environment > Use bundled defaults (explicitly replaces only
   this toolkit's creation defaults). Or configure Project Settings > Environment Authoring.
2. Open your level. The Environment Tools Scene palette creates House, Wall,
   Road and Sidewalk objects. Select a House and enter Edit House. Done exits.
3. Save normally. Generated geometry and collision are saved in the scene.

Content includes complete wall families, compatible inserts, trim templates,
fence catalogs and shared construction/path materials. Reference/WallModules/
WallModuleDemo.unity is the modular authoring reference; FBX templates are in
Content/WallKits/CompleteFamilies/Sources and Content/WallKits/TrimFamilies/Sources.
Reference scenes are optional; importing never opens or replaces your level.
See Documentation/LEVEL_EDITING.md and WALL_KIT_AUTHORING.md.

Keep Houses upright at unit scale; translation and Y rotation are supported.
Unpack prefab instances before editing their layout. Dense edits can still pause
on release (verified medians around 80–95 ms, with occasional longer pauses).
This prototype has been regression tested, not certified free of all defects.

Updates: reimport into the same folder, retaining GUIDs. Keep custom models and
materials outside EnvironmentAuthoring to avoid replacing bundled originals.
This archive contains no ProjectSettings, render-pipeline configuration, game
controller, test suite or download/import recipes. Existing project rendering
settings and creation defaults are not changed on import.

Bundled third-party geometry-library license files remain beside their sources.
Source/attribution manifests accompany bundled art where supplied; this archive
is for the owner's project use and is not a new redistribution license for art.
