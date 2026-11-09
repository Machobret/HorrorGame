using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Logical identity stays independent of generated renderers and mesh colliders.
    internal struct HouseSceneHit
    {
        internal HousePlan Plan;
        internal HouseEditSelection.Kind Kind;
        internal string Id, BoundaryId;
        internal float Height, Along, Distance;
    }

    internal static partial class HouseSceneQueries
    {
        // Share read-only geometry only during one GUI event. The next event starts fresh,
        // so Undo, scripts, camera changes and accepted edits cannot leave a stale cache.
        internal sealed class EventGeometry : IDisposable
        {
            private readonly EventGeometry previous;
            internal readonly Dictionary<HouseStack.Level, List<SlabGeometry.FloorArea>> FloorFootprints = new Dictionary<HouseStack.Level, List<SlabGeometry.FloorArea>>();
            internal readonly Dictionary<(HouseBuilding, string), List<HouseSlabLayout.Patch>> Patches = new Dictionary<(HouseBuilding, string), List<HouseSlabLayout.Patch>>();
            internal readonly Dictionary<(HousePlan, string, string), List<RoomSelectionGeometry.RoomArea>> SurfaceAreas = new Dictionary<(HousePlan, string, string), List<RoomSelectionGeometry.RoomArea>>();
            internal readonly Dictionary<(HousePlan, HouseStack.Boundary, bool, HouseStack.Opening), List<RoomSelectionGeometry.RoomArea>> OpeningAreas = new Dictionary<(HousePlan, HouseStack.Boundary, bool, HouseStack.Opening), List<RoomSelectionGeometry.RoomArea>>();
            internal readonly Dictionary<HouseBuilding, HouseGenerationData> Buildings = new Dictionary<HouseBuilding, HouseGenerationData>();
            internal readonly Dictionary<(HousePlan, Vector2), Vector3> Nodes = new Dictionary<(HousePlan, Vector2), Vector3>();
            internal readonly Dictionary<(HousePlan, HouseStack.Boundary, bool, Vector2, bool), SlabHit?> SlabHits = new Dictionary<(HousePlan, HouseStack.Boundary, bool, Vector2, bool), SlabHit?>();
            internal readonly Dictionary<int, HouseSceneVisibility> Visibility = new Dictionary<int, HouseSceneVisibility>();
            internal readonly Dictionary<(HousePlan, Vector2), HouseSceneHit?> WallHits = new Dictionary<(HousePlan, Vector2), HouseSceneHit?>();
            internal readonly Dictionary<(HousePlan, string), bool> VisibleNodes = new Dictionary<(HousePlan, string), bool>();
            internal readonly Dictionary<HousePlan, HouseWallOutput> WallOutputs = new Dictionary<HousePlan, HouseWallOutput>();
            internal readonly Dictionary<HousePlan, HouseWallLayout.Result> WallLayouts = new Dictionary<HousePlan, HouseWallLayout.Result>();
            internal readonly Dictionary<(HousePlan, bool), PlacementGeometry> Placement = new Dictionary<(HousePlan, bool), PlacementGeometry>();
            internal readonly Dictionary<(HouseBuilding, string), IReadOnlyList<HouseSurfaceAreas.Area>> Areas = new Dictionary<(HouseBuilding, string), IReadOnlyList<HouseSurfaceAreas.Area>>();
            internal readonly Dictionary<HousePlan, Clipper2Lib.PathsD> FloorCoverage = new Dictionary<HousePlan, Clipper2Lib.PathsD>();
            internal readonly Dictionary<HousePlan, IReadOnlyList<FloorBand>> Bands = new Dictionary<HousePlan, IReadOnlyList<FloorBand>>();
            internal EventGeometry() { previous = geometry; geometry = this; }
            public void Dispose() { geometry = previous; }
            internal void Invalidate()
            {
                FloorFootprints.Clear(); Patches.Clear(); SurfaceAreas.Clear(); OpeningAreas.Clear();
                Buildings.Clear(); Nodes.Clear(); SlabHits.Clear(); Visibility.Clear();
                WallHits.Clear(); VisibleNodes.Clear(); WallOutputs.Clear(); WallLayouts.Clear(); Placement.Clear(); Areas.Clear(); FloorCoverage.Clear(); Bands.Clear();
                // A nested query scope must not restore its parent's old snapshot.
                previous?.Invalidate();
            }
        }
        private static EventGeometry geometry;
        static HouseSceneQueries()
        {
            HouseEditCommit.AuthoredChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            HouseRebuildCoordinator.OutputsRefreshed += _ => Invalidate();
        }
        private static void Invalidate() => geometry?.Invalidate();
        internal static EventGeometry BeginEvent() => new EventGeometry();

        private static HouseSceneVisibility Visibility(HousePlan plan)
        {
            var camera = SceneView.currentDrawingSceneView?.camera;
            if (geometry == null) return new HouseSceneVisibility(plan.gameObject.scene, camera);
            int scene = plan.gameObject.scene.handle;
            if (!geometry.Visibility.TryGetValue(scene, out var visibility))
                geometry.Visibility.Add(scene, visibility = new HouseSceneVisibility(plan.gameObject.scene, camera));
            return visibility;
        }

        internal static bool RaycastFloor(HousePlan plan, Ray ray, out Vector2 point)
        {
            point = default;
            if (!new Plane(plan.transform.up, plan.transform.position).Raycast(ray, out float fallback))
                return false;
            var p = plan.transform.InverseTransformPoint(ray.GetPoint(fallback));
            point = new Vector2(p.x, p.z);
            return true;
        }

        internal static bool VisibleWorld(HousePlan plan, Vector3 world, Bounds? support = null, Transform ownSurface = null)
        {
            var camera = SceneView.currentDrawingSceneView?.camera;
            if (camera == null || camera.WorldToViewportPoint(world).z <= 0) return false;
            var ray = HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(world));
            float distance = Vector3.Dot(world - ray.origin, ray.direction);
            return distance >= 0 && Visibility(plan).Visible(ray, distance, ownSurface, support);
        }

        internal static Vector3? VisibleSurfacePoint(HousePlan plan, Vector2 mouse)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            return Visibility(plan).TryHit(ray, out _, out float distance) ? ray.GetPoint(distance) : (Vector3?)null;
        }

        internal static bool VisibleNode(HousePlan plan, FloorPlan.Corner corner)
        {
            if (geometry == null) return VisibleNodeFresh(plan, corner);
            var key = (plan, corner.Id);
            if (!geometry.VisibleNodes.TryGetValue(key, out bool visible))
                geometry.VisibleNodes.Add(key, visible = VisibleNodeFresh(plan, corner));
            return visible;
        }

        internal static Vector2 WallDepths(HousePlan plan, FloorPlan.Wall wall, WallKit kit)
            => HouseWallLayout.WallDepths(plan.Data, wall, kit);

        internal static HouseWallLayout.Result WallLayout(HousePlan plan)
        {
            if (geometry != null && geometry.WallLayouts.TryGetValue(plan, out var cached)) return cached;
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            var result = kit == null ? new HouseWallLayout.Result() : HouseWallLayout.CalculateRequiredWalls(plan.Data, kit,
                includeDoorways: false);
            geometry?.WallLayouts.Add(plan, result);
            return result;
        }

        internal static HouseWallLayout.Result RenderedWallLayout(HousePlan plan)
        {
            var output = WallOutput(plan);
            // Picking follows applied placement. An immediate edit preview instead
            // requests WallLayout above, so pending source edits cannot reuse old placement.
            return output != null && output.PlacementLayout != null && output.PlacementInputs == output.InputSignature
                ? output.PlacementLayout : WallLayout(plan);
        }

        private static bool VisibleNodeFresh(HousePlan plan, FloorPlan.Corner corner)
        {
            float radius = .002f;
            var builder = plan.GetComponent<HouseWallBuilder>();
            var owned = new HashSet<string> { "corner:" + corner.Id };
            foreach (var wall in plan.Data.Walls)
                if (wall.StartId == corner.Id || wall.EndId == corner.Id)
                {
                    var depths = builder == null ? Vector2.zero : WallDepths(plan, wall, builder.Data);
                    radius = Mathf.Max(radius, Mathf.Abs(depths.x) + .002f, Mathf.Abs(depths.y) + .002f);
                    owned.Add("wall:" + wall.Id);
                }
            var world = NodeWorld(plan, corner.Position);
            var camera = SceneView.currentDrawingSceneView?.camera;
            if (camera == null || camera.WorldToViewportPoint(world).z <= 0) return false;
            var ray = HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(world));
            var groups = WallOutput(plan)?.Groups.Where(g => owned.Contains(g.Key))
                .Select(g => g.Root?.transform).ToArray();
            // The bottom-level handle belongs to its own junction's vertical footprint.
            // Top view may see its incident wall cap first; other walls/slabs still block.
            var support = new Bounds(world, new Vector3(radius * 2, float.MaxValue, radius * 2));
            float distance = Vector3.Dot(world - ray.origin, ray.direction);
            return distance >= 0 && Visibility(plan).Visible(ray, distance, handleSupport: support,
                handleSurfaces: groups ?? new Transform[0]);
        }

        internal static HouseWallOutput WallOutput(HousePlan plan)
        {
            if (geometry == null) return plan.GetComponent<HouseWallBuilder>()?.Output;
            // Output resolves ownership by traversing the generated hierarchy. Share that
            // lookup too, rather than traversing hundreds of modules for each visible dot.
            if (!geometry.WallOutputs.TryGetValue(plan, out var output))
                geometry.WallOutputs.Add(plan, output = plan.GetComponent<HouseWallBuilder>()?.Output);
            return output;
        }

        private static Transform WallSurface(HousePlan plan, FloorPlan.Wall wall) =>
            WallOutput(plan)?.Groups.FirstOrDefault(g => g.Key == "wall:" + wall.Id)?.Root?.transform;

        private static Vector3 ScreenSegmentPoint(Vector3 start, Vector3 end, Vector2 mouse)
        {
            var a = HandleUtility.WorldToGUIPoint(start); var b = HandleUtility.WorldToGUIPoint(end);
            float t = (b - a).sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(mouse - a, b - a) / (b - a).sqrMagnitude);
            var camera = SceneView.currentDrawingSceneView.camera;
            if (!camera.orthographic)
            {
                // Screen interpolation is perspective-divided. Recover the world fraction
                // before testing visibility, otherwise long angled edges test the wrong point.
                float za = camera.WorldToViewportPoint(start).z, zb = camera.WorldToViewportPoint(end).z;
                float denominator = (1 - t) * zb + t * za;
                if (denominator > 0) t = t * za / denominator;
            }
            return Vector3.Lerp(start, end, t);
        }

        internal static List<SlabGeometry.FloorArea> FloorAreas(HouseStack.Level level)
        {
            if (geometry == null || level == null) return SlabGeometry.FloorAreas(level);
            if (!geometry.FloorFootprints.TryGetValue(level, out var areas))
                geometry.FloorFootprints.Add(level, areas = SlabGeometry.FloorAreas(level));
            return areas;
        }

        internal static List<HouseSlabLayout.Patch> BoundaryPatches(HouseBuilding building, string boundary)
        {
            if (geometry == null) return Read();
            var key = (building, boundary);
            List<HouseSlabLayout.Patch> patches;
            if (!geometry.Patches.TryGetValue(key, out patches))
            {
                patches = Read();
                geometry.Patches[key] = patches;
            }
            return patches;
            List<HouseSlabLayout.Patch> Read()
            {
                return Generation(building).Patches.Where(patch => patch.Boundary.Id == boundary).ToList();
            }
        }

        internal static List<RoomSelectionGeometry.RoomArea> PatchAreas(HousePlan plan, HouseSlabLayout.Patch patch)
        {
            if (geometry == null) return RoomSelectionGeometry.PatchAreas(plan, patch);
            var key = (plan, patch.Boundary.Id, patch.Key);
            if (!geometry.SurfaceAreas.TryGetValue(key, out var areas))
                geometry.SurfaceAreas.Add(key, areas = RoomSelectionGeometry.PatchAreas(plan, patch));
            return areas;
        }

        internal static IReadOnlyList<HouseSurfaceAreas.Area> SurfaceAreas(HouseBuilding building, string boundaryId)
        {
            var key = (building, boundaryId);
            if (geometry != null && geometry.Areas.TryGetValue(key, out var cached)) return cached;
            var boundary = building.Boundaries.FirstOrDefault(b => b.Id == boundaryId);
            IReadOnlyList<HouseSurfaceAreas.Area> result = boundary == null ? new List<HouseSurfaceAreas.Area>()
                : HouseSurfaceAreas.FromPatches(boundary, BoundaryPatches(building, boundaryId));
            geometry?.Areas.Add(key, result);
            return result;
        }

        private static HouseGenerationData Generation(HousePlan plan) => Generation(HouseBuilding.For(plan));

        private static HouseGenerationData Generation(HouseBuilding building)
        {
            if (geometry == null || building == null) return HouseGenerationData.Create(building);
            if (!geometry.Buildings.TryGetValue(building, out var data))
                geometry.Buildings.Add(building, data = HouseGenerationData.Create(building));
            return data;
        }

        private static List<HouseHeightWalls.Section> HeightSections(HousePlan plan, Vector2 start, Vector2 end)
            => Generation(plan).HeightSections(plan, start, end);

        internal static List<Vector2> DrawingIntersections(HousePlan plan, IReadOnlyList<Vector2> proposed,
            bool closed, bool includeSlabOpenings, bool ceiling = false)
        {
            var result = new List<Vector2>();
            var edges = PlacementCandidates(plan, ceiling).Edges
                .Where(edge => includeSlabOpenings || edge.Kind != HouseSceneFeedback.PointKind.Opening)
                .ToArray();
            int count = closed ? proposed.Count : proposed.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var a = proposed[i]; var b = proposed[(i + 1) % proposed.Count];
                foreach (var edge in edges)
                {
                    var c = edge.Start; var d = edge.End;
                    if (OutlineGeometry.TryInteriorIntersection(a, b, c, d, out float fraction))
                        Add(Vector2.Lerp(c, d, fraction), edge);
                    // Include touching endpoints too. A polygon corner belongs to two edges,
                    // so deduplicate in plan metres rather than drawing two coincident dots.
                    if (OutlineGeometry.PointOnSegment(a, c, d)) Add(a, edge);
                    if (OutlineGeometry.PointOnSegment(b, c, d)) Add(b, edge);
                    if (OutlineGeometry.PointOnSegment(c, a, b)) Add(c, edge);
                    if (OutlineGeometry.PointOnSegment(d, a, b)) Add(d, edge);
                }
            }
            return result;
            void Add(Vector2 point, PlacementEdge edge)
            {
                if (!ceiling && edge.Kind == HouseSceneFeedback.PointKind.Divider && !OnEditableFloor(plan, point)) return;
                if (!result.Any(p => Vector2.Distance(p, point) <= OutlineGeometry.Tolerance)) result.Add(point);
            }
        }

        internal static int PendingSlabPoint(HousePlan plan, bool ceiling, IReadOnlyList<Vector2> points, Vector2 mouse)
        {
            int index = -1;
            float nearest = 14;
            for (int i = 0; i < points.Count; i++)
            {
                float pixels = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(SlabWorld(plan, ceiling, points[i])));
                if (pixels >= nearest || !VisibleWorld(plan, SlabWorld(plan, ceiling, points[i]))) continue;
                nearest = pixels;
                index = i;
            }
            return index;
        }

        internal static IEnumerable<string> OpeningsAtWallPoint(HousePlan plan, Vector2 point)
        {
            foreach (var wall in plan.Data.Walls)
            {
                var start = plan.Data.GetPosition(wall.StartId); var end = plan.Data.GetPosition(wall.EndId);
                if (!OutlineGeometry.PointOnSegment(point, start, end)) continue;
                float along = Vector2.Dot(point - start, (end - start).normalized);
                foreach (var opening in wall.Openings)
                    if (Mathf.Abs(along - opening.Distance) < opening.Width / 2 + OutlineGeometry.Tolerance)
                        yield return opening.Id;
            }
        }

        internal struct SlabHit
        {
            internal string OpeningId;
            internal int Vertex, Edge;
            internal float Height;
            internal float Distance;
        }

        internal static float SlabHeight(HousePlan plan, bool ceiling) =>
            ceiling ? HouseHeightQueries.Ceiling(plan) : 0;

        internal static Vector3 SlabWorld(HousePlan plan, bool ceiling, Vector2 point) =>
            plan.transform.TransformPoint(new Vector3(point.x, SlabHeight(plan, ceiling), point.y));

        internal struct SlabEdge
        {
            internal int Index;
            internal Vector3 Start, End;
            internal float Height;
        }

        internal static IEnumerable<SlabEdge> SlabEdges(HousePlan plan, bool ceiling, IReadOnlyList<Vector2> points, bool closed)
        {
            float height = SlabHeight(plan, ceiling);
            int count = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < count; i++)
                yield return new SlabEdge { Index = i, Start = plan.ToWorld(points[i], height),
                    End = plan.ToWorld(points[(i + 1) % points.Count], height), Height = height };
        }

        internal static HouseSceneTarget PickSlabTarget(HousePlan plan, Vector2 mouse)
        {
            var building = HouseBuilding.For(plan);
            if (building == null) return default;
            var level = building.Find(plan);
            float nearest = float.PositiveInfinity;
            HouseSceneTarget hit = default;
            // Both boundaries stay reachable regardless of the last slab tool operation.
            foreach (bool ceiling in new[] { false, true })
            {
                var boundary = ceiling ? building.Above(level) : building.Below(level);
                if (boundary == null) continue;
                var candidate = PickSlabOpening(plan, boundary, ceiling, mouse,
                    !ceiling || !HouseLevelView.CeilingHidden(plan));
                if (!candidate.HasValue) continue;
                // Editing points/edges take priority over surface interiors.
                float distance = candidate.Value.Edge >= 0 ? -1 : candidate.Value.Distance;
                if (distance >= nearest) continue;
                nearest = distance;
                hit = HouseSceneTarget.ForCut(candidate, ceiling);
            }
            return hit;
        }

        internal static SlabHit? PickSlabOpening(HousePlan plan, HouseStack.Boundary boundary, bool ceiling, Vector2 mouse, bool interiors = true)
        {
            if (geometry == null) return PickSlabOpeningFresh(plan, boundary, ceiling, mouse, interiors);
            var key = (plan, boundary, ceiling, mouse, interiors);
            if (!geometry.SlabHits.TryGetValue(key, out var hit))
                geometry.SlabHits.Add(key, hit = PickSlabOpeningFresh(plan, boundary, ceiling, mouse, interiors));
            return hit;
        }

        private static SlabHit? PickSlabOpeningFresh(HousePlan plan, HouseStack.Boundary boundary, bool ceiling, Vector2 mouse, bool interiors)
        {
            HouseStack.Opening hovered = null;
            SlabHit hit = default;
            float nearest = 9;
            foreach (var opening in boundary.Openings)
                foreach (var edge in SlabEdges(plan, ceiling, opening.Points, true))
                {
                    var camera = SceneView.currentDrawingSceneView.camera;
                    if (camera.WorldToViewportPoint(edge.Start).z <= 0 && camera.WorldToViewportPoint(edge.End).z <= 0) continue;
                    float distance = HandleUtility.DistanceToLine(edge.Start, edge.End);
                    if (distance >= nearest) continue;
                    var world = ScreenSegmentPoint(edge.Start, edge.End, mouse);
                    if (!VisibleWorld(plan, world)) continue;
                    nearest = distance;
                    hovered = opening;
                    var edgeRay = HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(world));
                    hit = new SlabHit { OpeningId = opening.Id, Vertex = -1, Edge = edge.Index, Height = edge.Height,
                        Distance = Vector3.Dot(world - edgeRay.origin, edgeRay.direction) };
                }
            if (hovered == null && interiors)
            {
                var ray = HandleUtility.GUIPointToWorldRay(mouse);
                float distance = float.PositiveInfinity;
                foreach (var opening in boundary.Openings)
                    foreach (var area in SlabOpeningAreas(plan, boundary, ceiling, opening))
                    {
                        var plane = new Plane(plan.transform.up, plan.transform.TransformPoint(new Vector3(0, area.Height, 0)));
                        if (!plane.Raycast(ray, out float next) || next >= distance) continue;
                        var local = plan.transform.InverseTransformPoint(ray.GetPoint(next));
                        if (!SlabGeometry.Contains(area.Holes, new Vector2(local.x, local.z))) continue;
                        if (!Visibility(plan).Visible(ray, next)) continue;
                        distance = next;
                        hovered = opening;
                        hit = new SlabHit { OpeningId = opening.Id, Vertex = -1, Edge = -1,
                            Height = area.Height, Distance = next };
                    }
                // Walls in front keep priority. Room handle priority is routed by the
                // controller before slab input, using the same scope as handle drawing.
                if (hovered != null)
                {
                    var other = PickWallItem(plan, mouse);
                    if (other.HasValue && other.Value.Kind != HouseEditSelection.Kind.Room
                        && other.Value.Distance <= hit.Distance + .002f) return null;
                }
            }
            if (hovered == null) return null;
            // Resolve a point only on the nearest edge's opening, preserving hit priority.
            for (int i = 0; i < hovered.Points.Count; i++)
                if (Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(SlabWorld(plan, ceiling, hovered.Points[i]))) < 10
                    && VisibleWorld(plan, SlabWorld(plan, ceiling, hovered.Points[i])))
                { hit.Vertex = i; hit.Height = SlabHeight(plan, ceiling); break; }
            return hit;
        }

        internal static List<RoomSelectionGeometry.RoomArea> SlabOpeningAreas(HousePlan plan,
            HouseStack.Boundary boundary, bool ceiling, HouseStack.Opening opening)
        {
            if (geometry == null) return SlabOpeningAreasFresh(plan, boundary, ceiling, opening);
            var key = (plan, boundary, ceiling, opening);
            if (!geometry.OpeningAreas.TryGetValue(key, out var areas))
                geometry.OpeningAreas.Add(key, areas = SlabOpeningAreasFresh(plan, boundary, ceiling, opening));
            return areas;
        }

        private static List<RoomSelectionGeometry.RoomArea> SlabOpeningAreasFresh(HousePlan plan,
            HouseStack.Boundary boundary, bool ceiling, HouseStack.Opening opening)
        {
            var building = HouseBuilding.For(plan);
            var result = new List<RoomSelectionGeometry.RoomArea>();
            if (building == null) return result;
            var level = building.Find(plan);
            var cut = SlabGeometry.Shape(opening.Points);
            // Reuse the actual slab partition: both room layouts and height regions divide
            // visible fragments, while one authored cut ID stays shared between levels.
            foreach (var patch in BoundaryPatches(building, boundary.Id))
            {
                if (ceiling ? patch.Lower != level || patch.LowerRoom == null
                    : patch.Upper != level || patch.UpperRoom == null) continue;
                var shape = SlabGeometry.Intersect(patch.Shape, cut);
                if (shape.Count == 0) continue;
                float height = patch.WalkingHeight - building.FloorElevation(level)
                    - (ceiling ? building.FloorThickness + building.CeilingThickness : 0);
                result.Add(new RoomSelectionGeometry.RoomArea { Holes = shape,
                    Floor = new Clipper2Lib.PathsD(), Footprint = shape, Height = height });
            }
            return result;
        }

        internal static bool PointOnSlab(HousePlan plan, bool ceiling, Vector2 mouse, out Vector2 point, out float height)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            height = ceiling ? building?.WallHeight(level) ?? 2.8f : 0;
            point = default;
            if (building == null) return PointOnEditPlane(plan, mouse, height, out point);
            var boundary = ceiling ? building.Above(level) : building.Below(level);
            if (boundary == null) return false;
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            float nearest = float.PositiveInfinity;
            foreach (var patch in BoundaryPatches(building, boundary.Id))
            {
                if (ceiling ? patch.Lower != level || patch.LowerRoom == null
                    : patch.Upper != level || patch.UpperRoom == null) continue;
                float nextHeight = patch.WalkingHeight - building.FloorElevation(level)
                    - (ceiling ? building.FloorThickness + building.CeilingThickness : 0);
                if (!PointOnEditPlane(plan, mouse, nextHeight, out var candidate)) continue;
                if (!SlabGeometry.Contains(patch.Shape, candidate)) continue;
                float distance = Vector3.Distance(ray.origin, plan.transform.TransformPoint(new Vector3(candidate.x, nextHeight, candidate.y)));
                if (distance >= nearest) continue;
                nearest = distance; height = nextHeight; point = candidate;
            }
            return float.IsFinite(nearest) || PointOnEditPlane(plan, mouse, height, out point);
        }

        internal static IEnumerable<string> NodesInRectangle(HousePlan plan, SceneView view, Rect rect, HouseEditSelection selection)
        {
            var nodes = selection.HandleNodes(plan);
            foreach (var corner in plan.Data.Corners)
            {
                if (!nodes.Contains(corner.Id)) continue;
                var world = NodeWorld(plan, corner.Position);
                if (view.camera.WorldToViewportPoint(world).z > 0
                    && rect.Contains(HandleUtility.WorldToGUIPoint(world))) yield return corner.Id;
            }
        }

        internal static IEnumerable<HouseEditSelection.Item> DividerNodesInRectangle(HousePlan plan, SceneView view,
            Rect rectangle, HouseEditSelection selection)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            if (level == null) yield break;
            foreach (var boundary in new[] { building.Below(level), building.Above(level) }.Where(b => b != null))
            {
                bool ceiling = boundary.LowerId == level.Id;
                var handles = selection.HandleDividerNodes(plan, boundary);
                foreach (var node in boundary.DividerNodes.Where(n => handles.Contains(n.Id)))
                {
                    if (!ceiling && !OnEditableFloor(plan, node.Position)) continue;
                    var world = SlabWorld(plan, ceiling, node.Position);
                    if (view.camera.WorldToViewportPoint(world).z > 0 && rectangle.Contains(HandleUtility.WorldToGUIPoint(world)))
                        yield return new HouseEditSelection.Item(HouseEditSelection.Kind.DividerNode, node.Id, boundary.Id);
                }
            }
        }

        internal static Vector3 NodeWorld(HousePlan plan, Vector2 point)
        {
            if (geometry == null) return plan.ToWorld(point, 0);
            var key = (plan, point);
            if (!geometry.Nodes.TryGetValue(key, out var world))
                geometry.Nodes.Add(key, world = plan.ToWorld(point, 0));
            return world;
        }
        internal static Vector2 Direction(HousePlan plan, FloorPlan.Wall wall) =>
            (plan.Data.GetPosition(wall.EndId) - plan.Data.GetPosition(wall.StartId)).normalized;

        internal static Vector2 Snap(HouseEditorSession session, Vector2 point) => !GridSnapping(session) ? point
            : new Vector2(Mathf.Round(point.x / session.GridSize) * session.GridSize,
                Mathf.Round(point.y / session.GridSize) * session.GridSize);

        internal static int FindWallUnderMouse(HousePlan plan, out float nearestDistance)
        {
            int index = -1;
            nearestDistance = 10;
            for (int i = 0; i < plan.Data.Walls.Count; i++)
            {
                var wall = plan.Data.Walls[i];
                float distance = HandleUtility.DistanceToLine(NodeWorld(plan, plan.Data.GetPosition(wall.StartId)),
                    NodeWorld(plan, plan.Data.GetPosition(wall.EndId)));
                if (distance >= nearestDistance) continue;
                var start = NodeWorld(plan, plan.Data.GetPosition(wall.StartId));
                var end = NodeWorld(plan, plan.Data.GetPosition(wall.EndId));
                if (!VisibleWorld(plan, ScreenSegmentPoint(start, end, Event.current.mousePosition), ownSurface: WallSurface(plan, wall))) continue;
                nearestDistance = distance;
                index = i;
            }
            return index;
        }

        internal static bool PointOnEditPlane(HousePlan plan, Vector2 mouse, float height, out Vector2 point)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            var plane = new Plane(plan.transform.up, plan.transform.position + plan.transform.up * height);
            point = default;
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = plan.transform.InverseTransformPoint(ray.GetPoint(distance));
            point = new Vector2(local.x, local.z);
            return true;
        }

        internal static HouseSceneHit? PickNode(HousePlan plan, Vector2 mouse, HouseEditSelection selection)
        {
            // These are room editing controls, drawn over geometry. Picking and
            // marquee must use the same scope without hiding them behind walls.
            HouseSceneHit? hit = null;
            float closest = 12;
            var nodes = selection.HandleNodes(plan);
            foreach (var corner in plan.Data.Corners)
            {
                if (!nodes.Contains(corner.Id)) continue;
                var world = NodeWorld(plan, corner.Position);
                if (SceneView.currentDrawingSceneView.camera.WorldToViewportPoint(world).z <= 0) continue;
                float distance = Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(world));
                if (distance >= closest) continue;
                closest = distance;
                hit = new HouseSceneHit { Plan = plan, Kind = HouseEditSelection.Kind.Node, Id = corner.Id,
                    Height = plan.transform.InverseTransformPoint(world).y,
                    Distance = Vector3.Dot(world - HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(world)).origin,
                        HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(world)).direction) };
            }
            return hit;
        }

        internal static HouseSceneHit? PickEditItem(HousePlan plan, Vector2 mouse, HouseEditSelection selection)
        {
            var hit = PickControl(plan, mouse, selection);
            if (hit.HasValue) return hit;
            return PickEditSurface(plan, mouse);
        }

        // The controller already checked controls before slab openings. Keep that
        // ordering without repeating the expensive control pass for ordinary surfaces.
        internal static HouseSceneHit? PickEditSurface(HousePlan plan, Vector2 mouse)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            HouseSceneHit? roomHit = null;
            if (RoomSelectionGeometry.PickSegment(plan, ray, false, out var surface, out float roomHeight, out float roomDistance)
                && Visibility(plan).Visible(ray, roomDistance))
                roomHit = new HouseSceneHit { Plan = plan, Kind = HouseEditSelection.Kind.SurfaceRegion, Id = surface.Floor.Id, BoundaryId = surface.Boundary.Id, Height = roomHeight, Distance = roomDistance };
            var wallHit = PickWallItem(plan, mouse);
            if (wallHit.HasValue) wallHit = WallTopTarget(plan, wallHit.Value, ray);
            return roomHit.HasValue && (!wallHit.HasValue || roomDistance < wallHit.Value.Distance - .002f) ? roomHit : wallHit;
        }

        // Select, Delete and opening placement share the same visible wall target. Surface
        // queries remain collider-free; a blocked face must never fall back to a hidden line.
        internal static HouseSceneHit? PickWallItem(HousePlan plan, Vector2 mouse)
        {
            if (geometry == null) return PickWallItemFresh(plan, mouse);
            var key = (plan, mouse);
            if (!geometry.WallHits.TryGetValue(key, out var hit))
                geometry.WallHits.Add(key, hit = PickWallItemFresh(plan, mouse));
            return hit;
        }

        private static HouseSceneHit? PickWallItemFresh(HousePlan plan, Vector2 mouse)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            var rendered = PickRenderedWall(plan, ray);
            int wallIndex;
            float along;
            var builder = plan.GetComponent<HouseWallBuilder>();
            var output = WallOutput(plan);
            bool visible = builder != null && builder.Data.ShowWalls && output != null && output.gameObject.activeInHierarchy;
            float wallDistance;
            bool face = OpeningPlacement.RaycastWall(plan, ray, out wallIndex, out along, out wallDistance) && visible;
            // Actual geometry owns protruding frames, caps and authored details. A
            // nearer logical opening still wins over a wall seen through its hole.
            if (rendered.HasValue && (!face || rendered.Value.Distance <= wallDistance + .002f)) return rendered;
            if (face)
            {
                if (!Visibility(plan).Visible(ray, wallDistance, WallSurface(plan, plan.Data.Walls[wallIndex]))) return null;
            }
            else
            {
                wallIndex = FindWallUnderMouse(plan, out _);
                along = 0;
                if (wallIndex < 0) return null;
                var wall = plan.Data.Walls[wallIndex];
                if (HouseSceneQueries.RaycastFloor(plan, ray, out var point))
                    along = Vector2.Dot(point - plan.Data.GetPosition(wall.StartId), Direction(plan, wall));
                var world = NodeWorld(plan, plan.Data.GetPosition(wall.StartId) + Direction(plan, wall) * along);
                wallDistance = Vector3.Dot(world - ray.origin, ray.direction);
            }
            var selected = plan.Data.Walls[wallIndex];
            float height = 0;
            var opening = selected.Openings.FirstOrDefault(o =>
            {
                if (Mathf.Abs(o.Distance - along) > o.Width / 2) return false;
                if (!face) return true; // In plan view the opening has its own footprint.
                var definition = OpeningPlacement.ResolveModule(plan, selected, builder, o.Kind, o.Width);
                if (definition == null) return false;
                float y = plan.transform.InverseTransformPoint(ray.GetPoint(wallDistance)).y;
                return y >= 0 && y <= definition.Height;
            });
            return new HouseSceneHit { Plan = plan, Kind = opening == null ? HouseEditSelection.Kind.Wall : HouseEditSelection.Kind.Opening,
                Id = opening == null ? selected.Id : opening.Id, Along = along,
                Height = height, Distance = wallDistance };
        }
    }
}
