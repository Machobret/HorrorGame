using Mode = EnvironmentAuthoring.Editor.HouseEditorSession.Mode;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseEditorStatus
    {
        internal const string DeleteHint = "Click a wall, room boundary, floor divider, opening, slab cut or attached trim to remove it.";
        internal static string Instructions(HouseEditorSession session) => session.Message.Length > 0 ? session.Message
            : session.ActiveMode == Mode.Wall ? "Click to chain walls. Close a loop to create a room. Click the last point again or end on a wall to finish."
            : session.ActiveMode == Mode.Boundary ? "Click to chain wall-less room boundaries. Close a loop or connect to an existing boundary to enclose a room."
            : session.ActiveMode == Mode.Divider ? "Click draft points across a floor region or close a loop. Only a complete division is saved. Right-click removes a draft point."
            : session.ActiveMode == Mode.Door || session.ActiveMode == Mode.Window ? "Hover over a wall to preview an opening. Click to place; Escape clears selection, then exits."
            : session.ActiveMode == Mode.Slab ? "One shared cut removes the floor and ceiling panels. Walls and props remain."
            : session.ActiveMode == Mode.Trim ? "Click an exterior wall's bottom band; Shift adds, Ctrl removes. Assign a trim in House Properties."
            : session.ActiveMode == Mode.Boolean ? "Add a box or icosphere in House Properties, then click to place. Select a shape to move, rotate or resize it. Escape cancels."
            : session.ActiveMode == Mode.Delete ? DeleteHint + " Shared boundaries merge rooms; exterior walls open rooms. Escape clears selection, then exits."
            : "Click/drag to edit. Box-select nodes. Shift adds; Ctrl removes. Escape clears selection; with nothing selected, it exits.";

        internal static string StatusHint(HouseEditorSession session) => session.Message.Length > 0 ? session.Message
            : session.ActiveMode == Mode.Wall ? "Wall: click to chain; close a loop to add a room."
            : session.ActiveMode == Mode.Boundary ? "Room boundary: draw wall-less edges to enclose rooms."
            : session.ActiveMode == Mode.Divider ? "Floor divider: draw a complete split between material areas without walls."
            : session.ActiveMode == Mode.Door ? "Door: hover a wall to preview; click to place."
            : session.ActiveMode == Mode.Window ? "Window: hover a wall to preview; click to place."
            : session.ActiveMode == Mode.Trim ? "Trim: select exterior bottom bands; edit the attachments in House Properties."
            : session.ActiveMode == Mode.Boolean ? session.PlaceBooleanShape ? "Boolean cut: click to place a " + session.BooleanShape.ToString().ToLowerInvariant() + "; Escape cancels."
                : "Boolean cut: select a shape; W moves, E rotates, R resizes. Delete restores the cut surfaces."
            : session.ActiveMode == Mode.Slab ? session.SlabPolygon ? "Slab cut: click corners; first/last point or Enter finishes." : "Slab cut: drag a rectangle on the chosen boundary."
            : session.ActiveMode == Mode.Delete ? DeleteHint
            : "Select: click/drag objects; Shift adds; Ctrl removes; drag empty space to box-select nodes.";

    }
}
