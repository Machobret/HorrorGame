# Authoring a wall kit

## Window/door families and bulk selection

A WallFamily explicitly lists its supported OpeningFamilies. Create one with Assets > Create > Environment > Window or Door Family, set Window/Doorway, and add model sets with a name, Clean/Broken condition and their WallOpeningInsert models. One atlas is one grouped model set; its pictures are variants. A family may contain several sets and may be supported by multiple wall families. The actual opening module must also exist at the chosen width/shape and match the model's aperture, mounting datum and thickness limits. Listing a family never creates geometry or bypasses fitting.

Waterpixels Windows and Waterpixels Short Windows contain clean and broken atlas sets for their respective fixed apertures, with eight pictures per condition. Random/condition changes therefore do not switch opening size. Waterpixels Interior Doors and Waterpixels Exterior Doors contain their respective door sets. Both complete sample wall families explicitly support them. New Houses reach these models through shared family assets; they do not copy atlas pictures into every level's custom catalog.

Choose Door/Window on the toolbar, then use the Selection panel for insert family, condition and rendered model thumbnails. The hovered wall supplies its wall family; only compatible families are offered. Width remains the toolbar's rigid 1/2 m choice. Random chooses a concrete variant for preview/placement, then stores it. Rebuild and reopen never reroll placed models.

Select existing openings to edit the same choices afterward. Multiple selections retain common controls, show Mixed values, and offer only choices compatible with every target. Model changes and explicit Randomize selected models publish one Undo action for the whole group. Invalid edits reject atomically. Unsupported widths are omitted; physical clearance is checked when applying a width change. Existing off-catalog models remain current/custom choices rather than being cleared by catalog edits.

## Exterior trim inheritance

The Defaults panel has Exterior trim defaults at House and level scopes. Choose a trim family/profile or custom mesh, material, height, projection and base offset. House settings apply to eligible exterior runs, including newly drawn rooms. Levels can inherit House, override, or choose None. The Trim tool edits local attachments in bulk; None for selected walls disables inherited trims, and Reset selected trims restores inheritance.

Inheritance is resolved during interpretation, without saved copies on every wall. Local settings survive default changes. Eligibility, opening clipping, corner fitting, paint retention and native output Undo use the existing owners. Material inheritance is independent of profile inheritance. See MATERIAL_LIBRARY.md for shared finishes.

## Automatic interior/exterior geometry

Each level's kit has **Interior family** and **Exterior family** defaults. Automatic walls use current room ownership to choose one: enclosed indoor partitions use the interior family; facades, indoor/outdoor separators and exposed runs use the exterior family. Leaving Exterior family empty deliberately uses the interior/base family for both roles. Wall type can explicitly select a role; a selected wall's **Wall family** overrides either default, and **Reset** returns it to automatic routing.

Closing an adjoining room or changing Indoor/Outdoor use therefore changes the complete module model, its thickness and matching opening pieces, not just its finish. Material inheritance remains independent; using exterior geometry on an interior partition does not assign an exterior finish. Opening identities/widths/styles are retained, with existing interface/fit validation rejecting incompatible changes rather than silently replacing models.

New Houses and the main samples use Wall_01 inside and Wall_02 outside. The modular reference keeps one base family plus a deliberate wall override to demonstrate mixing. Known generated family pins in shipped samples were removed; arbitrary user overrides remain untouched. For an already-open prototype copied before this correction, choose its Exterior family and use **Select all walls -> Wall family -> Reset** if those old sample assignments should become automatic. This is an explicit Undoable edit; there is no live-data migration or geometry inferred from material names.

## Complete wall families

Author each solid, window-opening or door-opening wall as one closed complete module. Both faces and all closure/reveal faces belong to that module. Door/window frames and leaves belong to the interchangeable model. No independent-side geometry selection or runtime splitting is required. Materials remain independently assignable on the two faces.

