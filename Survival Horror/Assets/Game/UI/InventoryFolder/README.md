# Folder inventory

An owned presentation of Horror Engine's inventory: muted gray-brown folder, off-white torn notebook and dim monochrome procedural fog. Open the inventory normally in Play mode. Item data, capacity, selection, equip, combine, examine and drop use the existing template components.

`Inventory_Folder.prefab` is registered in HECorePrefabs. Rebuild with **Horror Game > UI > Build Folder Inventory**. The builder copies and unpacks the vendor UIInventory prefab into this feature folder; it does not edit the vendor prefab or inventory logic. Rebuilding replaces presentation changes made directly to the owned prefab.

Fog visibility and drift speed are material properties on `Inventory_Fog.mat`; the presentation component supplies an unscaled clock so fog continues while gameplay is paused. Opening and page selection use short unscaled fades/rotation.

Paper text uses Courier Prime (SIL Open Font License, retained in Fonts); labels reuse the tablet's 3270 font. Courier Prime sources: https://github.com/google/fonts/tree/main/ofl/courierprime . Generated paper art prompts and provenance are in `ArtSource/InventoryFolder/Generation.md`.

Integration validation is in Assets/Sandbox/InventoryFolder/Editor. Review captures and logs belong in ignored Exports/Reviews/InventoryFolder.

Inventory_ItemPalette.mat desaturates inventory item sprites to 45% without changing shared icon textures. Inventory_PaperPalette.mat removes the paper textures’ original yellow/blue colors and applies a shared warm gray tint. Selection uses off-white. The inventory condition window replaces the ECG with procedural backlit liquid, bubbles and mirrored blood tendrils. InventoryConditionFluid reads existing player health without changing global status definitions. FINE is at or above 66%, INJURED is above 33% and below 66%, and CRITICAL is at or below 33%. The label changes immediately; the blood amount eases smoothly using unscaled time. Material instances are released when the menu closes. Tune state thresholds and transition speed on the window component, and drift speed on Inventory_ConditionFluid.mat.

The same builder also produces Inventory_Files.prefab, registered as the native document list. It retains UIDocs document collection, paging, selection, category buttons and opening the existing document reader. Paper selection rotation is limited to ±0.12 degrees; the notebook has no Field Notes heading.

Current presentation uses condition text only. ShowFluid is disabled on InventoryConditionFluid, so the liquid image is hidden and no liquid material instance is created. The shader and material remain available for later experiments.

Layout refinement: matching Inventory / Files / Map navigation, selected-slot outlines, space-used counter, alpha-bound icon fitting and reduced paper texture contrast. Inventory_IconProfile.asset stores visible sprite bounds without changing original textures or item data. The tablet opens as a separate interface.

Vendor integration: UIExamineItem.Show now asks UIExamineItemRenderer.FrameModel to center visible geometry and fit a rotation-safe bounds sphere to the camera. This replaces fixed-distance framing while retaining examination controls and interactive colliders. UIMap exposes category transitions equivalent to the existing inventory/files transitions.

Inspection also assigns instantiated preview renderers to a camera-visible layer when their source layer is excluded. This fixes invisible custom props without changing their world prefabs. The missing PlantMixBG examination reference is linked to its existing template examination prefab.

Paper detail pass: clipped-corner index tabs replace rectangular navigation. Notebook quantities show loaded rounds/capacity for firearms, rounds for linked ammunition, and quantities for other stackable items. Equipped entries have a stamp; empty equipment areas have faint labels. UIInventory.OnFeedback reports real equip and combine results to a short-lived notebook message. File thumbnails have titles and unread folded corners. UIDocument.Show records read documents in the existing inventory; InventorySaveData.ReadDocuments persists them and remains optional for older saves. Existing Paper.wav and Select.wav play through the native UIAudio at 22% volume for owned inventory/files menus. The original one-argument UIAudio.Play API remains available.

## Footprints and readability

Shotgun, hunting rifle and MP5 now cost two actual inventory places, using ItemData.InventorySlots (other items default to one). The native inventory still stores normal InventoryEntry records. Weighted capacity checks govern pickup/addition and equipment return; InventoryFootprintLayout presents wide items across adjacent cells without fake inventory items. The owned UI uses the existing pooled item-slot rebuild path when capacity changes. Old saves are expanded only when necessary to preserve all existing items after footprint costs increase.

Wide item icons are rotated to their alpha-mask principal direction for legibility, using bounds stored in Inventory_IconProfile; source item images are unchanged. The combine source has a persistent COMBINE stamp and instruction until selection ends. Settings > Game provides Inventory Text Size. Large is the default, and the saved size applies to the inventory, files and tablet. The Game settings set registers the owned Inventory_TextSizeSetting asset. The option reads the previous local preference until a new size is saved. With a folder tab focused, D or Right highlights the next tab and A or Left highlights the previous one. Tablet service history uses an optional inventory save field. Removed map-note data from older saves is accepted and ignored.

Capacity-dependent UI rebuilds reuse the native object pool and return pooled slot objects correctly. Hidden surplus entry records are not selectable, and default keyboard focus chooses an available visible slot. Confirming a folder tab does not also submit the previously selected item.

Item selection uses a hollow ivory frame with a 1.1-unit border and 2.6-unit corner brackets. Both native Selected and SelectionLocked states retain the frame without a filled selection wash. The graphic does not receive pointer events or alter item images.
