# Documents and Archive ledger

The built-in ImageGen tool generated `ReporterLedger_Body.png` and `ReporterLedger_TabsAtlas.png` with transparency enabled. The upper atlas sprite is the active ivory divider tab. The lower sprite is the recessed olive-gray tab. Either title can use either sprite.

## Placement

- The ledger panel measures about 559 × 995 px at 1920 × 1080. Both tab buttons occupy about 265 × 73 px. The page begins 5 px above the bottom of the tabs so they overlap the body. At 1440 × 1080 the panel is about 419 px wide and the tabs are about 199 px wide.
- Put both tabs behind the page edge so their bottom outlines are covered. Keep the small vertical offset on the recessed tab. Render Documents and Archive as live labels.
- Keep category rows, document rows, their focus highlights, chevrons, and pin states as UI controls. Their titles share a left edge. Fit row backdrops just inside the cloth spine. Archive keeps the read action where a transcript exists and clears pin state on archival.

## Source bounds

The coordinates use a top-left origin and describe the visible art before import padding. The preview uses these bounds to fit both states to the blockout.

| Artwork | Source size | X | Y | Width | Height |
| --- | --- | ---: | ---: | ---: | ---: |
| Ledger body | 973 × 1616 | 9 | 9 | 955 | 1599 |
| Active tab | 1552 × 1013 | 38 | 67 | 1475 | 388 |
| Recessed tab | 1552 × 1013 | 37 | 557 | 1477 | 388 |

## Clipped-corner index tabs

