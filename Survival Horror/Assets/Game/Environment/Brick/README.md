# Clean brick wall material

Brick_Clean.mat uses the standard Environment Authoring/Horizontal Wall (Prototype) shader with Brick_Clean_Albedo.png. It is labelled Construction in House Properties and remains the same material asset already assigned to Houses. Original source images and provenance live under ArtSource/Environment/Brick.

The shared wall shader defaults to Start vertical tiling at wall bottom with Vertical repeat (metres) = 2.8. Vertical UV is height above the wall bottom divided by 2.8: shorter walls crop the image, and taller walls repeat it without changing texel size. Horizontal projection still repeats in metres. Separate wall/head bands share one bottom anchor, retained through save/reopen, vertical movement and Boolean cuts. Disable the bottom anchor to register vertical repeats to world zero instead. Ground/top projections keep their world-space tiling; authored UV materials retain their UVs.

This clean brick material enables Crisp point sampling, which samples original texels without mip averaging. Other standard materials retain their filtering unless this option is enabled. No unrelated brick normal map is assigned. Both original PNGs remain in ArtSource; neither source image was edited.
