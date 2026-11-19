# Shop window advertisements

## Select and place in the building tools

Select the building's ground floor, enter **Edit House**, then choose **Fixture**. The seven **Ad - ...** choices appear in the existing fixture palette. Click the glass from outside to place a banner facing the street. Ads keep a 12 mm gap from the mounting surface to avoid glass clipping. Ordinary fixtures still reject window and door inserts.

Click a placed ad and use the **Advertisement** dropdown in Inspector or Procedural Environment Properties to change its design. **Move fixture** places it on another surface; normal Unity scaling changes its size. These are normal prefab instances attached to the wall, with Undo support and no scene-specific runtime script.

`ShopWindowAdvertisements.asset` holds the choices. Add or edit choices there. **Horror Game > Environment > Create Shop Advertisement Selection** adds missing shop ad choices. The local House Toolkit uses optional opening-surface mounting and the surface-decoration selector for this integration.

Seven worn printed banners mix Hero HQ notices with neighborhood shop promotions.

- `WindowAd_HeroHQ_Protection`: Your district. Our duty.
- `WindowAd_HeroHQ_StayCalm`: public safety notice.
- `WindowAd_Shop_Essentials`: groceries / approved stock.
- `WindowAd_Shop_Break`: drinks and snacks.
- `WindowAd_Shop_ColdDrinks`: ice-cold drinks and a can offer.
- `WindowAd_Shop_Coffee`: hot coffee and pastry.
- `WindowAd_Shop_Hours`: posted opening hours.

Drag the prefabs from `Prefabs` onto a storefront. Each is 1.2m wide by 0.6m high with a centered pivot; the readable front faces local -Z. Place the sheet slightly in front of the window (about 0.003m) to avoid z-fighting. These are opaque printed vinyl/paper banners, not replacement glass materials. Their reverse is visible with mirrored print. No colliders or runtime components are included.

Materials use matte URP Lit shading. Window textures import at 512 px with point filtering and mipmaps. `CornerMarket_Fascia` is a separate 3 × 1 m sign prefab that imports at 1024 px. Scale the prefabs to fit your storefront. Original masters and generation prompts are in `ArtSource/Environment/NeighborhoodStore/WindowAds`. **Horror Game > Environment > Create Missing Shop Window Ads** creates missing materials and prefabs and applies the documented texture settings.
