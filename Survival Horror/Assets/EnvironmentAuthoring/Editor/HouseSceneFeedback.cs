using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseSceneFeedback : IDisposable
    {
        internal static readonly Color SelectionColor = Color.yellow;
        internal static readonly Color HoverColor = Color.white;
        internal enum PointKind { Wall, Divider, Opening, Intersection }
        private readonly Vector3[] pointQuad = new Vector3[4];
        private readonly Vector3[] pointCircle = new Vector3[12];
        private readonly Dictionary<string, RoomPreview> slabPreviews = new Dictionary<string, RoomPreview>();
        private readonly HouseComponentOutline outline = new HouseComponentOutline();
        internal void BeginOutlines() => outline.BeginBatch();
        internal void EndOutlines() => outline.EndBatch();

        internal void DrawTrimHighlight(HousePlan plan, FloorPlan.Wall wall, Color color)
        {
            if (!HouseTrimLayout.Eligible(plan.Data, wall)) return;
            var group = plan.GetComponent<HouseWallBuilder>()?.Output?.Groups.Find(g => g.Key == "trim:" + wall.Id);
            if (group?.Root != null) outline.Draw(group.Root.GetComponentsInChildren<MeshFilter>(), color);
            else outline.Draw(HouseSceneQueries.TrimBands(plan, wall)
                .Select(matrix => (HouseTrimMesh.Preview, plan.transform.localToWorldMatrix * matrix)), color);
        }

        internal void DrawSlabArea(HousePlan plan, HouseStack.Boundary boundary, bool ceiling, HouseStack.Opening opening)
        {
            if (Event.current.type != EventType.Repaint) return;
            string key = boundary.Id + ":" + opening.Id + ":" + ceiling;
            string signature = HouseInputSignatures.SlabOutputInputs(HouseBuilding.For(plan));
            if (!slabPreviews.TryGetValue(key, out var preview) || preview.Signature != signature)
            {
                if (preview != null) DestroyRoomPreview(preview);
                preview = new RoomPreview { Signature = signature,
                    Stripes = RoomSelectionGeometry.Overlay(HouseSceneQueries.SlabOpeningAreas(plan, boundary, ceiling, opening), true) };
                slabPreviews[key] = preview;
            }
            DrawMeshHighlight(preview.Stripes, plan.transform.localToWorldMatrix, Color.yellow, .7f);
        }

        internal static float SnapGridRadius(float spacing) => Mathf.Min(1.5f, spacing * 16);

        internal static float SnapGridOpacity(float distance, float radius)
        {
            float t = Mathf.Clamp01(1 - distance / radius);
            return .45f * t * t * (3 - 2 * t);
        }

        internal void DrawSnapGrid(HousePlan plan, Vector2 cursor, float height, float spacing)
        {
            if (Event.current.type != EventType.Repaint) return;
            float radius = SnapGridRadius(spacing);
            // One immediate line pass, bounded even at a tiny grid spacing. Alpha fades in
            // both directions; the grid stays local to the cursor and never creates output.
            EnsurePreviewMaterial();
            openingPreviewMaterial.SetColor("_Color", Color.white);
            if (!openingPreviewMaterial.SetPass(0)) return;
            GL.PushMatrix();
            GL.MultMatrix(plan.transform.localToWorldMatrix);
            GL.Begin(GL.LINES);
            for (int axis = 0; axis < 2; axis++)
            {
                float along = axis == 0 ? cursor.x : cursor.y;
                float across = axis == 0 ? cursor.y : cursor.x;
                int first = Mathf.CeilToInt((across - radius) / spacing);
                int last = Mathf.FloorToInt((across + radius) / spacing);
                for (int line = first; line <= last; line++)
                {
                    float fixedCoordinate = line * spacing;
                    for (int part = 0; part < 16; part++)
                        for (int end = 0; end < 2; end++)
                        {
                            float coordinate = along - radius + 2 * radius * (part + end) / 16;
                            var point = axis == 0 ? new Vector2(coordinate, fixedCoordinate) : new Vector2(fixedCoordinate, coordinate);
                            GL.Color(new Color(.65f, .82f, 1, SnapGridOpacity(Vector2.Distance(point, cursor), radius)));
                            GL.Vertex(new Vector3(point.x, height + .005f, point.y));
                        }
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        private Material openingPreviewMaterial;
        private void EnsurePreviewMaterial()
        {
            if (openingPreviewMaterial != null)
            {
                // Each draw starts with the normal overlay state. Floor hover changes it
                // for that draw only; it must not leak into the next grid/opening preview.
                openingPreviewMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
                return;
            }
            openingPreviewMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            openingPreviewMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            openingPreviewMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            openingPreviewMaterial.SetInt("_Cull", (int)CullMode.Off);
            openingPreviewMaterial.SetInt("_ZWrite", 0);
            openingPreviewMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }
        internal void DrawMeshHighlight(Mesh mesh, Matrix4x4 matrix, Color color, float opacity = .22f,
            CompareFunction depthTest = CompareFunction.Always)
        {
            if (Event.current.type != EventType.Repaint || mesh == null) return;
            EnsurePreviewMaterial();
            openingPreviewMaterial.SetInt("_ZTest", (int)depthTest);
            // Overlay the real shared mesh. No temporary GameObject or wall rebuild is needed.
            openingPreviewMaterial.SetColor("_Color", new Color(color.r, color.g, color.b, opacity));
            if (openingPreviewMaterial.SetPass(0)) Graphics.DrawMeshNow(mesh, matrix);
        }

        internal void DrawWallHighlight(HousePlan plan, FloorPlan.Wall wall, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var output = plan.GetComponent<HouseWallBuilder>()?.Output;
            var group = output?.Groups.Find(g => g.Key == "wall:" + wall.Id);
            var meshes = new List<MeshFilter>();
            if (group?.Root != null && group.Root.activeInHierarchy)
                foreach (var filter in group.Root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                    if (!renderer.forceRenderingOff) meshes.Add(filter);
                }
            // Outline the logical wall as one group, rather than each modular seam.
            if (meshes.Count > 0) outline.Draw(meshes, color);
            // The plan remains editable when 3D output is switched off or cannot be built.
            if (meshes.Count == 0)
                using (new Handles.DrawingScope(color))
                    Handles.DrawAAPolyLine(4, HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.StartId)), HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.EndId)));
        }

        internal void DrawOpeningHighlight(HousePlan plan, FloorPlan.Wall wall, FloorPlan.Opening opening, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            // Body and caps form one silhouette, including fitted/painted mesh overrides.
            outline.Draw(HouseSceneQueries.OpeningMeshes(plan, wall, opening), color);
        }

        internal void DrawOpeningPreview(WallModuleDefinition definition, Matrix4x4 matrix, Color color)
        {
            if (Event.current.type != EventType.Repaint || definition.Body == null) return;
            DrawMeshHighlight(definition.Body, matrix, color);
            var previousDepth = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            using (new Handles.DrawingScope(color, matrix))
            {
                var bounds = definition.Body.bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
                DrawOpeningOutline(definition);
            }
            Handles.zTest = previousDepth;
        }

        internal static void DrawOpeningOutline(WallModuleDefinition definition)
        {
            Rect opening = definition.OpeningOutline;
            if (opening.width <= 0 || opening.height <= 0) return;
            // Outline both faces so the clear opening reads from either side of the wall.
            var depths = definition != null ? definition.ConnectionDepths : Vector2.zero;
            foreach (float z in new[] { depths.x, depths.y })
                Handles.DrawAAPolyLine(4, new Vector3(opening.xMin, opening.yMin, z), new Vector3(opening.xMax, opening.yMin, z),
                    new Vector3(opening.xMax, opening.yMax, z), new Vector3(opening.xMin, opening.yMax, z), new Vector3(opening.xMin, opening.yMin, z));
        }

        internal void DrawSelection(HousePlan plan, HouseEditSelection selection, HouseSceneHit? hover,
            IReadOnlyDictionary<string, Vector2> movingNodes, IReadOnlyDictionary<string, float> movingOpenings,
            bool? valid)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawRoomSelection(plan, selection, hover);
            Color moveColor = valid == null ? HoverColor : valid.Value ? Color.green : Color.red;
            var depth = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            using (new Handles.DrawingScope(Handles.color))
            {
                Vector2 Position(string id) => movingNodes != null && movingNodes.TryGetValue(id, out var target) ? target : plan.Data.GetPosition(id);
                foreach (var wall in plan.Data.Walls)
                {
                    bool selected = selection.Contains(HouseEditSelection.Kind.Wall, wall.Id);
                    bool moved = movingNodes != null && (movingNodes.ContainsKey(wall.StartId) || movingNodes.ContainsKey(wall.EndId));
                    bool hovered = hover.HasValue && hover.Value.Kind == HouseEditSelection.Kind.Wall && hover.Value.Id == wall.Id;
                    if (selected || moved || hovered)
                    {
                        Handles.color = moved ? moveColor : selected ? SelectionColor : HoverColor;
                        if (!moved)
                        {
                            if (wall.IsPhysical) DrawWallHighlight(plan, wall, Handles.color);
                            var start = plan.Data.GetPosition(wall.StartId);
                            var end = plan.Data.GetPosition(wall.EndId);
                            float length = Vector2.Distance(start, end);
                            var center = (start + end) / 2;
                            float labelHeight = wall.IsPhysical ? HouseSceneQueries.WallTop(plan, wall, length / 2) : 0;
                            Handles.Label(plan.transform.TransformPoint(new Vector3(center.x, labelHeight, center.y)),
                                "L " + HouseSectionData.Metres(length) + (wall.IsPhysical ? " Â· H " + HouseSceneQueries.WallHeight(plan, wall).Text : ""));
                        }
                        else if (wall.IsPhysical)
                        {
                            // Keep the drag ghost separate from the unchanged generated output.
                            var builder = plan.GetComponent<HouseWallBuilder>();
                            var definition = HouseWallLayout.DefinitionFor(plan.Data, wall, builder?.Data);
                            var up = plan.transform.up * (builder != null && builder.Data.ShowWalls && definition != null ? definition.Height : 0);
                            var start = HouseSceneQueries.NodeWorld(plan, Position(wall.StartId));
                            var end = HouseSceneQueries.NodeWorld(plan, Position(wall.EndId));
                            Handles.DrawAAPolyLine(4, start, end, end + up, start + up, start);
                        }
                    }
                    foreach (var opening in wall.Openings)
                    {
                        bool picked = selection.Contains(HouseEditSelection.Kind.Opening, opening.Id);
                        bool over = hover.HasValue && hover.Value.Kind == HouseEditSelection.Kind.Opening && hover.Value.Id == opening.Id;
                        if (!picked && !over) continue;
                        if (movingOpenings != null && movingOpenings.TryGetValue(opening.Id, out float target))
                        {
                            // An uncommitted move still needs a placement ghost at its proposed location.
                            var definition = OpeningPlacement.ResolveModule(plan, wall, plan.GetComponent<HouseWallBuilder>(), opening.Kind, opening.Width);
                            if (definition != null) DrawOpeningPreview(definition,
                                OpeningPlacement.PreviewMatrix(plan, plan.Data.Walls.ToList().IndexOf(wall), target, opening.Width),
                                moveColor);
                        }
                        else DrawOpeningHighlight(plan, wall, opening, picked ? SelectionColor : HoverColor);
                    }
                }
                var handles = selection.HandleNodes(plan);
                foreach (var corner in plan.Data.Corners)
                {
                    bool selected = selection.Contains(HouseEditSelection.Kind.Node, corner.Id);
                    bool moved = movingNodes != null && movingNodes.ContainsKey(corner.Id);
                    bool hovered = hover.HasValue && hover.Value.Kind == HouseEditSelection.Kind.Node && hover.Value.Id == corner.Id;
                    if (!moved && !handles.Contains(corner.Id)) continue;
                    var color = moved ? moveColor : selected ? SelectionColor : hovered ? HoverColor : HoverColor;
                    var world = HouseSceneQueries.NodeWorld(plan, Position(corner.Id));
                    DrawPoint(world, color, plan.Data.Walls.Any(w => w.IsPhysical && (w.StartId == corner.Id || w.EndId == corner.Id))
                        ? PointKind.Wall : PointKind.Divider);
                }
            }
            Handles.zTest = depth;
        }

        internal void DrawNavigationDividers(HousePlan plan)
        {
            var building = HouseBuilding.For(plan);
            var boundary = building?.Below(building.Find(plan));
            if (boundary == null || boundary.Dividers.Count == 0) return;
            var previousDepth = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            foreach (var band in HouseSceneQueries.ExplicitDividerBands(plan, boundary))
                DrawConstructionSpan(plan, band.Start, band.End, new Color(.6f, .6f, .6f, .7f));
            Handles.zTest = previousDepth;
        }

        internal void DrawDividers(HousePlan plan, HouseEditSelection selection, HouseSceneHit? hover,
            string previewBoundary, IReadOnlyDictionary<string, Vector2> positions, bool? valid,
            IReadOnlyDictionary<string, Vector2> wallPositions = null, HouseEditPreview preview = null)
        {
            Color moveColor = valid == null ? HoverColor : valid.Value ? Color.green : Color.red;
            if (Event.current.type != EventType.Repaint) return;
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            if (level == null) return;
            var boundary = building.Below(level);
            if (boundary == null || !boundary.TryValidateSurfaceData(out _)) return;
            var previousDepth = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            {
                var handles = selection.HandleDividerNodes(plan, boundary);
                Vector2 Position(HouseStack.DividerNode node) => boundary.Id == previewBoundary && positions != null
                    && positions.TryGetValue(node.Id, out var point) ? point : node.Position;
                foreach (var band in HouseSceneQueries.FloorBands(plan))
                {
                    var divider = band.Kind == HouseEditSelection.Kind.Divider
                        ? boundary.Dividers.First(d => d.Id == band.Id) : null;
                    bool moved = divider != null && boundary.Id == previewBoundary && positions != null
                        && (positions.ContainsKey(divider.StartId) || positions.ContainsKey(divider.EndId));
                    if (band.Kind == HouseEditSelection.Kind.Wall && wallPositions != null)
                    {
                        var wall = plan.Data.Walls.First(w => w.Id == band.Id);
                        moved = wallPositions.ContainsKey(wall.StartId) || wallPositions.ContainsKey(wall.EndId);
                    }
                    if (moved) continue;
                    bool selected = selection.Contains(band.Kind, band.Id, band.BoundaryId);
                    bool hovered = hover?.Kind == band.Kind && hover.Value.Id == band.Id && hover.Value.BoundaryId == band.BoundaryId;
                    bool roomBoundary = band.Kind == HouseEditSelection.Kind.Wall;
                    DrawConstructionSpan(plan, band.Start, band.End,
                        selected ? SelectionColor : hovered ? HoverColor : roomBoundary ? RoomBoundaryColor : new Color(.6f, .6f, .6f, .7f),
                        roomBoundary ? RoomBoundaryWidth : 2);
                }
                foreach (var divider in boundary.Dividers.Where(d => boundary.Id == previewBoundary && positions != null
                    && (positions.ContainsKey(d.StartId) || positions.ContainsKey(d.EndId))))
                    foreach (var piece in HouseSceneQueries.ConstructionPieces(plan,
                        Position(boundary.DividerNodes.First(n => n.Id == divider.StartId)),
                        Position(boundary.DividerNodes.First(n => n.Id == divider.EndId)), preview))
                        DrawConstructionSpan(plan, piece.Start, piece.End, moveColor);
                foreach (var wall in plan.Data.Walls.Where(w => !w.IsPhysical && wallPositions != null
                    && (wallPositions.ContainsKey(w.StartId) || wallPositions.ContainsKey(w.EndId))))
                {
                    Vector2 Proposed(string id) => wallPositions.TryGetValue(id, out var point) ? point : plan.Data.GetPosition(id);
                    foreach (var piece in HouseSceneQueries.RoomBoundaryPieces(plan, Proposed(wall.StartId), Proposed(wall.EndId)))
                        DrawConstructionSpan(plan, piece.Start, piece.End, moveColor, RoomBoundaryWidth);
                }
                Handles.zTest = CompareFunction.Always;
                foreach (var node in boundary.DividerNodes.Where(n => handles.Contains(n.Id)))
                {
                    if (!HouseSceneQueries.DividerHandleOnFloor(plan, boundary, Position(node), preview)) continue;
                    bool moved = boundary.Id == previewBoundary && positions != null && positions.ContainsKey(node.Id);
                    bool selected = selection.Contains(HouseEditSelection.Kind.DividerNode, node.Id, boundary.Id);
                    var color = moved ? moveColor : selected ? SelectionColor : HoverColor;
                    DrawPoint(HouseSceneQueries.SlabWorld(plan, false, Position(node)), color, PointKind.Divider);
                }
            }
            Handles.zTest = previousDepth;
        }

        internal static readonly Color RoomBoundaryColor = new Color(.25f, .9f, .95f, .9f);
        internal const float RoomBoundaryWidth = 4;

        private void DrawConstructionSpan(HousePlan plan, Vector3 start, Vector3 end, Color color, float width = 2)
        {
            using (new Handles.DrawingScope(color))
                Handles.DrawAAPolyLine(width, start + plan.transform.up * .003f, end + plan.transform.up * .003f);
        }

        internal void DrawMarquee(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            Handles.BeginGUI();
            EditorGUI.DrawRect(rect, new Color(.2f, .7f, 1, .14f));
            GUI.Box(rect, GUIContent.none, "SelectionRect");
            Handles.EndGUI();
        }

        internal void DrawSlabLine(HousePlan plan, bool ceiling, IReadOnlyList<Vector2> points, Color color, bool closed)
        {
            if (Event.current.type != EventType.Repaint || points.Count < 2) return;
            using (new Handles.DrawingScope(color))
                foreach (var edge in HouseSceneQueries.SlabEdges(plan, ceiling, points, closed))
                    Handles.DrawAAPolyLine(3, edge.Start, edge.End);
        }

        // Glyphs face the drawing camera and keep their apparent size as it moves.
        // Callers still own picking, state colors and the current depth policy.
        private void DrawPoint(Vector3 world, Color color, PointKind kind)
        {
            if (Event.current.type != EventType.Repaint) return;
            var camera = Camera.current ?? SceneView.currentDrawingSceneView?.camera;
            if (camera == null) return;
            float radius = HandleUtility.GetHandleSize(world)
                * (kind == PointKind.Divider ? .045f : kind == PointKind.Intersection ? .0375f : .065f);
            var right = Handles.inverseMatrix.MultiplyVector(camera.transform.right) * radius;
            var up = Handles.inverseMatrix.MultiplyVector(camera.transform.up) * radius;
            var vertices = kind == PointKind.Wall ? pointCircle : pointQuad;
            if (kind == PointKind.Wall)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    float angle = 2 * Mathf.PI * i / vertices.Length;
                    vertices[i] = world + right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                }
            }
            else if (kind == PointKind.Divider)
            {
                vertices[0] = world + up; vertices[1] = world + right;
                vertices[2] = world - up; vertices[3] = world - right;
            }
            else
            {
                vertices[0] = world - right - up; vertices[1] = world - right + up;
                vertices[2] = world + right + up; vertices[3] = world + right - up;
            }
            using (new Handles.DrawingScope(color))
                Handles.DrawAAConvexPolygon(vertices);
        }

        internal void DrawSlabPoints(HousePlan plan, bool ceiling, IEnumerable<Vector2> points, Color color,
            PointKind kind = PointKind.Opening)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var point in points) DrawPoint(HouseSceneQueries.SlabWorld(plan, ceiling, point), color, kind);
        }

        internal void DrawPlacementGuides(HousePlan plan, Vector2 raw, bool ceiling = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            var shown = new HashSet<Vector2>();
            var depth = Handles.zTest;
            // Visibility is resolved by the same queries used for snapping. Rendering
            // through a node's own wall cap must not make unrelated occluded nodes snap.
            Handles.zTest = CompareFunction.Always;
            foreach (var point in HouseSceneQueries.PlacementCandidates(plan, ceiling).Points
                .Concat(HouseSceneQueries.NearbyIntersections(plan, raw, ceiling)))
            {
                float opacity = HouseSceneQueries.PlacementGuideOpacity(plan, raw, point.Position);
                if (opacity <= 0 || !HouseSceneQueries.PlacementPointVisible(plan, ceiling, point) || !shown.Add(point.Position)) continue;
                var color = point.Kind == PointKind.Intersection ? new Color(.55f, .55f, .55f, opacity) : new Color(1, 1, 1, opacity);
                DrawPoint(HouseSceneQueries.SlabWorld(plan, ceiling, point.Position), color, point.Kind);
            }
            Handles.zTest = depth;
        }

        internal void DrawIntersectionPoints(HousePlan plan, IEnumerable<Vector2> points, bool? ceiling = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            var depth = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            foreach (var point in points)
            {
                var world = ceiling.HasValue ? HouseSceneQueries.SlabWorld(plan, ceiling.Value, point) : HouseSceneQueries.NodeWorld(plan, point);
                // Derived markers are quieter and smaller than authored editing controls.
                DrawPoint(world, new Color(.55f, .55f, .55f), PointKind.Intersection);
            }
            Handles.zTest = depth;
        }

        internal void DrawRemovedOpenings(HousePlan plan, IEnumerable<string> ids)
        {
            if (Event.current.type != EventType.Repaint) return;
            var affected = new HashSet<string>(ids);
            foreach (var wall in plan.Data.Walls)
                foreach (var opening in wall.Openings.Where(o => affected.Contains(o.Id)))
                {
                    var definition = OpeningPlacement.ResolveModule(plan, wall, plan.GetComponent<HouseWallBuilder>(), opening.Kind, opening.Width);
                    if (definition != null)
                        DrawOpeningHighlight(plan, wall, opening, new Color(1, .5f, .05f));
                    else
                        using (new Handles.DrawingScope(new Color(1, .5f, .05f)))
                        {
                            var center = plan.Data.GetPosition(wall.StartId) + HouseSceneQueries.Direction(plan, wall) * opening.Distance;
                            Handles.DrawAAPolyLine(6, HouseSceneQueries.NodeWorld(plan, center - HouseSceneQueries.Direction(plan, wall) * opening.Width / 2),
                                HouseSceneQueries.NodeWorld(plan, center + HouseSceneQueries.Direction(plan, wall) * opening.Width / 2));
                        }
                }
        }

        private sealed class RoomPreview
        {
            public string Signature;
            public Mesh Floor, Stripes;
        }
        private readonly Dictionary<string, RoomPreview> roomPreviews = new Dictionary<string, RoomPreview>();

        private readonly Dictionary<(string Boundary, string Region), RoomPreview> regionPreviews = new Dictionary<(string, string), RoomPreview>();

        internal void DrawRoomSelection(HousePlan plan, HouseEditSelection selection, HouseSceneHit? hover)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawRegionSelection(plan, selection, hover);
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = level == null ? null : building.Below(level);
            string sharedRoomInputs = null;
            foreach (var room in plan.Data.Cells)
            {
                bool selected = selection.Contains(HouseEditSelection.Kind.Room, room.Id);
                bool hovered = hover.HasValue && hover.Value.Kind == HouseEditSelection.Kind.Room && hover.Value.Id == room.Id;
                if (!selected && !hovered) continue;
                // All room previews share these inputs during this repaint.
                sharedRoomInputs ??= "|" + (boundary == null ? "" : JsonUtility.ToJson(boundary))
                    + "|" + (level == null ? "" : JsonUtility.ToJson(level));
                string signature = string.Join("/", plan.Data.GetCellBoundary(room).Select(p => p.ToString("R")))
                    + sharedRoomInputs;
                if (!roomPreviews.TryGetValue(room.Id, out var preview) || preview.Signature != signature)
                {
                    if (preview != null) DestroyRoomPreview(preview);
                    var areas = RoomSelectionGeometry.Areas(plan, room);
                    // Room scope includes all its floor segments; hole feedback stays separate.
                    var omitted = boundary == null ? Enumerable.Empty<RoomSelectionGeometry.RoomArea>()
                        : HouseSceneQueries.BoundaryPatches(building, boundary.Id)
                            .Where(patch => patch.UpperRoom == room.Id && patch.Floor.CutAway)
                            .SelectMany(patch => HouseSceneQueries.PatchAreas(plan, patch));
                    preview = new RoomPreview { Signature = signature, Floor = RoomSelectionGeometry.Overlay(areas, false),
                        Stripes = RoomSelectionGeometry.Overlay(omitted, true) };
                    roomPreviews[room.Id] = preview;
                }
                DrawMeshHighlight(preview.Floor, plan.transform.localToWorldMatrix, selected ? SelectionColor : HoverColor,
                    depthTest: selected ? CompareFunction.Always : CompareFunction.LessEqual);
                DrawMeshHighlight(preview.Stripes, plan.transform.localToWorldMatrix, selected ? SelectionColor : HoverColor,
                    depthTest: selected ? CompareFunction.Always : CompareFunction.LessEqual);
                // Independent openings keep their own hover and selection feedback.
            }
            foreach (string id in roomPreviews.Keys.Where(id => !plan.Data.Cells.Any(room => room.Id == id)).ToArray())
            {
                DestroyRoomPreview(roomPreviews[id]);
                roomPreviews.Remove(id);
            }
        }

        private void DrawRegionSelection(HousePlan plan, HouseEditSelection selection, HouseSceneHit? hover)
        {
            var building = HouseBuilding.For(plan);
            if (building == null) return;
            var targets = selection.Items.Where(i => i.Type == HouseEditSelection.Kind.SurfaceRegion)
                .Select(i => (Boundary: i.BoundaryId, Region: i.Id)).ToList();
            if (hover.HasValue && hover.Value.Kind == HouseEditSelection.Kind.SurfaceRegion)
            {
                var area = HouseSceneQueries.SurfaceAreas(building, hover.Value.BoundaryId)
                    .FirstOrDefault(a => a.RegionId == hover.Value.Id);
                if (area != null) targets.Add((hover.Value.BoundaryId, area.RegionId));
            }
            // A repaint cannot change authored slabs between these targets.
            string signature = targets.Count == 0 ? null : HouseInputSignatures.SlabOutputInputs(building);
            foreach (var target in targets.Distinct())
            {
                var patches = HouseSceneQueries.BoundaryPatches(building, target.Boundary).Where(p => p.Floor.Id == target.Region).ToArray();
                if (patches.Length == 0) continue;
                if (!regionPreviews.TryGetValue(target, out var preview) || preview.Signature != signature)
                {
                    if (preview != null) DestroyRoomPreview(preview);
                    var areas = patches.SelectMany(patch => HouseSceneQueries.PatchAreas(plan, patch)).ToArray();
                    preview = new RoomPreview { Signature = signature,
                        Floor = RoomSelectionGeometry.Overlay(areas, false),
                        Stripes = patches[0].Floor.CutAway ? RoomSelectionGeometry.Overlay(areas, true) : null };
                    regionPreviews[target] = preview;
                }
                bool selected = selection.Contains(HouseEditSelection.Kind.SurfaceRegion, target.Region, target.Boundary);
                DrawMeshHighlight(preview.Floor, plan.transform.localToWorldMatrix, selected ? SelectionColor : HoverColor,
                    depthTest: selected ? CompareFunction.Always : CompareFunction.LessEqual);
                DrawMeshHighlight(preview.Stripes, plan.transform.localToWorldMatrix, selected ? SelectionColor : HoverColor,
                    depthTest: selected ? CompareFunction.Always : CompareFunction.LessEqual);
            }
            // Keep only displayed targets; passing across many segments must not retain
            // every temporary hover mesh until the context closes.
            foreach (var key in regionPreviews.Keys.Where(k => !targets.Contains(k)).ToArray())
            { DestroyRoomPreview(regionPreviews[key]); regionPreviews.Remove(key); }
        }

        private static void DestroyRoomPreview(RoomPreview preview)
        {
            if (preview.Floor != null) UnityEngine.Object.DestroyImmediate(preview.Floor);
            if (preview.Stripes != null) UnityEngine.Object.DestroyImmediate(preview.Stripes);
        }

        private void DisposeRoomPreviews()
        {
            foreach (var preview in roomPreviews.Values) DestroyRoomPreview(preview);
            roomPreviews.Clear();
            foreach (var preview in regionPreviews.Values) DestroyRoomPreview(preview);
            regionPreviews.Clear();
        }

        public void Dispose()
        {
            outline.Dispose();
            foreach (var preview in slabPreviews.Values) DestroyRoomPreview(preview);
            slabPreviews.Clear();
            DisposeRoomPreviews();
            if (openingPreviewMaterial != null) UnityEngine.Object.DestroyImmediate(openingPreviewMaterial);
            openingPreviewMaterial = null;
        }
    }
}
