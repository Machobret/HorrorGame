# Player press card

Generated and refined with the built-in ImageGen tool with transparency enabled. The oxblood masthead study from the Player ID layout atlas supplied the design. The blank source is `PlayerId_PressCard.png`.

The panel at 1920 × 1080 measures 551 × 286 pixels. The portrait, name, press mark, equipped sprites, and health stamp are separate UI layers.

## Generation prompt

```text
Use case: precise-object-edit, photoreal production UI background for a field reporter survival horror game.
Reference image 1 is our comparison sheet. Use only card 01 at the very top left as the selected color and material design, and use its overlaid assets as placement guidance. Produce ONE substantially refined field reporter credential, centered and filling the image. Do not reproduce the grid or any of the overlay content. Generate a clean physical card background plate for the game to layer those elements onto.
The actual UI panel is 551 wide by 286 tall, so the physical card silhouette should measure approximately 1.93:1 width to height, excluding the transparent margin. Straight-on orthographic scan of a thin, believable issued press credential. Nearly square rounded corners, understated edge compression and corner wear from handling, warm desaturated ivory cotton-fiber cardstock, muted deep oxblood offset-printed masthead. Photoreal paper grain and ink, subdued contrast, even lighting, no perspective, no heavy distress. The visual world includes a tobacco brown satchel with a dark olive canvas lining. This card is single-layer paper stock with a crisp cut edge, no holder or leather.
Design the print to suit the exact overlay layout. The upper 13–14% of the card is a clean full-width oxblood masthead. It ends in a thin offset line and leaves room for white name and PRESS mark later. The paper starts beneath it and remains a single broad warm light face. On the left, from 4 to 43% of the width and 16 to 94% of the height, leave uninterrupted warm paper for the separate portrait of flexible proportions, with no printed window or frame. Put one extremely fine, faded warm gray vertical printed rule near 45% of the card width, starting below the masthead and ending just above the lower edge, like quiet document typography without text. On the right, keep the whole top half especially clean for the two shadowless equipment icons. At about 63% from the top, put one subtle horizontal printed hairline across the right column only, ending a little before the edge. The entire lower-right quarter remains unprinted warm cream for a two-line rubber ink stamp. Keep the design visually balanced and use these two quiet rules to connect it to the layout. Every line should be genuinely thin and visually recessive, the red masthead is the one strong print element.
Leave everything blank for compositing. No letters, characters, numbers, words, logos, badge marks, photo placeholder or silhouette, person, gun, flashlight, stamps, boxes, labels, punched holes, lanyard or clip. One credential only. Genuine transparent alpha around the full silhouette, with modest clear outer margins and no cast shadow. Render as a large crisp source image in a wide landscape format.
```

## Refinement prompt

```text
Use case: precise-object-edit. Refine this one blank oxblood and ivory press card, using this exact image as the edit target. The current broad red region is much too tall. Compress the whole red header and its thin underline upward by forty percent so the red becomes a NARROW masthead strip, only the upper one eighth of the total card height. Preserve the existing outer silhouette, there is no change to overall card height or width. In the current image the upper red band and accent end around image pixel y=235. In the output ending of the underline move it up to around y=140 at the same coordinate scale, comfortably above the content. Fill all vacated area with matching ivory matte paper and extend the thin vertical divider up to just below that new header. Keep the divider x position. Nudge the fine horizontal rule in the right column up slightly, to 63 percent of the silhouette height, around image pixel y=550 at the same coordinate scale. Use the same almost 2:1 aspect ratio and near-orthographic transparency. Keep the muted red, warm ivory stock and realistic print grain. Refine the abrasion with subtler rounded corners. Preserve the single blank card, with no text, photos, logos, equipment, captions, or new ornament. Transparency outside the whole card with clear margins.
```

## Clean revision

`PlayerId_PressCardClean.png` removes the two rules and red underline from the original. The built-in ImageGen tool used `PlayerId_PressCard.png` as the edit target.

```text
Use case: precise-object-edit. Edit the attached single blank press card in place with only three local removals. Carefully erase the skinny dark-red underline below the broad top red masthead, the faint vertical gray rule in the cream body, and the faint horizontal gray rule in the right side of the cream body. Fill each erased mark with the nearby warm ivory cardstock so the repaired strips naturally match the grain and color of their surroundings. The red masthead itself stays: same uninterrupted deep oxblood color and texture, exact thickness and top position, with clean direct transition from the bottom of that main banner into the ivory body. Keep the exact silhouette, all four corner curves, thin cut edge, worn details, paper color, aspect ratio, face-on view, crop, and transparent outer margin of the input. Preserve the texture everywhere outside the narrow erased strips. The entire ivory body should now be a continuous blank sheet, and the single broad banner is the only printed design. No border lines, underlines, text, logos, portrait boxes, props, or new design details. Return one transparent PNG card.
```

## Maverick portraits

The supplied Happy_base_color.png, Normal_base_color.png, and Sad_base_color.png are imported at their original 128 by 175 size as Maverick_PortraitHappy, Maverick_PortraitNormal, and Maverick_PortraitSad under Assets/Game/Characters/Male43. The border, tape, and tilt are composed in the menu.
