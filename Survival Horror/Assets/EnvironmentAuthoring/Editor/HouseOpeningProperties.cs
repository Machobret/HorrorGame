using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // One compatible catalog for placement and bulk properties. Commands own edits and random resolution.
    internal static class HouseOpeningProperties
    {
        internal static OpeningFamily[] Families(IEnumerable<(WallFamily Family, float Width)> hosts, FloorPlan.OpeningKind? kind = null)
        {
            var targets = hosts.ToArray();
            if (targets.Length == 0) return Array.Empty<OpeningFamily>();
            return (targets[0].Family?.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Where(family => family != null
                && (!kind.HasValue || family.Kind == kind) && Enum.GetValues(typeof(OpeningFamily.Condition)).Cast<OpeningFamily.Condition>().Any(state =>
                    family.Models(state).Any(model => targets.All(host => family.Compatible(host.Family, host.Width, state).Contains(model)))))
                .Distinct().ToArray();
        }
        private static WallOpeningInsert[] Models(OpeningFamily family, OpeningFamily.Condition state,
            IEnumerable<(WallFamily Family, float Width)> hosts)
            => family == null ? Array.Empty<WallOpeningInsert>() : hosts.Select(host => family.Compatible(host.Family, host.Width, state))
                .Aggregate((a, b) => a.Intersect(b)).ToArray();

        internal static void Placement(HouseEditorSession session)
        {
            var plan = session.SessionPlan; var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            var host = session.OpeningHostFamily != null ? session.OpeningHostFamily : kit?.DefaultFamily;
            var kind = session.ActiveMode == HouseEditorSession.Mode.Window ? FloorPlan.OpeningKind.Window : FloorPlan.OpeningKind.Doorway;
            var hosts = new[] { (host, session.OpeningWidth == 0 ? 2 : session.OpeningWidth) };
            var families = Families(hosts, kind);
            if (!session.OpeningOnly && !families.Contains(session.OpeningFamily)) session.OpeningFamily = families.FirstOrDefault();
            EditorGUILayout.LabelField("Place " + (kind == FloorPlan.OpeningKind.Window ? "window" : "door"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Wall family: " + (host != null ? host.name : "None"), EditorStyles.miniLabel);
            int current = Array.IndexOf(families, session.OpeningFamily) + 1;
            EditorGUI.BeginChangeCheck();
            int next = HousePropertyFields.Popup("Insert family", current, new[] { "Opening only" }.Concat(families.Select(f => f.name)).ToArray());
            if (EditorGUI.EndChangeCheck()) { session.OpeningFamily = next == 0 ? null : families[next - 1]; session.OpeningOnly = next == 0; session.OpeningModel = null; }
            if (session.OpeningFamily == null || !families.Contains(session.OpeningFamily)) return;
            var states = Enum.GetValues(typeof(OpeningFamily.Condition)).Cast<OpeningFamily.Condition>()
                .Where(state => Models(session.OpeningFamily, state, hosts).Length > 0).ToArray();
            if (states.Length == 0) return;
            int index = Mathf.Max(0, Array.IndexOf(states, session.OpeningCondition));
            session.OpeningCondition = states[HousePropertyFields.Popup("Condition", index, states.Select(s => s.ToString()).ToArray())];
            var models = Models(session.OpeningFamily, session.OpeningCondition, hosts);
            if (!models.Contains(session.OpeningModel)) session.OpeningModel = null;
            bool random = HousePropertyFields.Toggle("Random model", session.OpeningModel == null);
            if (random) session.OpeningModel = null;
            else if (session.OpeningModel == null) session.OpeningModel = models[0];
            session.OpeningModel = OpeningModelPreview.Grid(models, session.OpeningModel, out _);
            EditorGUILayout.LabelField("Random resolves once per placement. Width is set in the toolbar.", EditorStyles.wordWrappedMiniLabel);
        }

        internal static void Selection(HouseEditorController controller, HousePlan plan, FloorPlan.Opening[] openings)
        {
            if (openings.Length == 0) return;
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            var hosts = openings.Select(o => (kit?.FamilyFor(plan.Data, plan.Data.Walls.First(w => w.Openings.Contains(o))), o.Width)).ToArray();
            var families = Families(hosts); var ids = openings.Select(o => o.Id).ToArray();
            OpeningFamily Family(FloorPlan.Opening opening) => opening.InsertFamily != null ? opening.InsertFamily
                : families.FirstOrDefault(f => f.Sets.Any(set => set != null && (set.Models ?? Array.Empty<WallOpeningInsert>()).Contains(opening.Insert)));
            var family = Family(openings[0]);
            EditorGUI.showMixedValue = openings.Any(o => Family(o) != family);
            EditorGUI.BeginChangeCheck();
            int index = HousePropertyFields.Popup("Insert family", Array.IndexOf(families, family) + 1,
                new[] { family == null && openings.Any(o => o.Insert != null) ? "Current / custom model" : "Opening only" }.Concat(families.Select(f => f.name)).ToArray());
            bool changed = EditorGUI.EndChangeCheck(); EditorGUI.showMixedValue = false;
            if (changed)
            {
                if (index == 0) controller.RunCommand(() => HouseEditCommands.ClearOpeningModels(plan, ids));
                else
                {
                    var selected = families[index - 1];
                    var state = Enum.GetValues(typeof(OpeningFamily.Condition)).Cast<OpeningFamily.Condition>().First(s => Models(selected, s, hosts).Length > 0);
                    controller.RunCommand(() => HouseEditCommands.SetOpeningFamily(plan, ids, selected, state));
                }
                return;
            }
            if (family != null && families.Contains(family))
            {
                var states = Enum.GetValues(typeof(OpeningFamily.Condition)).Cast<OpeningFamily.Condition>().Where(s => Models(family, s, hosts).Length > 0).ToArray();
                if (states.Length > 0)
                {
                    OpeningFamily.Condition State(FloorPlan.Opening opening) => opening.InsertFamily != null ? opening.Condition
                        : family.Sets.FirstOrDefault(set => set != null && (set.Models ?? Array.Empty<WallOpeningInsert>()).Contains(opening.Insert))?.State ?? OpeningFamily.Condition.Clean;
                    var state = State(openings[0]);
                    EditorGUI.showMixedValue = openings.Any(o => State(o) != state);
                    EditorGUI.BeginChangeCheck();
                    var next = states[HousePropertyFields.Popup("Condition", Mathf.Max(0, Array.IndexOf(states, state)), states.Select(s => s.ToString()).ToArray())];
                    changed = EditorGUI.EndChangeCheck(); EditorGUI.showMixedValue = false;
                    if (changed) { controller.RunCommand(() => HouseEditCommands.SetOpeningFamily(plan, ids, family, next)); return; }
                    var models = Models(family, next, hosts);
                    var selected = OpeningModelPreview.Grid(models, openings[0].Insert, out bool picked, openings.Any(o => o.Insert != openings[0].Insert));
                    if (picked) controller.RunCommand(() => HouseEditCommands.SetOpeningFamily(plan, ids, family, next, selected));
                    if (GUILayout.Button("Randomize selected models")) controller.RunCommand(() => HouseEditCommands.SetOpeningFamily(plan, ids, family, next));
                }
            }
            else
            {
                // Custom models remain editable without inventing a duplicate family from filenames.
                var choices = hosts.Select(h => HouseAssetProperties.OpeningChoices(h.Item1, kit, h.Width)).ToArray();
                var common = choices[0].Where(c => choices.All(list => list.Any(v => v.Kind == c.Kind && v.Module.OpeningShape == c.Module.OpeningShape && v.Model == c.Model))).ToArray();
                var interfaces = common.Where(c => c.Model == null).ToArray();
                int current = Array.FindIndex(interfaces, c => c.Kind == openings[0].Kind && c.Module.OpeningShape == (openings[0].Shape ?? "01"));
                EditorGUI.showMixedValue = openings.Any(o => o.Kind != openings[0].Kind || o.Shape != openings[0].Shape);
                EditorGUI.BeginChangeCheck();
                int selected = HousePropertyFields.Popup("Opening", current + 1, new[] { "Current" }.Concat(interfaces.Select(c => c.Label)).ToArray());
                changed = EditorGUI.EndChangeCheck(); EditorGUI.showMixedValue = false;
                if (changed && selected > 0)
                {
                    var choice = interfaces[selected - 1];
                    controller.RunCommand(() => HouseEditCommands.SetOpeningAssets(plan, ids, choice.Kind, 0, choice.Module.OpeningShape, null));
                }
                var models = common.Select(c => c.Model).Where(model => model != null).Distinct().ToArray();
                var model = OpeningModelPreview.Grid(models, openings[0].Insert, out bool picked, openings.Any(o => o.Insert != openings[0].Insert));
                if (picked)
                {
                    var choice = common.First(c => c.Model == model);
                    controller.RunCommand(() => HouseEditCommands.SetOpeningAssets(plan, ids, choice.Kind, 0, choice.Module.OpeningShape, model));
                }
            }
            var widths = new[] { 1f, 2f }.Where(width => openings.All(opening =>
            {
                var wall = plan.Data.Walls.First(w => w.Openings.Contains(opening));
                var host = kit?.FamilyFor(plan.Data, wall);
                var module = host?.OpeningFor(opening.Kind, width, opening.Shape);
                return module != null && (opening.Insert == null || opening.Insert.Fits(host, opening.Kind, module))
                    && (opening.InsertFamily == null || opening.InsertFamily.Compatible(host, width, opening.Condition).Contains(opening.Insert));
            })).ToArray();
            if (widths.Length == 0)
            { EditorGUILayout.LabelField("No common segment width is supported by this selection.", EditorStyles.wordWrappedMiniLabel); return; }
            EditorGUI.showMixedValue = openings.Any(o => o.Width != openings[0].Width);
            EditorGUI.BeginChangeCheck();
            int size = HousePropertyFields.Popup("Segment width", Mathf.Max(0, Array.IndexOf(widths, openings[0].Width)), widths.Select(w => w + " m").ToArray());
            changed = EditorGUI.EndChangeCheck(); EditorGUI.showMixedValue = false;
            if (changed) controller.RunCommand(() => HouseEditCommands.SetOpeningWidths(plan, ids, widths[size]));
        }
    }
}
