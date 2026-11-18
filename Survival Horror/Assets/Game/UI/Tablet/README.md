# Utilitarian tablet map

The default HECorePrefabs profile uses the owned Tablet_Map prefab and Tablet_MapRenderer variant. Native map discovery, room/door states, map pickups, floors, panning, zoom and save data remain authoritative.

- Open the map normally. The first opening each play session displays a 2.6-second Hero Response Authority boot screen. Later openings go directly to the map. Startup timing uses real time while native UIMap pauses gameplay. Set Duration / OncePerSession on TabletStartupScreen to adjust it.
- I or the MAP DIRECTORY button toggles the left directory as an overlay above the map. The map and pan-arrow frame retain the same size and position when it opens or closes. Escape closes an open directory first; M closes the map.
- The compass is hidden. Yellow arrows sit near viewport edges at 42% opacity.
- The terminal font is 3270 Condensed; source and license information are in Fonts/README.md. It is used throughout the tablet, including the splash and map directory.
- TabletDisplayFilter sends the complete tablet display through Tablet_Screen.mat: a 960x540 visible pixel grid, 32 colour steps, ordered dithering, faint scanlines and edge shading. Material settings are editable. The map, font, menus and splash all share this filter; the rest of the game's UI is unaffected.
- The filter uses its own display camera with the template's map renderer configuration. Its input render target stays at the real screen size so standard UI raycasts and scrollbar dragging keep matching the pointer. The visible pixel reduction happens in the output shader. Targets/material instances are released when closed and resized with the screen.

Horror Game > UI > Build Utilitarian Tablet Map regenerates these presentation assets and registers the two prefabs. Rebuilding overwrites manual edits to generated prefabs. Review captures and tests belong in Exports/Reviews/Tablet and Assets/Sandbox/Tablet respectively. Tablet_Map is an owned copy of the native UI prefab because the filtered display needs to reorganize its Canvas hierarchy; it retains the native UIMap/UIMapList components and serialized references.

Necessary template extensions: UIMap has an optional sidebar layout, pan-frame reference, configurable crosshair colour, and an OnShown event for the presentation-only boot screen. It balances the tablet's cursor acquisition when closed. MapRenderer exposes grid/contour/player colours, preserving its original defaults. UIMapList parents entries with local transforms so their scale remains correct across Canvas resolutions. Other UI screens and gameplay managers are unchanged.

The hero-agency wording is provisional branding. The splash is visual only and does not change puzzle progression or unlock maps.

## Field terminal workspace

The owned Tablet_Map prefab opens to the map inside a worn dark casing. Use Power Controls or Service Log for tablet services and Back to Map to return. UIMap owns pause and map navigation. The directory overlays the map and closes on selection. The legend can be hidden. Symbol shapes distinguish known locked/unlocked doors, unvisited rooms and diagram-revealed circuit terminals. Unknown rooms and doors stay hidden.

Map-note placement has been removed. Existing save files containing legacy notes still load; those notes are ignored and no longer written. Log shows collected/read file counts and actual service changes made while the tablet is open. Service history is limited to 50 entries and saved with inventory data.

The polished legend uses real glyphs shared with the map overlay: a filled player arrow, a boxed cross for locked doors, an open-sided door outline for unlocked access, a hollow diamond for unexplored areas, and a boxed plus for revealed power points. Each row has a name and a short explanation. The popup has a close button and is mutually exclusive with the directory. Removing note controls gives the map additional vertical space.

Power binds to an existing PowerMapCircuitPanel and DistrictPowerGrid for the selected map. It reuses room-specific circuit-diagram permissions. It does not create a grid, expose unknown circuit names, or provide a remote reset. Enabling a circuit above capacity still trips the native grid; restore supply at the physical fuse box. A map without an authored power network displays an explicit unavailable state. Interacting with an authored power terminal opens its Power page.

Inventory Text Size in Settings > Game controls the shared text scale. Large is the default and uses 120% of the base size. The display filter uses 1280x720 sampling, subtle scanlines/dithering and a rare 2% flicker; it affects only the tablet. World rendering settings are untouched.

The utilitarian tablet builder applies TabletWorkspaceBuilder to regenerate the workspace and its actions. The inventory and files builder creates the paper tabs for those screens.

Validation captures: Exports/Reviews/TabletWorkspace. Tests: Assets/Sandbox/InventoryFolder/Editor/TabletWorkspaceChecks.cs, invoked by InventoryFolderValidation.BuildAndRun in an isolated editor.

The Map page is the default when opening the tablet normally. An authored district power terminal opens Power directly. The tablet has its own interface, with map controls and service actions. The legend blocks clicks through its panel.