For solid Boolean cuts, author one watertight exterior boundary: no buried centre faces between material halves, no contact faces between adjoining aperture-surround boxes, and no buried faces behind an attached ledge. Material slots label parts of that boundary; they are not separate closed solids. Duplicated vertices for hard normals and UV/material seams are fine. The supplied eighteen complete-family FBXs follow this convention. Open or non-manifold geometry cannot provide reliable solid cut surfaces.

- One Body mesh per FBX, applied transforms, X=0..width, Y=0..height, centred about Z=0. Two material slots: **Outside** (slot 0, +Z) and **Inside** (slot 1, -Z), including their closure/reveal halves. These are authoring names, not a forced exterior finish on an interior wall; the generator resolves actual room ownership. Provide normals and UV0. Keep connection thickness distinct from decorative projections.
- Names: `Wall_01_Solid_01_W2000_H2800_T150.fbx`, `Wall_02_Window_02_W1000_H2800_T300.fbx`, `Insert_Window_01_W1000_H2800_Model01.fbx`. Dimensions are millimetres. Tokens are alphanumeric; Solid has no aperture. T is full thickness, with half on either side of the datum. Thickness presets: 100/150/200/225/250/300/400/500/600 mm.
- Solid widths: 500/1000/2000 mm. Rigid opening segments: 1000/2000 mm. Family modules share height and connection thickness; each type/width/shape combination is unique. Solids use existing shared-mesh transform scaling; only a residual span below 0.5 m crops its selected source.
- Opening modules and shared models carry `OpeningMin` and `OpeningMax` transforms on Z=0. They describe the clear rectangular aperture in X/Y. The mounting datum is Z=0. A model is compatible by kind, shape, aperture size, lower height and aperture centre relative to the segment centre, optionally constrained by minimum/maximum wall thickness. Family identity is not a compatibility constraint. A 1 m door can have a 0.9 m opening, and the same model can fit a centred aperture in a 2 m segment.
- A shared model's filename W/H describe its source coordinate basis, not its leaf dimensions. Include its frame. Optional minimum/maximum wall thickness metadata can reject frames unsuitable for a deep reveal; maximum zero means unbounded. Model material is independent of wall finishes.
- Author full-height V (0..1) and a two-dimensional unwrap on every surface, including horizontal tops/bottoms, ends and aperture reveals. Wall panels use their full segment height; standalone opening models use their own bounds. A constant-height cap needs planar X/depth UVs, not constant V. Tileable-material UV islands may overlap; lightmap UVs are a separate concern.
- Projected wall materials use physical U along the wall and physical V along world height, with independent repeat dimensions and full texture sampling. Use normal tiling (1,1) as the starting point. There is no atlas band or V x4 compensation. Authored UV mode and source caps/reveals retain native coordinates for baked details. Genuine material overrides remain independent of geometry.

All 26 supplied FBXs follow the full-height convention. Regression checks cover every non-degenerate source triangle and actual House/slab height fitting, including the former window-versus-solid row mismatch. See [material setup and verification](HORIZONTAL_MAPPING_PROTOTYPE.md).

Select a source folder and use **Assets > Environment > Import Wall Family**. A folder can contain multiple complete families and shared models. The importer validates all staged objects before publishing definitions, families and models, allocates persistent identities before copying references, and updates existing assets without changing GUIDs. Chosen primary blanks, retained custom definitions and model materials survive reimport. `Preserve Import Overrides` protects hand-edited definition metadata. Missing source files do not automatically delete saved catalog entries.

Import is additive: **Undo restores changes to existing asset objects; newly created files remain**. Remove unwanted new files explicitly in Unity's Project window.

Select a wall for its family and face finishes. Select one or more openings for compatible families/models as described above. A level custom-model catalog remains under Advanced / asset setup for off-family inserts. Explicit width changes validate neighboring openings and publish atomically. Room finish convenience applies a material to facing walls once; subsequent overrides remain independent.

The reference scene contains **Complete wall families**: two families, eighteen wall modules, five shared framed opening models and an assembled example using one model across both families. **Use complete sample families** assigns Wall_01 inside, Wall_02 outside and the shared model catalog through the normal serialized Undo path; it preserves per-wall families and authored wall finishes. Source generation lives in `Tools/WallKitSource/ExportWallFamilies.py`.

