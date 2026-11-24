# Reporter menu

The live inventory prefab opts into the blockout through the Reporter Menu field on UIInventory. The host retains the existing pause, cursor, audio, map, and scripted inventory entry points. Separate document pickup and examine screens handle their gameplay flows.

ReporterMenuBridge reads the current character and inventory, publishes a read-only snapshot, and validates gameplay commands against live targets. ReporterMenu owns navigation and contextual inputs. ReporterMenuView builds the UI and reports pointer actions. Item capabilities and recipes come from Horror Engine data. A new item that uses those capabilities requires no changes to the menu.

Tab opens Inventory from gameplay and closes Inventory or Map back to gameplay. M or selecting the acquired tablet opens the map. Q returns from Map to Inventory. Use WASD or arrow keys to move within Inventory. Focused controls advertise their shortcuts. E or Enter opens an item's action list and selects an action. Press A or D in the action list to leave it and move from the source item. R opens a readable transcript from the ledger. In the ledger reader, Q goes back a page when available and E advances or exits. The ledger reads LedgerSection from each document and uses a default category when the field is empty.

Display fits document artwork to the available space beneath its centered title. In readout mode, the artwork remains visible beneath the dim and wrapped, scrollable transcript. The Display legend shows the commands available for the current page. Mouse chevrons remain below multi-page transcripts, and a single-page transcript only shows E Exit below Display.

Keep inventory tiles square. The selected item's full name belongs in Display. After the player acquires the tablet, it appears as a wide target below the grid and opens the map. Down reaches it when there is no item below, and Up returns to the last inventory item. Left and right restore the previous target when crossing columns.

Use the same gutter for the inventory panel edges, the spaces between tiles, and the separation between the grid and tablet. Item icons grow on hover or focus. A selected item gets a white silhouette outline. Item shortcuts appear below Inventory. Item actions stack from the bottom of Display, with matching rounded paper and oxblood states.

Ammo, charge, and stack counts use a single readout per item. Inventory anchors it bottom right. The ID card anchors it below the equipped icon at bottom center. A soft circular shadow behind an inventory icon marks equipped gear. ID card icons mirror inventory hover and selection without accepting input. Gear stored only in an equipment slot has an inventory menu entry without taking up gameplay capacity. Health appears in bold white text on a green, gold, or red badge.

The ID displays the character portrait as a tilted photo with a white border and a strip of tape. In the menu art asset, match a condition portrait set to the character's base portrait. Maverick uses Happy when healthy, Normal when wounded, and Sad when critical. The base portrait also covers unknown health and missing variants.

Categories and documents share row bounds and a text inset, with the visible letters centered vertically in each row. Keep the chevron and pin in the shared gutter. The focused category uses an inset oxblood fill that leaves the original row visible at its edges. The page covers the tabs' bottom corners, with oxblood for the open section and paper for the other.

Lay out the menu across landscape ratios from 4:3 through 16:9, including 16:10. Outside that range, center and uniformly scale the nearest supported layout. The gameplay dim covers the entire screen independently of the menu bounds.

Focus, scroll, collapsed sections, and the current reading page live with the current game session. Loading a save resets navigation. The inventory save retains archive membership and pin order by document ID. Gameplay can call Inventory.DocumentLedger.Archive(document.UniqueId) when a clue becomes obsolete. This operation also clears its pin. Configure the milestone at the gameplay event that makes the clue obsolete.

E toggles the selected document's pin in the ledger. Pinning a fourth replaces the oldest. In gameplay, C slides in the pinned document artwork and pauses gameplay. The chevron starts on the leftmost document and remembers the last selection for the current session. A and D change the selection. E opens the selected document full-screen. Tab, Escape, or C dismisses the artwork overlay.

A fresh Chapter 1 game starts with an empty inventory, equipment slots, and document ledger. Chapter1PlayerStart configures this loadout. The shared Horror Engine DocumentDB still resolves preview documents for saves made during menu review.

Item actions use ReporterActionAppearance for their paper and oxblood surfaces, typography, and label motion. The item actions, ledger accents, and health badge use the same 4-pixel corner radius on their nine-sliced artwork. Use `UpdateRowSurfaces` to regenerate these surfaces.

The inventory prefab assigns the ReporterMenuPreview art asset. Its Warm Graphite surfaces are drawn by the preview builder, which also maps existing sprites to local half-size preview copies by sprite reference. Other sprites keep their original artwork. The preview PNGs use no mipmaps. The equipped shadow uses bilinear filtering to smooth its edges. Regenerate the PNGs from the project root in Windows PowerShell with `Add-Type -Path Tools/Art/BuildReporterMenuPreview.cs -ReferencedAssemblies System.Drawing` and `[BuildReporterMenuPreview]::Run((Get-Location).ProviderPath)`. Use `UpdateSurfaces` to rebuild the backgrounds and row surfaces alone. The other source material is under ArtSource/ReporterMenu.
