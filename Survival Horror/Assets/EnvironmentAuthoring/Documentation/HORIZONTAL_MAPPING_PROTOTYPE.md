# Wall material projection

Current contract, 2026-11-09: one full texture per independently assigned material. The former four-strip atlas, Band control and quarter-height source UV convention are removed.

## Material setup

Choose Assets > Create > Environment Authoring > Construction Material, optionally with a base-color material selected. The creation helper copies base color, tint and tiling/offset into a new asset without rewriting its source. Assign it through the existing House/floor/room/wall-side finish controls; their inheritance and Undo commands are unchanged.

New construction materials start in Triplanar. Mapping selects Authored UV, Wall projection, Box projection or Triplanar. U repeat and V repeat set the metres covered by one image, initially 2 and 2.8. In Wall projection these are the horizontal and vertical sizes; on box-projected tops V follows depth. Base-map tiling/offset are additional shared material adjustments; start with (1,1), with no legacy V x4 compensation. M_Plaster_SampleWall_01 and M_Brick_SampleWall_01 under Assets/EnvironmentAuthoring/Content/Materials/Examples are the supplied projected wall finishes. All shared sample construction finishes now use Triplanar, including the retained SampleUV and SampleBox asset names. Names/GUIDs remain stable for existing references; mapping is the material property. Baked ModelSpecific atlases retain their existing shaders and UV sampling.

In the optional Wall projection mode, broad wall faces project U along the wall and V along absolute world height. Adjacent solid, window and door pieces therefore have the same texture density and phase after width scaling, cropping, stacked bands or opening-preserving head fitting. No UV repair is performed by WallHeightClip, and no per-instance material or texture-coordinate buffer is introduced. A consistent direction sign handles reversed fragments. Generated cover sides choose their U direction from the face normal; their UV3 contains only a cover tag. Source caps/reveals and generated cover closures keep native UV sampling. Authored UV mode retains both native coordinates for baked details.

Registration is world anchored: moving or rotating a House can change its texture phase. Changing camera position does not move the texture. Adjacent surfaces need the same material repeat/tiling settings to match. Projection does not guarantee a seamless bend around arbitrary corners, continuity of baked decoration, or uniform density on every scaled fallback face. In Wall projection mode, decorative slopes outside the compatible broad-face convention retain their authored UVs. Triplanar samples every face physically, including slopes, tops and reveals.

## Trim mapping

The supplied trim finish now uses Triplanar, matching shared wall/slab finishes. Any shared construction finish can be assigned to a trim without cloning it or generating a trim-specific mapping variant. The trim convention remains one closed triangle submesh. Optional Box projection remains available for an explicit material choice.

The box frame follows normalized object directions and discards object scale; coordinates are measured from absolute world position. Tops/bottoms use along/depth, ends use depth/up and front/back use along/up. Both repeat dimensions and ordinary tiling/offset apply to every plane. The dominant plane is chosen per pixel before the existing two surface reads, and packed normal mapping uses that same UV frame. This includes rotated/tilted trims and nonuniform height/depth changes. Projection transitions can show hard seams on bevels; this mode does not blend between planes; Triplanar is the separate blended option. Source UVs, mesh sharing and authored mode remain available.

## Universal construction materials

The 33 imported finishes in Assets/EnvironmentAuthoring/Content/ConstructionMaterials/Imported use Triplanar and can be assigned unchanged to walls, slabs, trims, fences or paths. Base and packed maps remain shared. The new importer does not convert baked fence materials; those keep ordinary URP Lit/UV sampling, tint, alpha and slot assignments. The same geometry can use either kind of material. Geometry fitting is independent of mapping: projection cannot repair stretched pickets, jambs or baked decoration.