The individual FBX templates are in `Assets/EnvironmentAuthoring/Content/WallKits/CompleteFamilies/Sources`. The refreshed reference scene groups each family and the shared models separately, labels dimensions/types, and records the Outside/Inside convention. It no longer displays legacy open bodies, separate caps or obsolete filler examples. Obsolete combined-source assets, exporters and their internal fitting paths are removed; there is one supported source convention.

## Waterpixels plane models

`Assets/EnvironmentAuthoring/Content/WallKits/Waterpixels` contains 16 door pictures and 16 window pictures, with both existing window aperture sizes (48 model choices). These are rigid, two-sided opaque planes fitted to the current sample apertures; wall meshes need no adjustment. New Houses include them. On an existing level, select an opening and click **Add Waterpixels door/window models**, then choose **Model**. The same button is available in the level defaults. Catalog additions and placed choices use the existing Undo commands; custom entries remain. Standalone prefabs and original source copies are included. **Assets > Environment > Rebuild Waterpixels opening models** reproduces the assets without replacing their GUIDs or material overrides. The local README records hashes, atlas order and fitting. Photographed glass/damage stays opaque; the models add no interaction or collision. The unpadded 256px atlases use point filtering without mipmaps to prevent cell bleeding, with possible aliasing at distance.

## Trim families and modeling templates

Open **Tools > Environment > Open Modular Reference Scene** for the Independent/Townhouse trim rows and applied building example. The reusable catalogs are `Assets/EnvironmentAuthoring/Content/WallKits/TrimFamilies/Independent.asset` and `Townhouse.asset`; each offers multiple profiles and shares the bevel source. They are browsing collections, independent of wall-family compatibility. Choose **Trim family**, then **Profile** in the Trim tool, or assign a **Trim mesh** directly. Existing bulk selection, dimensions, finish overrides and Undo remain available. Changing the browser family alone never replaces placed trims.

The FBX templates are in `Assets/EnvironmentAuthoring/Content/WallKits/TrimFamilies/Sources`:

- Plain band: 1 m repeat, 0.20 m height, 0.15 m projection.
- Beveled band: the same dimensions, with sloped upper/lower edges.
- Townhouse cornice: 1 m repeat, 0.30 m height, 0.20 m projection, with a stepped profile.

Each source is one closed mesh, including end caps, with one **Trim** material slot, normals and UV0. Use +X for repeat length, +Y up, +Z outward; start at X/Y/Z=0. Author full-height V (0..1), with planar UVs on caps and ends. Materials are assigned independently; the supplied trim material uses Triplanar, while Authored UV remains available for baked details. Template filenames include family/style and millimetre W/H/D dimensions for clarity; no new trim filename parser is required. Import a custom FBX normally, then create **Assets > Create > Environment > Trim Family** and populate its mesh list. A building-specific catalog and an Independent catalog use the same format.

The material/dimensions on each attachment remain explicit; catalogs do not silently supply defaults or lock a profile to a building. The placed back reaches the logical wall centerline; **Projection** measures the portion beyond the exterior face. Total mesh depth includes that exterior wall depth, using shared-mesh transform scaling, so detailed profiles also scale in depth. Corner fitting and hover bounds use this same range. Tapered or mismatched profiles can show visible intersections; these samples do not add miter cuts or a transition resolver. The reference uses zero base offset to keep its bands visible above the display floor; a negative offset places a band below the wall base and continues under door apertures when it stays below their sill.

`Tools/WallKitSource/ExportTrimTemplates.py` reproduces the source templates and refuses existing FBXs unless `--overwrite` is explicit. `CombineTemplates.py` rebuilds the single combined modeling FBX under ignored `Artifacts/DemoExports`, including all 26 wall/model/trim meshes and checking them by reimport.

## Fitting, materials and output ownership

Wall lines are fixed logical datums. The family supplies closed geometry and declared full connection thickness; decorative projections do not enlarge that nominal connection footprint. Effective Interior/Exterior classification chooses the corresponding kit default unless the wall has an explicit family override. Finish overrides never choose geometry. Outdoor classification can also change slab coverage above a wall and therefore its fitted top.

