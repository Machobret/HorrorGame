using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // One instance per logical opening, outside replaceable wall output. Gameplay belongs to the prefab.
    internal static class HouseDoorObjects
    {
        private static HouseWallLayout.Span[] Doors(HousePlan plan, HouseGenerationData data, bool hasAttachments)
        {
            if (!hasAttachments && !plan.Data.Walls.Any(w => w.Openings.Any(o => o.Insert != null && o.Insert.UsesPrefab)))
                return System.Array.Empty<HouseWallLayout.Span>();
            var builder = plan.GetComponent<HouseWallBuilder>();
            if (builder == null) return System.Array.Empty<HouseWallLayout.Span>();
            var layout = data.WallLayout(plan, builder);
            return layout.IsValid ? layout.Spans.Where(s => s.DoorwayId != null && s.Insert != null && s.Insert.UsesPrefab).ToArray()
                : null;
        }

        private static Vector3 Position(HouseWallLayout.Span span)
        {
            var along = (span.End - span.Start).normalized;
            var at = span.Start + along * span.Definition.OpeningOutline.center.x;
            return new Vector3(at.x, span.Definition.OpeningOutline.yMin, at.y);
        }

        private static Quaternion Rotation(HouseWallLayout.Span span)
        {
            var along = (span.End - span.Start).normalized;
            return Quaternion.LookRotation(new Vector3(-along.y, 0, along.x), Vector3.up);
        }

        private static bool Matches(HouseDoorAttachment attachment, HouseWallLayout.Span span) =>
            attachment.Preset == span.Insert && attachment.SourcePrefab == span.Insert.Prefab;

        private static bool PoseCurrent(HouseDoorAttachment attachment, HousePlan plan, HouseWallLayout.Span span) =>
            Vector3.Distance(attachment.transform.position, plan.transform.TransformPoint(Position(span))) < .00001f
            && Quaternion.Angle(attachment.transform.rotation, plan.transform.rotation * Rotation(span)) < .001f;

        internal static bool RefreshMaterials(HouseDoorAttachment attachment, Material frame, Material glass, bool apply)
        {
            var preset = attachment.Preset;
            if (preset == null || !preset.UseWallMaterialRoles || attachment.Instance == null) return false;
            var renderer = attachment.Instance.transform.Find("Visual")?.GetComponent<MeshRenderer>();
            var source = attachment.SourcePrefab?.transform.Find("Visual")?.GetComponent<MeshRenderer>();
            if (renderer == null || source == null) return false;
            var current = renderer.sharedMaterials;
            var previous = attachment.InheritedMaterials;
            if (previous == null || previous.Length != current.Length) previous = source.sharedMaterials;
            if (current.Length != 2 || previous.Length != 2) return false;
            var desired = new[] { frame != null ? frame : preset.Material, glass != null ? glass : preset.MaterialFor(1) };
            bool changed = false;
            for (int i = 0; i < current.Length; i++)
                if (current[i] == previous[i] && current[i] != desired[i])
                { current[i] = desired[i]; changed = true; }
            if (!changed || !apply) return changed;
            Undo.RecordObject(renderer, "Inherit door finishes");
            Undo.RecordObject(attachment, "Inherit door finishes");
            renderer.sharedMaterials = current;
            attachment.InheritedMaterials = desired;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(attachment);
            return true;
        }

        private static bool RefreshMaterials(HouseDoorAttachment attachment, HousePlan plan, HouseWallLayout.Span span, bool apply)
        {
            var wall = plan.Data.Walls.First(w => w.Id == span.WallId);
            return RefreshMaterials(attachment, HouseFinishResolution.Role(plan, wall, 0), HouseFinishResolution.Role(plan, wall, 1), apply);
        }

        internal static bool IsCurrent(HousePlan plan, HouseGenerationData data)
        {
            var placed = plan.GetComponentsInChildren<HouseDoorAttachment>(true).Where(a => a.Owner == plan).ToArray();
            var doors = Doors(plan, data, placed.Length != 0);
            if (doors == null) return true; // The existing geometry validator owns invalid-source diagnostics.
            return doors.Length == placed.Length && doors.All(span => placed.Count(a => a.OpeningId == span.DoorwayId
                && Matches(a, span) && PoseCurrent(a, plan, span) && !RefreshMaterials(a, plan, span, false)) == 1);
        }

        internal static void Build(HousePlan plan, HouseGenerationData data)
        {
            var placed = plan.GetComponentsInChildren<HouseDoorAttachment>(true).Where(a => a.Owner == plan).ToList();
            var doors = Doors(plan, data, placed.Count != 0);
            if (doors == null) return; // A failed fit must not discard existing gameplay objects or their overrides.
            foreach (var span in doors)
            {
                var attachment = placed.FirstOrDefault(a => a.OpeningId == span.DoorwayId && Matches(a, span));
                bool created = attachment == null;
                if (attachment != null)
                {
                    placed.Remove(attachment);
                    // A manually deleted prefab stays deleted. Undo or a different preset restores it;
                    // geometry rebuilds must not resurrect ordinary scene content.
                    if (RefreshMaterials(attachment, plan, span, true)) data.HasOutputChanges = true;
                    if (PoseCurrent(attachment, plan, span)) continue;
                }
                else
                {
                    var root = new GameObject("Door attachment " + span.DoorwayId);
                    var content = HouseBuilding.For(plan)?.Find(plan)?.Content;
                    root.transform.SetParent(content != null ? content : plan.transform, false);
                    attachment = root.AddComponent<HouseDoorAttachment>();
                    attachment.Owner = plan; attachment.OpeningId = span.DoorwayId;
                    attachment.Preset = span.Insert; attachment.SourcePrefab = span.Insert.Prefab;
                    attachment.Instance = (GameObject)PrefabUtility.InstantiatePrefab(span.Insert.Prefab, plan.gameObject.scene);
                    attachment.Instance.transform.SetParent(root.transform, false);
                    if (attachment.Instance.GetComponent<HouseDoorObject>() == null)
                    {
                        var selection = attachment.Instance.AddComponent<HouseDoorObject>();
                        selection.hideFlags |= HideFlags.HideInInspector;
                    }
                    EditorUtility.SetDirty(attachment);
                    RefreshMaterials(attachment, plan, span, true);
                }
                // Content is not a generated-mesh root. Record only the anchor pose;
                // ordinary wall edits must not snapshot every gameplay script/override.
                if (!created) Undo.RecordObject(attachment.transform, "Move door attachment");
                attachment.transform.SetPositionAndRotation(plan.transform.TransformPoint(Position(span)), plan.transform.rotation * Rotation(span));
                if (created) Undo.RegisterCreatedObjectUndo(attachment.gameObject, "Place door prefab");
                EditorUtility.SetDirty(attachment.transform);
                data.HasOutputChanges = true;
            }
            foreach (var removed in placed)
            {
                HouseOutputUndo.Destroy(removed.gameObject);
                data.HasOutputChanges = true;
            }
        }
    }
}
