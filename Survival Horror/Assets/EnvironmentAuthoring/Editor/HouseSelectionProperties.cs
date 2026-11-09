using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseSelectionProperties
    {
        private static bool showFloor = true, showFloorRooms = true;
        internal static void Draw(HouseEditorSession session, HouseEditorController controller)
        {
            var plan = session.SessionPlan;
            var editSelection = session.Selection;
            if (plan == null) return;
            controller.RefreshSelection();
            if (editSelection.Items.Count == 0) return;
            if (editSelection.Items.All(item => item.Type == HouseEditSelection.Kind.Trim))
            {
                HouseTrimProperties.Draw(session, controller);
                return;
            }
            var target = editSelection.Items[0];
            if (editSelection.Items.Count == 1 && (target.Type == HouseEditSelection.Kind.FloorSlab
                || target.Type == HouseEditSelection.Kind.CeilingSlab))
            {
                HouseRulerSlabProperties.Draw(session, controller, target.Type == HouseEditSelection.Kind.CeilingSlab);
                return;
            }
            if (editSelection.Items.All(item => item.Type == HouseEditSelection.Kind.SurfaceRegion))
            {
                DrawSegments(session, controller);
                return;
            }
            if (target.Type == HouseEditSelection.Kind.SlabCut)
            {
                HouseSlabProperties.Draw(session, controller);
                return;
            }
            if (editSelection.Items.All(item => item.Type == HouseEditSelection.Kind.DividerNode || item.Type == HouseEditSelection.Kind.Divider))
            {
                DrawDividers(session, controller);
                return;
            }
            bool onlyRooms = editSelection.Items.Count > 0 && editSelection.Items.All(i => i.Type == HouseEditSelection.Kind.Room);
            string title = onlyRooms ? editSelection.Items.Count == 1
                ? "Room " + (plan.Data.Cells.ToList().FindIndex(room => room.Id == editSelection.Items[0].Id) + 1)
                : $"{editSelection.Items.Count} rooms selected"
                : editSelection.Items.Count == 1 ? editSelection.Items[0].Type == HouseEditSelection.Kind.Wall ? "Wall"
                    : editSelection.Items[0].Type == HouseEditSelection.Kind.Node ? "Corner node" : "Opening"
                : $"{editSelection.Items.Count} selected";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (controller.HasSelectionGesture) return;
            var items = editSelection.Items;
            if (!onlyRooms && !session.CanEditSource)
                EditorGUILayout.LabelField("Apply or discard the pending values before editing this selection.", EditorStyles.wordWrappedMiniLabel);
            if (onlyRooms)
            {
                var rooms = plan.Data.Cells.Where(room => editSelection.Contains(HouseEditSelection.Kind.Room, room.Id)).ToArray();
                DrawRooms(session, controller, rooms);
                if (rooms.Length == 1 && HouseBuilding.For(plan) != null)
                {
                    if (GUILayout.Button("Focus room"))
                    {
                        var points = plan.Data.GetCellBoundary(rooms[0]);
                        var center = points.Aggregate(Vector2.zero, (a, b) => a + b) / points.Count;
                        float height = 0;
                        SceneView.lastActiveSceneView?.Frame(new Bounds(plan.transform.TransformPoint(new Vector3(center.x, height, center.y)), Vector3.one * 6), false);
                    }
                }
            }
            else
            {
                using var editFields = new EditorGUI.DisabledScope(!session.CanEditSource);
                if (items.All(i => i.Type == HouseEditSelection.Kind.Wall))
                {
                    var walls = plan.Data.Walls.Where(w => editSelection.Contains(HouseEditSelection.Kind.Wall, w.Id)).ToArray();
                    if (walls.Any(wall => !wall.IsPhysical) && GUILayout.Button("Make wall"))
                        controller.RunCommand(() => HouseEditCommands.SetWallPresence(plan, walls.Select(wall => wall.Id), FloorPlan.PhysicalPresence.Wall));
                    HouseAssetProperties.Walls(controller, plan, walls);
                    if (walls.Any(wall => wall.IsPhysical)) HouseMaterialEditor.DrawWallOverrides(plan, walls);
                    if (GUILayout.Button("Actions")) WallActions(session, controller, walls).ShowAsContext();
                }
                else if (items.All(i => i.Type == HouseEditSelection.Kind.Opening))
                {
                    var openings = plan.Data.Walls.SelectMany(w => w.Openings).Where(o => editSelection.Contains(HouseEditSelection.Kind.Opening, o.Id)).ToArray();
                    HouseOpeningProperties.Selection(controller, plan, openings);
                    if (openings.Length == 1)
                    {
                        EditorGUI.BeginChangeCheck();
                        float distance = HousePropertyFields.DelayedFloat("Along wall (m)", openings[0].Distance);
                        if (EditorGUI.EndChangeCheck())
                            session.Message = HouseEditCommands.MoveOpenings(plan,
                                new Dictionary<string, float> { [openings[0].Id] = distance }).Message;
                    }
                }
                else if (items.Count == 1 && items[0].Type == HouseEditSelection.Kind.Node)
                {
                    var corner = plan.Data.Corners.First(c => c.Id == items[0].Id);
                    EditorGUI.BeginChangeCheck();
                    float x = HousePropertyFields.DelayedFloat("X (m)", corner.Position.x);
                    float z = HousePropertyFields.DelayedFloat("Z (m)", corner.Position.y);
                    if (EditorGUI.EndChangeCheck())
                        session.Message = HouseEditCommands.MoveNodes(plan,
                            new Dictionary<string, Vector2> { [corner.Id] = new Vector2(x, z) }).Message;
                    bool canDissolve = plan.Data.CanDissolveCorner(corner.Id, out string dissolveReason);
                    using (new EditorGUI.DisabledScope(!canDissolve))
                        if (GUILayout.Button(new GUIContent("Dissolve junction", canDissolve ? "Join the two walls, preserving their settings." : dissolveReason)))
                            controller.RunCommand(() => HouseEditCommands.DissolveNode(plan, corner.Id));
                    if (GUILayout.Button("Delete junction and walls"))
                        controller.RunCommand(() => HouseEditCommands.DeleteNode(plan, corner.Id));
                    if (plan.Data.ConnectedWallCount(corner.Id) >= 2) HouseCornerProperties.Draw(session);
                }
                else GUILayout.Label(items.Any(i => i.Type == HouseEditSelection.Kind.Room)
                    ? "Select only rooms to change their finishes. Rooms do not participate in wall or node dragging."
                    : items.Any(i => i.Type == HouseEditSelection.Kind.Opening)
                    ? "Mixed selection: inspect together; move openings separately."
                    : "Drag a selected node or wall to move this group.", EditorStyles.wordWrappedMiniLabel);
            }
            if (GUILayout.Button("Clear selection")) controller.ClearSelection();
        }
        private static void DrawRooms(HouseEditorSession session, HouseEditorController controller, FloorPlan.Cell[] rooms)
        {
            if (rooms.Length == 0) return;
            var plan = session.SessionPlan;
            using (new EditorGUI.DisabledScope(!session.CanEditSource))
            {
                EditorGUI.showMixedValue = rooms.Any(room => room.IsOutdoor != rooms[0].IsOutdoor);
                EditorGUI.BeginChangeCheck();
                int classification = EditorGUILayout.Popup("Room use", rooms[0].IsOutdoor ? 1 : 0, new[] { "Indoor", "Outdoor" });
                if (EditorGUI.EndChangeCheck())
                    controller.RunCommand(() => HouseEditCommands.SetRoomOutdoor(plan, rooms.Select(room => room.Id), classification == 1));
                EditorGUI.showMixedValue = false;
            }
            using (new EditorGUI.DisabledScope(!session.CanEditSource)) HouseMaterialEditor.DrawRoomOverrides(plan, rooms);
            using (new EditorGUI.DisabledScope(!session.CanEditSource))
            {
                if (GUILayout.Button("Apply room finish to facing walls"))
                    controller.RunCommand(() => HouseEditCommands.ApplyRoomFinishToWalls(plan, rooms.Select(r => r.Id)));
                var building = HouseBuilding.For(plan);
                var level = building?.Find(plan);
                var boundary = level == null ? null : building.Below(level);
                if (boundary != null)
                {
                    var patches = HouseSceneQueries.BoundaryPatches(building, boundary.Id)
                        .Where(patch => patch.Upper == level && rooms.Any(room => room.Id == patch.UpperRoom)).ToArray();
                    bool removed = patches.Length > 0 && patches.All(patch => patch.Floor.CutAway);
                    if (GUILayout.Button(new GUIContent(removed ? "Restore room floor" : "Cut away room floor",
                        "Follows the room when resized. Also affects the ceiling below; walls and room settings remain. Manual cuts remain independent.")))
                        controller.RunCommand(() => HouseSlabCommands.SetRoomCutAway(plan, rooms.Select(room => room.Id), !removed));
                }
            }
        }

        private static GenericMenu WallActions(HouseEditorSession session, HouseEditorController controller, FloorPlan.Wall[] walls)
        {
            var menu = new GenericMenu();
            var plan = session.SessionPlan; var ids = walls.Select(w => w.Id).ToArray();
            if (walls.Any(w => w.IsPhysical))
            {
                int openings = walls.Sum(w => w.Openings.Count), trims = walls.Count(w => w.Trim != null);
                string label = openings + trims > 0 ? $"Make boundary only (removes {openings} openings, {trims} trims)" : "Make boundary only";
                menu.AddItem(new GUIContent(label), false, () => controller.RunCommand(() =>
                    HouseEditCommands.SetWallPresence(plan, ids, FloorPlan.PhysicalPresence.None)));
            }
            foreach (FloorPlan.WallType type in System.Enum.GetValues(typeof(FloorPlan.WallType)))
            {
                var selectedType = type;
                menu.AddItem(new GUIContent("Wall type/" + (type == FloorPlan.WallType.Auto ? "Automatic" : type.ToString())),
                    walls.All(w => w.Type == type), () => controller.RunCommand(() => HouseEditCommands.SetWallType(plan, ids, selectedType)));
            }
            if (walls.Length == 1)
                menu.AddItem(new GUIContent("Insert midpoint node"), false, () => controller.InsertNode(plan, ids[0], .5f));
            return menu;
        }

        internal static FloorPlan.Cell[] OwningRooms(HousePlan plan, IEnumerable<HouseSlabLayout.Patch> patches)
        {
            var level = HouseBuilding.For(plan)?.Find(plan);
            if (level == null) return System.Array.Empty<FloorPlan.Cell>();
            var ids = new HashSet<string>(patches.Select(p => p.Upper?.Id == level.Id ? p.UpperRoom
                : p.Lower?.Id == level.Id ? p.LowerRoom : null).Where(id => id != null));
            return plan.Data.Cells.Where(room => ids.Contains(room.Id)).ToArray();
        }

        private static void DrawDividers(HouseEditorSession session, HouseEditorController controller)
        {
            var items = session.Selection.Items;
            EditorGUILayout.LabelField(items.Count == 1 && items[0].Type == HouseEditSelection.Kind.DividerNode
                ? "Floor divider point" : "Floor divider", EditorStyles.boldLabel);
            if (controller.HasSelectionGesture || !session.CanEditSource) return;
            if (items.Select(i => i.BoundaryId).Distinct().Count() != 1)
            { EditorGUILayout.LabelField("Edit dividers from one slab at a time."); return; }
            var plan = session.SessionPlan;
            var boundary = HouseBuilding.For(plan)?.Boundaries.FirstOrDefault(b => b.Id == items[0].BoundaryId);
            if (boundary == null) return;
            if (items.Count == 1 && items[0].Type == HouseEditSelection.Kind.DividerNode)
            {
                var node = boundary.DividerNodes.First(n => n.Id == items[0].Id);
                EditorGUI.BeginChangeCheck();
                float x = HousePropertyFields.DelayedFloat("X (m)", node.Position.x);
                float z = HousePropertyFields.DelayedFloat("Z (m)", node.Position.y);
                if (EditorGUI.EndChangeCheck()) controller.RunCommand(() => HouseSurfaceCommands.MoveDividerNodes(plan, boundary.Id,
                    new Dictionary<string, Vector2> { [node.Id] = new Vector2(x, z) }));
            }
            if (GUILayout.Button("Clear selection")) controller.ClearSelection();
        }

        private static void DrawSegments(HouseEditorSession session, HouseEditorController controller)
        {
            var plan = session.SessionPlan;
            var items = session.Selection.Items;
            if (items.Select(item => item.BoundaryId).Distinct().Count() != 1)
            {
                EditorGUILayout.LabelField("Edit floor areas from one slab at a time.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            var item = items[0];
            var building = HouseBuilding.For(plan);
            var ids = items.Select(selected => selected.Id).Distinct().ToArray();
            var patches = HouseSceneQueries.BoundaryPatches(building, item.BoundaryId).Where(patch => ids.Contains(patch.Floor.Id)).ToArray();
            if (ids.Length == 0 || ids.Any(id => !patches.Any(patch => patch.Floor.Id == id))) return;
            var areas = HouseSceneQueries.SurfaceAreas(building, item.BoundaryId).Where(area => ids.Contains(area.RegionId)).ToArray();
            bool cutAway = patches.All(patch => patch.Floor.CutAway);
            EditorGUILayout.LabelField(areas.Length <= 1 ? cutAway ? "Cut-away floor area" : "Floor area"
                : cutAway ? $"{areas.Length} cut-away floor areas" : $"{areas.Length} floor areas", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(HouseSlabProperties.BoundaryLabel(building, patches[0].Boundary), EditorStyles.wordWrappedMiniLabel);
            if (controller.HasSelectionGesture) return;
            var segments = patches.Select(patch => patch.Floor).Distinct().ToArray();
            showFloor = EditorGUILayout.Foldout(showFloor, "Floor", true);
            if (showFloor)
            {
                using (new EditorGUI.DisabledScope(!session.CanEditSource))
                {
                    Finish(false);
                    Finish(true);
                    bool removed = segments.All(record => record.CutAway);
                    if (GUILayout.Button(new GUIContent(removed ? "Restore floor area" : "Cut away floor area",
                        "Follows this area's boundary without another set of handles. Also affects the ceiling below. Manual cuts remain independent.")))
                        controller.RunCommand(() => HouseSlabCommands.SetAreaCutAway(plan, item.BoundaryId, ids, !removed));
                }
            }
            var rooms = OwningRooms(plan, patches);
            showFloorRooms = EditorGUILayout.Foldout(showFloorRooms, rooms.Length > 1 ? $"Rooms ({rooms.Length})" : "Room", true);
            if (showFloorRooms)
            {
                if (rooms.Length == 0) EditorGUILayout.LabelField("No owning room on this level.", EditorStyles.wordWrappedMiniLabel);
                else DrawRooms(session, controller, rooms);
            }
            if (GUILayout.Button("Clear selection")) controller.ClearSelection();

            void Finish(bool ceiling)
            {
                var regions = patches.Select(patch => patch.Region(ceiling)).Distinct().ToArray();
                Material material = regions[0].Material;
                var effective = patches.Select(patch => HouseFinishResolution.RenderMaterial(HouseFinishResolution.Surface(building, patch, ceiling))).ToArray();
                EditorGUI.showMixedValue = effective.Any(value => value != effective[0]);
                EditorGUI.BeginChangeCheck();
                bool any = regions.Any(region => region.OverrideMaterial);
                var next = HousePropertyFields.InheritedMaterial(new GUIContent(ceiling ? "Underside finish" : "Floor finish",
                    ceiling ? "Applies to the owning ceiling regions below, including their extent beyond this floor area. Reset restores room/default inheritance."
                        : "Applies to this authored floor area. Reset restores room/default inheritance."),
                    material, effective[0], any, out bool reset);
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;
                if (changed) controller.RunCommand(() =>
                    HouseFinishCommands.SetSurfaceFinishes(plan, item.BoundaryId, regions.Select(region => region.Id), ceiling, !reset, next));
            }
        }

        // Both height scopes retain typed values in the same owner used by the ruler.
        // This view supplies labels and controls, never a second draft or edit policy.
    }
}