A complete module remains one body renderer with two material slots. The room on each side determines its effective finish: House defaults, then level, room and explicit face overrides. An interior partition never falls back to an exterior finish. These materials are independent of mesh identity. The optional shared opening model remains a separate renderer with its own material.

Solids at least 0.5 m long use shared authored meshes and X transform scaling. Short residual spans crop the smallest suitable source without compressing its authored UVs. Equivalent fitted results share geometry. Rigid door/window segments keep their authored width and aperture. Vertical wall bands and head fitting account for the level's uniform height and slab coverage; height variants do not imply a separate source family. Pure finish changes update material bindings without regenerating unchanged geometry.

Unknown replacement meshes may contain paint. When a rebuild cannot prove their binding, it keeps one hidden output and reports it instead of deleting the mesh or accumulating recovery versions. A missing definition can also remove proof of an otherwise native source binding. Explicit logical removal deletes the retained generated objects; persistent mesh assets remain under AssetDatabase ownership.

## Validation and inspection

The definition Inspector and builder's **Validate kit** action check finite positive dimensions, assigned mesh data, triangle topology, normals, UV0 and slot counts. Family checks enforce consistent family identity, height and declared connection thickness; solid/opening width roles and aperture keys must be valid and unique. Import adds filename, thickness-preset and aperture-marker checks before publishing assets.

These checks do not certify watertight topology, every triangle's winding, the exact visible aperture, decorative collision clearance, UV quality or actual GPU batching. Inspect a representative family in the reference scene and the assembled example, including Boolean cuts through its sides and closure faces. Material equality alone does not make different fitted geometry instance together.

## Reading the code

Paths below are relative to `Packages/com.manta.environment-authoring`:

- `Runtime/WallFamily.cs`, `WallModuleDefinition.cs`, `WallOpeningInsert.cs`: source metadata and compatibility.
- `Editor/WallFamilyImporter.cs`: staged import and persistent asset publication.
- `Runtime/HouseWallLayout.cs`: chosen source modules, opening placement and junction interpretation.
- `Editor/WallBlankLengths.cs`, `WallLengthFit.cs`, `WallHeightClip.cs`: shared length fitting, UV-preserving cropping and vertical fitting.
- `Editor/HouseWallOutputBuilder.cs`: generated bindings; `HouseFinishResolution.cs` owns effective finishes.
- `Editor/GeneratedMeshResources.cs`: derived mesh sharing, provenance and disposal.
- `Tests/Editor/CompleteWallFamilyTests.cs`, `WallKitContractTests.cs`, `WallProfileFittingTests.cs`: import/workflow, malformed-source, fitting and retention coverage.

The editor uses Unity's existing read-only mesh snapshot API; importing a family does not require enabling runtime Read/Write or installing a processing package.

Trim material sampling: the supplied M_Plaster_SampleWall_01 material uses Triplanar so top/end density follows the same physical repeat sizes as the other faces. Shared construction materials use Triplanar on trims too; choose another mode explicitly only for an authored mapping requirement. Authored UV remains available for baked decoration. This is a material choice; catalog membership and mesh assignment do not force it.

## Shared construction finishes and fence samples

Construction materials are independent of wall families and can also finish trim/slab/fence/path geometry. Assets/EnvironmentAuthoring/Content/Materials contains the sorted library; Assets/EnvironmentAuthoring/Content/ConstructionMaterials/Imported retains source textures and the manifest. See MATERIAL_LIBRARY.md. Original baked fence finishes remain ordinary UV materials. Preserve geometry proportions when using baked UVs: rigid openings are not stretched and House residuals below 0.5 m crop through WallLengthFit. Fence panels opt into native repeat/crop via TownModule.AllowStretch=false; they are not House opening families. See TOWN_TOOLKIT.md for the separate FenceFamily catalog convention and HORIZONTAL_MAPPING_PROTOTYPE.md for mapping costs/caveats. The existing modular reference scene includes both source pieces and generated specimens; no modular road/sidewalk mesh source was found in the recent batch.

