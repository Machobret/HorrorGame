# City Herald newspaper

`Newspaper_Pickup.prefab` uses the template's **PickupDocument** component, not an inventory item workaround. Collecting it opens the native document reader, adds `Newspaper_CityHerald.asset` to Inventory.Documents, and hides the physical copy. The document is registered in DocumentDB so it can be restored and reread through the normal documents menu.

The casualty headline **1,998 DEAD** gives the storage keypad PIN **1998**. The readable transcript includes a handwritten shopkeeper's hint: “Storage lock changed. Use the toll on the front page. Digits only.” It does not change the keypad automatically; update both document/art and the door's SequencePuzzle solution if the code changes.

The physical paper is 32 × 43 cm, lightly creased, with a thin paper stack, solid collider and separate interaction trigger. Point-filtered newspaper artwork is also the document cover. The native reader provides a readable text transcript in addition to the print artwork.

Build/place: **Horror Game > Items > Build and Place Newspaper**. It places a single copy on the shop checkout and avoids duplicates. It marks the scene dirty; save normally. Content and article are rebuilt from `Editor/NewspaperBuilder.cs`. Artwork provenance is in `ArtSource/Items/Newspaper/Newspaper.md`.

In Electricity_Puzzle_Dom the nearby empty PickupDocument placeholder is retained inactive, and the newspaper occupies its counter position. The transcript spans three pages to fit the template reader without changing the UI for other documents.