Triplanar projects absolute world Z/Y, X/Z and X/Y, blends by the absolute surface normal raised to Blend sharpness, and uses the sampled UV frame for each packed normal before blending world normals. Flat normals preserve the geometric normal. This follows Unity's normal-weighted triplanar convention; the existing derivative frame is retained to support independent repeat axes and the custom packed XY format rather than copying Shader Graph's normal texture decoder. See [Unity Shader Graph 17 triplanar documentation](https://docs.unity.cn/Packages/com.unity.shadergraph%4017.0/manual/Triplanar-Node.html).

Imported Wall-category finishes use V repeat = 2.8 m: one vertical image cycle over the default wall template height. The templates already use authored V=0..1; no mesh edit is needed. U repetition is unchanged, and floor/ceiling/roof/paving finishes retain their own repeat sizes. This is scale, not an automatic floor-local phase reset.

Repeat dimensions are physical metres and material-wide. World registration keeps coplanar geometry aligned through scaling/cropping, but moving an object changes its phase. Patterns blend/ghost near diagonal surfaces; brick courses and roof tiles are not guaranteed to turn a corner seamlessly. Triplanar does not follow a winding sidewalk's direction; choose Authored UV explicitly on a duplicated finish when path alignment matters; retained PathUV-named sample assets now default to Triplanar too. Baked atlases and alpha chain-link remain UV materials. Photo surfaces are explicitly candidates, not a promise that every image is mathematically seamless.

An isolated 1920?1080 player benchmark was attempted on the RTX 3060 Laptop GPU in D3D11 batch and D3D12 windowed modes. FrameTimingManager reported support but returned no positive GPU samples. No numerical GPU speedup or frame-time claim is justified; six versus two texture reads is the verified shader-work comparison. Profile the intended level on target hardware before choosing a universal default.

## Reference UVs

All 26 FBX templates have full-height V and two-dimensional mapping on visible surfaces, including ends, tops, aperture reveals and frame closures. Wall panels and trims use full source height; standalone door/window frames use their own actual vertical bounds. Horizontal closures use X/depth planar coordinates rather than constant V. These UVs are for tileable materials and may intentionally overlap; they are not unique lightmap UV islands. Geometry, material slots, pivots, markers and importer GUIDs remain unchanged. See WALL_KIT_AUTHORING.md and Tools/WallKitSource/README.md.

## Surface data and cost

UV, Wall and Box modes keep two surface texture reads: base color and packed linear RGBA surface data. The latter contains R/G encoded normal X/Y, B roughness and A reserved height. Use Default texture type with sRGB disabled; Unity Normal Map imports and ordinary metallic maps are not this format. The creation helper deliberately does not reinterpret them. Normal strength starts at zero. Height does not displace geometry or add another layer.

Those modes use one selected planar UV lookup. Triplanar instead blends three base and three packed-surface lookups (six reads), with extra normal reconstruction. It is an optional experiment, not a free replacement for UVs. Lighting and shadows have their own pipeline cost. The implementation follows the pinned URP 17.3 Lit lighting inputs and uses GetAbsolutePositionWS for registration. Refer to the installed ShaderLibrary/Lighting.hlsl, LitForwardPass.hlsl and Core's SpaceTransforms.hlsl. Mesh/material sharing permits instancing where Unity supports it; actual draw grouping and target-hardware GPU time must be measured separately.

## Verification

WallProjectionTests renders actual rigid openings, 0.7 m scaled and 0.2 m cropped solids, with the 2.8-to-2.995 m fitted-height extension that caused the visible row mismatch. Whole-material patterns are compared with a continuous face at 0/37/135 degrees, close/minified views and matched UV-only controls. A separate test constructs a real House, upper floor and window through the normal commands and verifies its fitted wall against the same physical mapping. Other tests retain source UV fallback, normals, translated phase, native mesh sharing, material assignment, Undo/save-reopen and generated-cover behavior.

Every non-zero-area source triangle across all 26 reference meshes must have non-zero UV area, and each reference mesh spans V=0..1. An isolated before/after import audit checks unchanged geometry, winding, normals, material slots, pivots, aperture markers and metadata. The combined modeling FBX is independently reimported and compared with those sources. Exact results and remaining limitations are recorded in RESTART_CHECKPOINT.md. Historical atlas decisions and diagnostics remain in PROJECT_DECISIONS.md/Artifacts; they are not current authoring instructions.

BoxProjectionTests compares all three real trim profiles with independently UV-mapped planes on tops, bottoms, front/back, ends and bevels. Cases include yaw 0/37/135, nonuniform resizing and a tilted placement, packed normals and texture tiling/offset. Top samples also distinguish the former authored-UV density. Mesh identity is retained.