The revised `ReporterLedger_IndexTabsAtlas.png` uses the original ledger body as its material reference. Its small paper tabs draw on the projecting edges on the [Institut de France correspondence register](https://bibnum.institutdefrance.fr/ark:/61562/ai25406) and the exposed sheet edges in the [Kirtley ledger index](https://patents.google.com/patent/US1023941A/en).

- Display the 40 px-tall art at 1080p. Tuck the active tab’s bottom 6 px behind the page and recess the inactive one another 4 px.
- The review uses widths of 160 and 131 px at 16:9 and 126 and 110 px at 4:3. The tabs are grouped at the ledger’s left edge with an 8 px gap. Keep each label and its click target with its own tab.
- The revised atlas measures 1560 × 1008. The active sprite is x 91, y 120, width 1377, height 305. The recessed sprite is x 88, y 617, width 1380, height 302. Coordinates use a top-left origin.

### ImageGen prompt

```text
Use case: product-mockup with the attached ledger body as a material reference. Create a brand-new transparent atlas of two small, authentic paper edge index tabs for the top of this fictional field reporter's working ledger. The physical design is derived from archival book index tabs: a simple compact strip of cardstock sticking a short distance out of an edge, with straight cut sides, a barely radiused corner and one small diagonal cut at the upper-right corner. The exact geometry of each separate asset is a straight-edged horizontal rectangle roughly 4.6 times as wide as tall, with an extremely small triangular clip at the upper-right corner. The clip is around 5 percent of the width and a fifth of the height, no other flared shapes or shoulders. Flat horizontal top edge. Vertical left and right edges. Flat perfectly straight lower edge, because the last 15% of its height tucks under the blank ledger paper visible in the reference. It will be a small accessory to the ledger, roughly 160 pixels wide by 36 pixels tall on a 559-pixel-wide page. Draw it as real thin hand-cut paper with restrained soft wear and visible but quiet fibers. A slight compressed edge is enough. Use contemporary restrained editorial product photography, orthographic overhead view, perfectly flat no perspective or outside shadows. The upper sprite is active, medium-light warm stone manila, close to but a little darker than the attached ivory page, no radiused broad silhouette. The lower sprite is inactive, muted charcoal olive paper, no green saturation, with the exact same geometry and no added ornament. Both must be immediately legible behind a dark or light separate UI label. Stack these two identical-position separate tab silhouettes one above the other in one wide landscape PNG with true alpha transparency, each centered in its half. Large unbroken clean transparent gutter between them, complete isolated silhouettes, clear exterior padding. Do not reproduce the source ledger. No lettering, diagrams, lines, frames, embossing, holes, seams, stitching, curves, bevels, decorative shoulders, handles, visible dividers, pages, dark shadows, icons, or other objects.
```

## Rounded tabs and textured rows

The current mockup uses matching rounded tabs and two textured row backdrops. Both tab sprites fit the same 150 × 40 px bounds at 1920 × 1080 and 126 × 40 px bounds at 1440 × 1080. Category rows use olive-charcoal stock. The selected document uses dark oxblood with ivory text and a light pin when pinned. Unselected documents show dark text and any pin directly over the ledger paper. All row titles retain their common left edge. Row art derives from selection and category state in the UI.

| Artwork | Source size | X | Y | Width | Height |
| --- | --- | ---: | ---: | ---: | ---: |
| Active tab in `ReporterLedger_RoundedTabsAtlas.png` | 1560 × 1008 | 88 | 122 | 1385 | 305 |
| Recessed tab in `ReporterLedger_RoundedTabsAtlas.png` | 1560 × 1008 | 87 | 620 | 1386 | 301 |
| Category row in `ReporterLedger_RowBackdropsAtlas.png` | 1774 × 887 | 14 | 245 | 1749 | 140 |
| Selected row in `ReporterLedger_RowBackdropsAtlas.png` | 1774 × 887 | 14 | 507 | 1749 | 139 |

The built-in ImageGen tool produced both atlases with transparency. Tab artwork was edited from the clipped-corner atlas. Row artwork was generated from the text prompt.

### Rounded tabs prompt

```text
Use case: precise-object-edit. Edit the attached transparent atlas of the two paper index tabs. Keep this same arrangement of two equally sized extremely simple horizontal pieces of quiet realistic paper, ivory on top, dark muted olive on bottom, one above the other with a broad transparent gutter. Change their silhouette by removing the cut-off upper-right corners and using the same small smooth radius at ALL FOUR corners on BOTH pieces. Identical plain rounded rectangles, identical width, height and corner geometry, flat straight edges in between. The corner radius is about 11 percent of the tab height, small and gentle, NOT a pill, and with no flared shoulders. Aspect ratio around 4:1 each; this is a low profile index tab and will be displayed at about 150 × 40 px. Lower edge flat between the small corner radii, where it slides beneath a paper page. Preserve the ivory palette and desaturated olive palette, subtle aged matte pressboard texture, very light edge wear, believable paper cut. Orthographic face-on flat scans. High-resolution wide sheet. Transparent around both complete shapes and in the gutter, no shadows on the surrounding transparent area. No type, letterforms, graphics, inset outlines, borders, notches, clipped corners, separate labels, or other objects.
```

### Row backdrop prompt

```text
Use case: product-mockup. Generate one wide transparent PNG atlas with two very thin long rectangular textured strips, aligned with one directly above the other and a broad completely transparent gutter between. Exactly two. Both strips are about 12 times as wide as tall, equivalent to roughly 510 by 42 pixels in a survival-horror game document ledger. Direct face-on orthographic, flat, no outside shadows. Aim for the quiet look of genuine worn dyed cardstock laid flat and pressed for years against warm ivory ledger paper. Top strip for category headers is dark olive-charcoal near #46493e. The texture should be legible at the small displayed height: enough short pressed fibers, broad faint mottled dye variations, and rubbed paper edge detail to read as a physical material. Bottom strip for the selected document is deep muted oxblood around #65302e, similar to dark red offset ink on rough cardstock, with quiet ink mottling, subtle caught-in-the-grain fibers, and unobtrusive soft wear near the long edges. Keep the central fields mostly dark and low contrast behind future ivory interface text. Make the two strips feel like they were cut from the same range of flat index stock, narrow soft corner radius and very straight long edges. Texture and lightly rubbed corners are the design, so avoid embossed borders, ornament, cast shadows, leather, wood, cloth weave, metal, frayed strands, pinholes, streaks, stripes, gradients, bars inside the strips, symbols, lettering, guidelines or other objects. Maximize useful pixels and leave natural alpha transparency outside both complete strips.
```

## Body prompt

```text
Use case: product-mockup. Generate one high-resolution photoreal UI background asset for a field reporter's Documents and Archive ledger. This is a narrow, tall single visible page of a real open, clothbound working ledger with a modest page stack behind it. Flat, orthographic, perfectly front-on, squared to the frame, no perspective, roughly 0.60 width to height (matches a 559 × 929 display panel body at 16:9). Tall portrait rendering with generous useful resolution. Use a subtly worn, faded dark olive-charcoal bookcloth spine at the far left, extremely narrow: roughly 2 to 3 percent of overall width. A small few thin paper edges at the far right and foot and quiet rounded outer corners. The writing paper is matte warm gray-ivory, like old office ledger stock, lighter than the tobacco/olive satchel in the same game world and a fraction cooler than the cream press ID. Slight fiber grain and subtle age at the extreme edges. Keep the page remarkably flat, light and tone-even. Use at least 94% of the width for the large live game UI list, with clear content from near the top to near the bottom. Keep the top edge nearly straight and level. It will join a separate cardstock divider tab that overlays it at the top. The top edge must have little or no dark trim; reserve a warm pale flat landing edge so the foreground tab can join the page without a dark seam. Tactile photographed cardstock and bookcloth, balanced diffuse lighting. This is a single visible page, no open spread, center gutter, cover flap, torn holes, ring binder, or leather. Single isolated art cutout, genuine transparent surround, no cast shadow outside it, no desk, no environment. No text, printing, gridlines, watermarks, boxes, rules, dividers, tabs, numbers, objects or apparent slots.
```

## Tab prompt

```text
Use case: product-mockup. One high-resolution transparent mini-atlas of exactly two blank broad archive ledger divider tab sprites, one directly above the other with a wide horizontal transparent gutter. Each separate tab is a wide, shallow physical pressed cardstock divider tongue for a field reporter's clothbound ledger, corresponding to about 265×73 pixels at display scale, approximately 3.65 times as wide as tall, identical silhouettes and scale. Full front-on flat orthographic scan, a practical trapezoidal divider profile with softened top corners, gently sloping shoulders, broad flat top, a completely straight full-width bottom rim. This is only the visible tongue of the divider; no page behind it. The lowest fifth of each tab is flat, uniform and free of a shadow or bottom lip so it can overlap the top of a ledger page. Material color steps: top is foreground/active, warm gray-ivory old ledger cardstock, matte and light, target pale stone cream around #d8cbb2, subtle fiber grain and very little wear. Its bottom edge should look smooth when layered on top of similarly pale paper. Bottom is recessed/inactive, muted dark olive-gray dyed cardstock with subdued rubbed fibers, target #56574b, clearly darker than the foreground even at small size. Both tabs should hold a centered UI label added later, so keep their broad faces clean. Narrow controlled edge wear and very subtle real texture, no highlights that would obscure text. The dark tab is paper, never metal, leather or wood. Transparent alpha all around both, completely clear wide gutter and outer margins, no shadows into the gutters, no background objects. No text, outlines painted inside, logos, symbols, pins, punched holes, rivets, binder, page or backdrop. Maximize the size of each tab while preserving both complete silhouettes.
```

## Tab refinement prompt

```text
Use case: precise-object-edit. First reference is the two blank archival cardstock divider tabs to edit. Second reference is the actual warm ivory ledger paper. Return only the TWO tab sprites in one new transparent atlas, in their current order and with clear separation. Retain their matching trapezoid silhouettes and texture, but correct the proportions. Both tabs currently look nearly six times as wide as tall. Each tab in the output must be exactly about 3.65 times as wide as tall, like 1460 pixels wide by 400 pixels tall. Intentionally make them much less stretched horizontally and taller with stronger full-size shoulders. A landscape atlas roughly 1568 by 1024 with two 1460 by 400 tab cutouts can fit both with clean outer margins and a wide transparent gutter. For the upper warm ivory tab match the paper color in reference two, warm ivory stone, especially along the tab's flat, unshaded bottom for joining it seamlessly to the ledger. For the lower tab retain the distinctly dark muted olive-gray dyed cardstock. Face-on, matching cut shapes, subtler texture, no object outside the tabs, transparent gutters and margins. No numbers, letters, decoration, outlines, tabs attached to a book, or other graphics. Both complete silhouettes visible.
```
