using System.Linq;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Uses the existing properties window and row layout. No separate settings state.
    internal static class TownProperties
    {
        internal static VisualElement Panel()
        {
            var root = new VisualElement { name = "town-properties" };
            root.style.flexGrow = 1;
            var scroll = new ScrollView(); scroll.style.flexGrow = 1;
            scroll.Add(new IMGUIContainer(Draw)); root.Add(scroll);
            root.Add(new Button(() => TownEditingContext.Active?.Exit()) { text = "Done", name = "town-done" });
            return root;
        }
        private static T Asset<T>(string label, T value) where T : Object
            => (T)EditorGUI.ObjectField(HousePropertyFields.InputRect(new GUIContent(label)), value, typeof(T), false);
        private static float Number(string label, float value) => HousePropertyFields.DelayedFloat(label, value);
        private static (bool enabled, float height, Material material) CurbFields(bool enabled, float height, Material material)
        {
            enabled = EditorGUILayout.ToggleLeft("Curbs", enabled);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                height = HousePropertyFields.DelayedFloat(new GUIContent("Curb height (m)", "Height above the sidewalk surface."), height);
                material = Asset("Curb material override", material);
            }
            return (enabled, height, material);
        }
        internal static void Draw()
        {
            var context = TownEditingContext.Active;
            var target = context != null ? context.Owner : TownEditingContext.Selected;
            if (target == null) return;
            EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
            if (context == null)
            {
                EditorGUILayout.LabelField($"{target.Path.Points.Count} points / {target.Path.Spans.Count} spans");
                if (GUILayout.Button("Edit " + target.name)) TownEditingContext.Enter(target);
                return;
            }
            var controller = context.Controller;
            using (new EditorGUI.DisabledScope(controller.HasGesture))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                if (target is TownWallRun wall)
                {
                    var family = Asset("Wall / fence family", context.FenceBrowser);
                    // Partly authored catalogs are normal while users build a family.
                    var panels = (family?.Panels ?? Array.Empty<TownModule>()).Where(panel => panel != null).ToArray();
                    var postChoices = (family?.Posts ?? Array.Empty<FenceFamily.Post>()).Where(item => item?.Mesh != null).ToArray();
                    var model = wall.Model; var post = wall.PostMesh; var postMaterial = wall.PostMaterial;
                    var postMaterials = wall.PostMaterials;
                    float height = wall.Height, width = wall.PostWidth, postHeight = wall.PostHeight;
                    // Browsing is editor intent. Applying a family's defaults publishes
                    // explicit source choices once; catalog reorganization never changes a run.
                    if (family != context.FenceBrowser)
                    {
                        context.FenceBrowser = family;
                        if (panels.Length > 0)
                        {
                            model = panels[0]; height = model.Height;
                            var authoredPost = postChoices.FirstOrDefault();
                            post = authoredPost?.Mesh; postMaterials = authoredPost?.Materials ?? Array.Empty<Material>();
                            if (post != null) { width = post.bounds.size.x; postHeight = post.bounds.size.y; }
                        }
                    }
                    if (panels.Length > 0)
                    {
                        var labels = new[] { "Custom" }.Concat(panels.Select(panel => panel.name)).ToArray();
                        int index = Array.IndexOf(panels, model) + 1;
                        int next = EditorGUILayout.Popup("Panel", index, labels);
                        if (next != index && next > 0) { model = panels[next-1]; height = model.Height; }
                        if (next == 0) model = Asset("Panel model", model);
                    }
                    else model = Asset("Panel model", model);
                    using (new EditorGUI.DisabledScope(model != null && !model.AllowStretch))
                        height = Number(model != null && !model.AllowStretch ? "Height (authored)" : "Height (m)",
                            model != null && !model.AllowStretch ? model.Height : height);
                    var front = Asset("Front material", wall.FrontMaterial); var back = Asset("Back material", wall.BackMaterial);
                    bool posts = EditorGUILayout.ToggleLeft("Fence posts", wall.Posts);
                    using (new EditorGUI.DisabledScope(!posts))
                    {
                        if (postChoices.Length > 0)
                        {
                            // Imported mesh names retain source IDs. Show the normalized model filename.
                            var labels = new[] { "Custom" }.Concat(postChoices.Select(item =>
                            {
                                string path = AssetDatabase.GetAssetPath(item.Mesh);
                                return string.IsNullOrEmpty(path) ? item.Mesh.name
                                    : System.IO.Path.GetFileNameWithoutExtension(path);
                            })).ToArray();
                            int index = Array.FindIndex(postChoices, item => item.Mesh == post) + 1;
                            int next = EditorGUILayout.Popup("Post", index, labels);
                            if (next != index && next > 0)
                            {
                                var choice = postChoices[next-1]; post = choice.Mesh; postMaterials = choice.Materials;
                                width = post.bounds.size.x; postHeight = post.bounds.size.y;
                            }
                        }
                        var nextPost = Asset("Post mesh", post);
                        if (nextPost != post)
                            postMaterials = postChoices.FirstOrDefault(item => item.Mesh == nextPost)?.Materials ?? Array.Empty<Material>();
                        post = nextPost; postMaterial = Asset("Post material override", postMaterial);
                        width = Number("Post width (m)", width); postHeight = Number("Post height (0 = fence)", postHeight);
                    }
                    bool collision = EditorGUILayout.ToggleLeft("Generate collision", wall.GenerateCollision);
                    if (EditorGUI.EndChangeCheck()) TownCommands.Settings(wall, "Edit wall run settings", () =>
                    { wall.Model = model; wall.Height = height; wall.FrontMaterial = front; wall.BackMaterial = back; wall.Posts = posts; wall.PostMesh = post; wall.PostMaterial = postMaterial; wall.PostMaterials = postMaterials; wall.PostWidth = width; wall.PostHeight = postHeight; wall.GenerateCollision = collision; }, out context.Message);
                }
                else if (target is TownSidewalk walk)
                {
                    float width = Number("Width (m)", walk.Width), thickness = Number("Thickness (m)", walk.Thickness), repeat = Number("Repeat (m)", walk.MetresPerRepeat);
                    var curbs = CurbFields(walk.Curbs, walk.CurbHeight, walk.CurbMaterial);
                    var material = Asset("Material", walk.Material); bool collision = EditorGUILayout.ToggleLeft("Generate collision", walk.GenerateCollision);
                    if (EditorGUI.EndChangeCheck()) TownCommands.Settings(walk, "Edit sidewalk settings", () =>
                    { walk.Width = width; walk.Thickness = thickness; walk.MetresPerRepeat = repeat; walk.Material = material; walk.Curbs = curbs.enabled; walk.CurbHeight = curbs.height; walk.CurbMaterial = curbs.material; walk.GenerateCollision = collision; }, out context.Message);
                }
                else if (target is TownRoad road)
                {
                    float roadWidth = Number("Road width (m)", road.Width), thickness = Number("Thickness (m)", road.Thickness);
                    float repeat = Number("Repeat (m)", road.MetresPerRepeat);
                    var material = Asset("Road material", road.Material);
                    bool left = EditorGUILayout.ToggleLeft("Left sidewalk", road.LeftSidewalk), right = EditorGUILayout.ToggleLeft("Right sidewalk", road.RightSidewalk);
                    float width = Number("Sidewalk width", road.SidewalkWidth), height = Number("Sidewalk height", road.SidewalkHeight);
                    var curbs = CurbFields(road.Curbs, road.CurbHeight, road.CurbMaterial);
                    var sidewalkMaterial = Asset("Sidewalk material", road.SidewalkMaterial); bool collision = EditorGUILayout.ToggleLeft("Generate collision", road.GenerateCollision);
                    if (EditorGUI.EndChangeCheck()) TownCommands.Settings(road, "Edit road settings", () =>
                    { road.Width = roadWidth; road.Thickness = thickness; road.MetresPerRepeat = repeat; road.Material = material; road.LeftSidewalk = left; road.RightSidewalk = right; road.SidewalkWidth = width; road.SidewalkHeight = height; road.SidewalkMaterial = sidewalkMaterial; road.Curbs = curbs.enabled; road.CurbHeight = curbs.height; road.CurbMaterial = curbs.material; road.GenerateCollision = collision; }, out context.Message);
                }
                else EditorGUI.EndChangeCheck();
                EditorGUILayout.Space(7);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select all spans")) { controller.Points.Clear(); controller.Spans.Clear(); foreach (var span in target.Path.Spans) controller.Spans.Add(span.Id); SceneView.RepaintAll(); }
                    if (GUILayout.Button("Settle down")) TownCommands.Settle(target, controller.SelectedPoints(), out context.Message);
                }
                if (controller.Points.Count + controller.Spans.Count > 0)
                {
                    EditorGUILayout.Space(6); EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{controller.Points.Count} points / {controller.Spans.Count} spans");
                    var selected = controller.Spans.Select(target.Path.FindSpan).Where(span => span != null).ToArray();
                    if (selected.Length > 0 && target is TownWallRun)
                    {
                        EditorGUI.showMixedValue = selected.Any(span => span.Model != selected[0].Model);
                        EditorGUI.BeginChangeCheck(); var model = Asset("Model override", selected[0].Model);
                        if (EditorGUI.EndChangeCheck()) TownCommands.Edit(target, "Change span models", path => { foreach (var span in selected) path.FindSpan(span.Id).Model = model; return true; }, out context.Message);
                        EditorGUI.showMixedValue = selected.Any(span => span.Material != selected[0].Material);
                        EditorGUI.BeginChangeCheck(); var material = Asset("Material override", selected[0].Material);
                        if (EditorGUI.EndChangeCheck()) TownCommands.Edit(target, "Change span materials", path => { foreach (var span in selected) path.FindSpan(span.Id).Material = material; return true; }, out context.Message);
                        EditorGUI.showMixedValue = false;
                        EditorGUI.showMixedValue = selected.Any(span => span.BackMaterial != selected[0].BackMaterial);
                        EditorGUI.BeginChangeCheck(); var back = Asset("Back override", selected[0].BackMaterial);
                        if (EditorGUI.EndChangeCheck()) TownCommands.Edit(target, "Change back materials", path => { foreach (var span in selected) path.FindSpan(span.Id).BackMaterial = back; return true; }, out context.Message);
                        EditorGUI.showMixedValue = false;
                    }
                    if (controller.Points.Count == 1 && controller.Spans.Count == 0)
                    {
                        string id = controller.Points.First(); var point = target.Path.Find(id);
                        if (point != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            var position = EditorGUILayout.Vector3Field("Local position", point.Position);
                            if (EditorGUI.EndChangeCheck()) TownCommands.Edit(target, "Move town point", path => { path.Find(id).Position = position; return true; }, out context.Message);
                            if (GUILayout.Button("Dissolve junction")) TownCommands.Edit(target, "Dissolve town junction", path => path.Dissolve(id), out context.Message);
                        }
                    }
                    if (GUILayout.Button("Delete selection")) controller.DeleteSelection();
                }
                if (TownRebuild.NeedsRebuild(target)) EditorGUILayout.HelpBox("Assets changed. Rebuild to update geometry.", MessageType.Info);
                if (GUILayout.Button("Rebuild")) { TownRebuild.Force(target); controller.Prune(); }
            }
            if (!string.IsNullOrEmpty(context.Message)) EditorGUILayout.HelpBox(context.Message, MessageType.Info);
            if (target.Output != null && !string.IsNullOrEmpty(target.Output.Problem)) EditorGUILayout.HelpBox(target.Output.Problem, MessageType.Warning);
        }

        internal static VisualElement Toolbar()
        {
            var root = new VisualElement { name = "town-tools" };
            var enter = HouseSceneUI.Button("Edit layout", "wall", "Enter town layout editing", () => TownEditingContext.Enter(TownEditingContext.Selected));
            root.Add(enter);
            var body = new VisualElement(); root.Add(body);
            var select = HouseSceneUI.Button("Select", "select", "Select/drag points or spans. Drag empty space to box-select points. Shift adds; Ctrl removes.", () => { if (TownEditingContext.Active != null) TownEditingContext.Active.Mode = TownEditingContext.EditMode.Select; });
            var draw = HouseSceneUI.Button("Draw", "wall", "Click to chain segments on surfaces. Use the Move gizmo to adjust point heights. Escape ends the chain.", () => { if (TownEditingContext.Active != null) TownEditingContext.Active.Mode = TownEditingContext.EditMode.Draw; });
            var delete = HouseSceneUI.Button("Delete", "delete", "Delete points and their attached spans, or individual spans.", () => { if (TownEditingContext.Active != null) TownEditingContext.Active.Mode = TownEditingContext.EditMode.Delete; });
            var grid = HouseSceneUI.Button("Grid snap", "grid", "Toggle grid snapping. Shift bypasses only the grid.", () => HouseEditorSession.instance.SnapToGrid = !HouseEditorSession.instance.SnapToGrid);
            var spacing = new Button { text = ">", tooltip = "Grid spacing" };
            spacing.clicked += () => UnityEditor.PopupWindow.Show(spacing.worldBound, new HouseSnapPopup(HouseEditorSession.instance));
            foreach (var button in new[] { select, draw, delete }) body.Add(button);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; grid.style.flexGrow = 1; spacing.style.width = 24; row.Add(grid); row.Add(spacing); body.Add(row);
            HouseSceneUI.Watch(root, () =>
            {
                var context = TownEditingContext.Active; enter.style.display = context == null ? DisplayStyle.Flex : DisplayStyle.None; body.style.display = context != null ? DisplayStyle.Flex : DisplayStyle.None;
                HouseSceneUI.Selected(select, context?.Mode == TownEditingContext.EditMode.Select); HouseSceneUI.Selected(draw, context?.Mode == TownEditingContext.EditMode.Draw);
                HouseSceneUI.Selected(delete, context?.Mode == TownEditingContext.EditMode.Delete); HouseSceneUI.Selected(grid, HouseEditorSession.instance.SnapToGrid);
            });
            return root;
        }
    }
}