## Framed glass window presets

A window preset is a separate Wall Opening Insert asset in a supported Window or Door Family. Export one mesh with submesh 0 for the opaque frame and optional submesh 1 for glass. Assign Material and Glass Material respectively; use a transparent shader such as URP Lit with Surface Type Transparent for glass. A blank Glass Material reuses Material on the second slot, retaining opaque presets. The opaque construction shader is for opaque surfaces. Single-submesh legacy inserts still use Material alone. Keep the existing metre-space coordinates, 1m/2m segment and OpeningMin/OpeningMax convention. The existing wall-family importer reads both material slots; import never replaces an artist's assigned finishes. Glass is visual; aperture collision is unchanged.

Assets/EnvironmentAuthoring/Content/WallKits/FramedWindows contains clear/divided window examples for both sample opening shapes. Both wall families list Framed Glass Windows. Select window segments in House edit mode to choose the family and previewed preset; multiple selection applies the same edit atomically with Undo. The modular reference scene has separate model rows and an editable house example. Assets > Environment > Create framed window examples creates missing teaching assets without resetting existing ones.

## Scripted door prefabs

Keep the existing Door Family and Wall Opening Insert presets. Assign a project prefab to the preset's Prefab field to replace its mesh visual. Mesh/material fields remain available for ordinary visual-only presets. Door prefabs retain the same 1m/2m segment and aperture compatibility metadata. Author the prefab root at doorway centre/floor height, unit scale, with +Z toward the module front. Put scripts, visual models, colliders and both prewired exit transforms in the project's base prefab; use prefab variants for ordinary visual/behaviour choices. The generator does not know about or attach gameplay scripts.

After placement, leave House edit mode to select the door object and edit ordinary prefab overrides in Unity. Its parent attachment follows wall/opening movement; edit prefab-local offsets, not the attachment. Rebuilding walls retains the same prefab instance and all its overrides. Choosing another preset is an explicit Undoable replacement, using that preset's defaults. Native Delete removes the door object but leaves the logical opening and does not respawn it during rebuild; undo deletion or choose another preset to restore it. Removing the opening removes its attached object through the same source command's Undo group. The game-specific base prefab is deferred until integration.


## Named material regions on glazed wall bodies

Wall families can declare two `MaterialRoles`, each with a stable ID, display name, mesh slot (0/1) and default material. The Storefront T150 family uses `frame`/Frame at slot 0 and `glass`/Glass at slot 1. House Properties displays these names in place of side finishes, with per-wall overrides and Reset to family inheritance. Family defaults are shared asset edits with Undo. The level Defaults view shows the roles of its active families; mixed wall selections keep each family's controls separate. Ordinary families leave MaterialRoles empty and retain House/level/room/side finish inheritance.

Named roles always follow mesh regions, without direction-based swapping. Corner covers use slot 0. Finish-only signatures track family defaults and per-wall overrides separately from geometry, retaining generated meshes. Wall copy, split, serialization and Undo preserve overrides; dissolve rejects different role assignments. An override remains dormant if its wall changes to a family without that role. Two-slot mesh inserts may enable `UseWallMaterialRoles` to follow the same host assignments; other inserts retain their own materials. This is a project integration extension to the bundled toolkit; retain it when updating the toolkit.

Legacy definitions may still assign two fixed `BodyMaterials`; both must be non-null. These apply when the host family has no named roles. Prefer family roles for new glazed content. Storefront imports migrate their previous palette to family defaults and clear duplicate definition palettes without resetting artist-edited defaults.


Framed wall definitions can disable `CoverSlabEdge` to stop rendered modules and generated corner covers at the nominal ceiling height. Ordinary opaque definitions keep this flag enabled for the established facade coverage over the slab edge. Storefront T150 imports set it false, so a 2.8 m wall is not stretched to 2.995 m over a 0.2 m slab. The flag participates in geometry signatures, allowing existing generated output to refresh on the next normal rebuild.
