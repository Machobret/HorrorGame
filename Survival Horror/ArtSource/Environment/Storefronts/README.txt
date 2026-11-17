Storefronts atlas

Updated square version: Storefronts_SquareAtlas.png is 4096x4096, packed into five fully filled rows with 16-pixel edge extrusion. All 22 images are resampled, uncropped, unrotated and retain proportions within pixel rounding. Use Storefronts_SquareAtlas.json for this version's UV rectangles. BuildSquareAtlas.ps1 rebuilds from Sources using Storefronts_SquareLayout.json. Validated complete padded coverage, no overlaps or out-of-bounds, and all 22 unique sources. Review: Exports/Reviews/StorefrontAtlas/SquareAtlasPreview.png.

Storefronts_Atlas.png: 8192x4096 RGBA, native-resolution unrotated textures with 16-pixel clamped edge padding. No rescaling, recoloring, or AI editing.
Storefronts_Atlas.json: named pixel rectangles (top-left origin) and Unity UV rectangles (bottom-left origin, x/y/width/height), excluding padding, plus source SHA256 hashes.
Sources: local copies of the 22 included textures. Original download files are unchanged.
BuildAtlas.ps1: repeatable packing recipe; optional SourceDirectory parameter can point at Sources for an offline rebuild.

Selection: all shop_front*.png and shopfront_neon.png, restaurant_window.png, building_5c.png, building_pub_old.png, building_showroom_vacant.png. Visually selected storefront assets; unrelated apartment/office facades and standalone industrial shutters are excluded. shop_front_top1.png is an upper storefront supplement.
Provenance: user-provided directory C:\Users\manta\Downloads\buildings-apartments-shopfronts-shutters. No license file was present in the supplied folder; upstream author/license is not recorded here.

Validation: all 22 source regions pixel-identical after PNG save; padded rectangles checked in bounds and non-overlapping; atlas and labeled index visually reviewed.
Usage: use JSON UV rectangles, not an equal tile grid. Clamp atlas wrapping. Preserve 8192 import size to retain native resolution. Padding reduces filtering bleed but cannot isolate tiles at every mip level. No Unity importer/material/mesh changes were made.
Review images: Exports/Reviews/StorefrontAtlas/Storefronts_Index.jpg and AtlasPreview.png.
