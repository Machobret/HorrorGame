using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    public static class HouseCollisionBuilder
    {
        [Serializable] private sealed class WallInputs
        {
            public Vector2 Start, End;
            public float Height, Thickness;
            public Rect Opening;
        }
        public static HouseCollisionOutput Output(HousePlan plan) => plan.GetComponentsInChildren<HouseCollisionOutput>(true)
            .FirstOrDefault(o => o.Owner == plan);

        private static bool IsSceneObject(Component c) => !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid()
            && c.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(c.gameObject.scene);

        internal static void RemoveOrphanedOutput(HousePlan plan)
        {
            foreach (var output in plan.GetComponentsInChildren<HouseCollisionOutput>(true))
                if (output != null && output.Owner == null && IsSceneObject(output)
                    && !PrefabUtility.IsPartOfNonAssetPrefabInstance(output))
                {
                    HouseOutputUndo.Remove(output.gameObject);
                }
        }

        public static void Refresh(HousePlan plan)
        {
            HouseRebuildCoordinator.RefreshCollisionNow(plan);
        }

        private static string Signature(HousePlan plan, HouseGenerationData data)
        {
            var builder = plan.GetComponent<HouseWallBuilder>();
            string kit = "";
            if (builder != null)
                foreach (var definition in builder.Data.AllDefinitions(plan.Data))
                    kit += data.KitSignature(definition);
            return Hash128.Compute("wall-side-collision-v15" + HouseInputSignatures.PlanWallGeometryInputs(plan.Data) + plan.Data.GenerateCollision + kit
                + HouseInputSignatures.WallSourceInputs(builder?.Data, plan.Data, data: data)
                + HouseInputSignatures.KitValiditySignature(builder) + plan.gameObject.layer
                + data.WallHeightInputs(plan) + data.BooleanInputs
                + (builder != null && builder.enabled) + plan.HasSupportedTransform).ToString();
        }

        private static bool Current(HouseCollisionOutput output, string signature) => output.InputSignature == signature
            && output.Groups.All(group => group.Root != null && group.Root.transform.parent == output.transform
                && group.Root.GetComponentsInChildren<Collider>(true).Length == group.ColliderCount
                && group.Root.GetComponentsInChildren<MeshCollider>(true).All(collider => collider.sharedMesh != null));

        internal static bool IsCurrent(HousePlan plan, HouseGenerationData data)
        {
            var owned = Resources.FindObjectsOfTypeAll<HouseCollisionOutput>()
                .Where(output => output.Owner == plan && output.gameObject.scene == plan.gameObject.scene).ToArray();
            return owned.Length == 0 ? !plan.Data.GenerateCollision || (plan.Data.Cells.Count == 0 && plan.GetComponent<HouseWallBuilder>() == null)
                : owned.Length == 1 && Current(owned[0], Signature(plan, data));
        }

        internal static void Build(HousePlan plan, HouseGenerationData data = null)
        {
            if (plan == null || Application.isPlaying || EditorUtility.IsPersistent(plan)
                || HouseRebuildCoordinator.NeedsSceneInstance(plan)) return;
            data ??= HouseGenerationData.Create(plan);
            var owned = Resources.FindObjectsOfTypeAll<HouseCollisionOutput>()
                .Where(o => o.Owner == plan && o.gameObject.scene == plan.gameObject.scene).ToArray();
            if (owned.Length > 1)
            {
                foreach (var duplicate in owned)
                {
                    const string problem = "Multiple generated collision roots claim this owner. Resolve the duplicate output before rebuilding.";
                    if (duplicate.Problem == problem && !duplicate.gameObject.activeSelf && duplicate.InputSignature == "") continue;
                    duplicate.Problem = problem;
                    duplicate.InputSignature = "";
                    duplicate.gameObject.SetActive(false);
                    EditorUtility.SetDirty(duplicate);
                    EditorSceneManager.MarkSceneDirty(plan.gameObject.scene);
                    data.HasOutputChanges = true;
                }
                return;
            }
            var output = owned.FirstOrDefault();
            if (output != null) data.HasOutputChanges |= HouseRebuildCoordinator.RestoreOutputPose(output, plan.transform);
            var builder = plan.GetComponent<HouseWallBuilder>();
            string signature = Signature(plan, data);
            if (output != null && Current(output, signature)) return;
            if (output == null)
            {
                if (!plan.Data.GenerateCollision || (plan.Data.Cells.Count == 0 && builder == null)) return;
                var root = new GameObject("Generated Collision - edit the House plan");
                root.transform.SetParent(plan.transform, false);
                output = root.AddComponent<HouseCollisionOutput>();
                output.Owner = plan;
            }
            var priorChildren = output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
            try
            {
                output.Problem = "";
                var next = new List<HouseCollisionOutput.Group>();
                bool registered = HouseBuilding.For(plan) != null;
                bool supported = registered && plan.HasSupportedTransform && data.SlabProblem.Length == 0;
                if (!registered) output.Problem = "Register this plan as a House level before generating collision.";
                else if (!plan.HasSupportedTransform) output.Problem = "Collision requires an upright House at world scale (1, 1, 1).";
                else if (!supported) output.Problem = data.SlabProblem;
                if (supported)
                {
                    if (builder != null && builder.enabled)
                    {
                        var layout = data.WallLayout(plan, builder);
                        if (!layout.IsValid) output.Problem = "Wall collision unavailable: " + layout.Problem;
                        else
                        {
                            foreach(var cover in layout.Corners)
                            {
                                float height=HouseHeightWalls.CornerTop(plan,cover,data);
                                if(height<=.0001f || cover.Size.x<=.0001f || cover.Size.y<=.0001f)continue;
                                string inputs=JsonUtility.ToJson(new CornerInputs {Along=cover.Along,Size=cover.Size,Profile=cover.Profile,Bottom=0,Top=height});
                                if (data.BooleanInputs.Length > 0) inputs += ":center:" + cover.Center.ToString("R");
                                var group=RetainOrCreate(output,"corner:"+cover.Id,"local-corner-v1:" + inputs + data.BooleanInputs,out bool changed);
                                var origin = cover.Profile == null ? cover.Center
                                    : cover.Center-(cover.Along*cover.Size.x+new Vector2(-cover.Along.y,cover.Along.x)*cover.Size.y)/2;
                                group.Root.transform.localPosition=new Vector3(origin.x,0,origin.y);
                                if(changed)
                                {
                                    group.Root.transform.localRotation=Along(cover.Along);
                                    if (cover.Profile == null)
                                    {
                                        Box(group.Root.transform,new Vector3(0,height/2,0),new Vector3(cover.Size.x,height,cover.Size.y));
                                    }
                                    else
                                    {
                                        group.Root.transform.localScale=new Vector3(cover.Size.x,height,cover.Size.y);
                                        Mesh(group.Root.transform, HouseCornerCoverMesh.For(cover));
                                    }
                                }
                                next.Add(group);
                            }
                            foreach (var wall in layout.Spans.GroupBy(s => s.WallId))
                            {
                                var origin = wall.First().Start;
                                var position = new Vector3(origin.x, 0, origin.y);
                                string inputs = "local-collision-v1|" + HouseHeightWalls.Signature(plan, wall, data);
                                // Fixed cutters are in House space. Moving a wall relative
                                // to them changes its cut, even when the uncut shape matches.
                                if (data.BooleanInputs.Length > 0) inputs += ":origin:" + origin.ToString("R");
                                bool valid = true;
                                foreach (var span in wall)
                                {
                                    var d = span.Definition;
                                    inputs += JsonUtility.ToJson(new WallInputs { Start = HouseInputSignatures.LocalShapePoint(span.Start - origin),
                                        End = HouseInputSignatures.LocalShapePoint(span.End - origin),
                                        Height = d.Height, Thickness = d.Thickness, Opening = span.DoorwayId == null ? default : d.OpeningOutline });
                                    inputs += ":connection-depths:" + span.Depths.ToString("R");
                                    if (span.DoorwayId != null && !ValidOpening(d)) valid = false;
                                }
                                if (!valid) { output.Problem = "A door/window module needs a valid rectangular Opening Outline for collision."; continue; }
                                var group = RetainOrCreate(output, "wall:" + wall.Key, inputs + data.BooleanInputs, out bool changed);
                                if (changed)
                                {
                                    foreach (var span in wall) AddHeightSpan(group.Root.transform, plan, span, data);
                                    foreach (Transform child in group.Root.transform) child.localPosition -= position;
                                }
                                group.Root.transform.localPosition = position;
                                next.Add(group);
                            }
                            foreach (var wall in plan.Data.Walls.Where(w => w.IsPhysical && w.IsFence))
                            {
                                var a = plan.Data.GetPosition(wall.StartId); var b = plan.Data.GetPosition(wall.EndId);
                                var delta = b - a;
                                string inputs = HouseWallOutputBuilder.FenceInputs(wall, data) + ":" + delta.ToString("R")
                                    + ":" + a.ToString("R") + data.BooleanInputs;
                                var group = RetainOrCreate(output, "wall:" + wall.Id, inputs, out bool changed);
                                group.Root.transform.localPosition = new Vector3(a.x, 0, a.y);
                                if (changed)
                                {
                                    var parts = new List<TownRebuild.Part>();
                                    TownWallBuilder.BuildFence(wall.Id, wall.Fence, Vector3.zero, new Vector3(delta.x, 0, delta.y),
                                        null, null, true, parts);
                                    foreach (var part in parts)
                                    {
                                        var child = new GameObject(part.Key);
                                        child.transform.SetParent(group.Root.transform, false);
                                        child.transform.localPosition = part.Position;
                                        child.transform.localRotation = part.Rotation;
                                        child.transform.localScale = part.Scale;
                                        Mesh(child.transform, part.Mesh);
                                    }
                                }
                                next.Add(group);
                            }
                        }
                    }
                    else if (plan.Data.Walls.Count > 0)
                        output.Problem = "Enable 3D Walls and assign a valid wall kit to generate wall collision. Room floors still have collision.";
                }
                // Discard stale collision even when the replacement layout is invalid.
                foreach (var old in output.Groups) if (!next.Contains(old)) Remove(old);
                output.Groups = next;
                foreach (var group in next)
                {
                    group.ColliderCount = group.Root.GetComponentsInChildren<Collider>(true).Length;
                    foreach (var child in group.Root.GetComponentsInChildren<Transform>(true))
                    { child.gameObject.layer = plan.gameObject.layer; child.gameObject.hideFlags |= HideFlags.NotEditable; }
                }
                output.gameObject.SetActive(plan.Data.GenerateCollision && supported);
                output.gameObject.layer = plan.gameObject.layer;
                output.gameObject.hideFlags |= HideFlags.NotEditable;
                GeneratedMeshResources.RecordOwnedHierarchy(output.gameObject, HouseOutputUndo.AddComponent<GeneratedMeshOwnership>);
                output.InputSignature = signature;
                data.HasOutputChanges = true;
                EditorUtility.SetDirty(output);
                EditorSceneManager.MarkSceneDirty(plan.gameObject.scene);
            }
            catch
            {
                // Failed derived output is retried from source; discard only newly staged children.
                foreach (var child in output.transform.Cast<Transform>().Select(t => t.gameObject).ToArray())
                    if (!priorChildren.Contains(child)) HouseOutputUndo.Remove(child);
                throw;
            }
        }

        [System.Serializable] private struct CornerInputs
        { public Vector2 Along,Size; public Vector2[] Profile; public float Bottom,Top; }

        private static bool ValidOpening(WallModuleDefinition d)
        {
            var r = d.OpeningOutline;
            return float.IsFinite(r.x) && float.IsFinite(r.y) && float.IsFinite(r.width) && float.IsFinite(r.height)
                && r.width > 0 && r.height > 0 && r.xMin >= 0 && r.yMin >= 0 && r.xMax <= d.Width && r.yMax <= d.Height;
        }

        private static HouseCollisionOutput.Group RetainOrCreate(HouseCollisionOutput output, string key, string inputs, out bool changed)
        {
            string signature = Hash128.Compute(inputs).ToString();
            var old = output.Groups.Find(g => g.Key == key);
            changed = old == null || old.Root == null || old.Root.transform.parent != output.transform || old.Signature != signature
                || old.Root.GetComponentsInChildren<Collider>(true).Length != old.ColliderCount
                || old.Root.GetComponentsInChildren<MeshCollider>(true).Any(c => c.sharedMesh == null);
            if (!changed) return old;
            var root = new GameObject(key);
            root.transform.SetParent(output.transform, false);
            return new HouseCollisionOutput.Group { Key = key, Signature = signature, Root = root };
        }

        private static Quaternion Along(Vector2 direction) => Quaternion.LookRotation(new Vector3(-direction.y, 0, direction.x), Vector3.up);
        private static void Mesh(Transform parent, UnityEngine.Mesh source)
        {
            var building = parent.GetComponentInParent<HouseBuilding>();
            var mesh = source;
            if (building != null)
            {
                var toMesh = parent.worldToLocalMatrix * building.transform.localToWorldMatrix;
                mesh = HouseBooleanMesh.SubtractAll(mesh, building.Data.BooleanVolumes, toMesh);
            }
            if (mesh.vertexCount > 0) parent.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        private static void Box(Transform parent, Vector3 center, Vector3 size)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0) return;
            var building = parent.GetComponentInParent<HouseBuilding>();
            if (building != null && building.Data.BooleanVolumes.Count > 0)
            {
                var toMesh = parent.worldToLocalMatrix * building.transform.localToWorldMatrix;
                if (building.Data.BooleanVolumes.Any(volume => new Bounds(center, size).Intersects(HouseBooleanMesh.PrimitiveBounds(toMesh * volume.Matrix))))
                {
                    var source = HouseBooleanMesh.Box(center, size);
                    var mesh = source;
                    mesh = HouseBooleanMesh.SubtractAll(mesh, building.Data.BooleanVolumes, toMesh);
                    if (mesh != source)
                    {
                        if (mesh.vertexCount > 0) parent.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                        return;
                    }
                }
            }
            var box = parent.gameObject.AddComponent<BoxCollider>();
            box.center = center; box.size = size;
        }
        private static void AddHeightSpan(Transform parent, HousePlan plan, HouseWallLayout.Span span, HouseGenerationData data)
        {
            foreach (var section in data.HeightSections(plan, span))
            {
                var start = Vector2.Lerp(span.Start, span.End, section.From);
                var end = Vector2.Lerp(span.Start, span.End, section.To);
                var root = new GameObject("Height-fitted wall collision"); root.transform.SetParent(parent, false);
                root.transform.localPosition = new Vector3(start.x, 0, start.y);
                root.transform.localRotation = Along((end-start).normalized);
                float width = Vector2.Distance(start,end), height = section.CoverageTop, thickness = span.Depths.y - span.Depths.x;
                float centre = (span.Depths.x + span.Depths.y) / 2;
                if (span.DoorwayId == null)
                { Box(root.transform, new Vector3(width/2,height/2,centre), new Vector3(width,height,thickness)); continue; }
                var hole = span.Definition.OpeningOutline;
                Box(root.transform, new Vector3(hole.xMin/2,height/2,centre),new Vector3(hole.xMin,height,thickness));
                Box(root.transform,new Vector3((hole.xMax+width)/2,height/2,centre),new Vector3(width-hole.xMax,height,thickness));
                Box(root.transform,new Vector3(hole.center.x,hole.yMin/2,centre),new Vector3(hole.width,hole.yMin,thickness));
                Box(root.transform,new Vector3(hole.center.x,(hole.yMax+height)/2,centre),new Vector3(hole.width,height-hole.yMax,thickness));
            }
        }

        private static void Remove(HouseCollisionOutput.Group group)
        {
            if (group.Root == null) return;
            HouseOutputUndo.Remove(group.Root);
        }
    }
}
