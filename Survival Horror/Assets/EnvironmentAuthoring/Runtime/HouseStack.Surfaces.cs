using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class HouseStack
    {
        [Serializable]
        public sealed class DividerAttachment
        {
            [SerializeField] private string levelId, startId, endId, openingId;
            [SerializeField] private float fraction;
            // OpeningId identifies hole vertex IDs. Otherwise a null level means
            // divider nodes, and a level means wall corners. Splits retain the span.
            public string LevelId => levelId;
            public string StartId => startId;
            public string EndId => endId;
            public string OpeningId => openingId;
            public float Fraction => fraction;
            internal DividerAttachment(string levelId, string startId, string endId, float fraction, string openingId = null)
            { this.levelId = levelId; this.startId = startId; this.endId = endId; this.fraction = fraction; this.openingId = openingId; }
            internal DividerAttachment Copy() => new DividerAttachment(levelId, startId, endId, fraction, openingId);
            internal void Slide(float value) => fraction = value;
        }

        [Serializable]
        public sealed class DividerNode
        {
            [SerializeField] private string id;
            [SerializeField] private Vector2 position;
            [SerializeField] private DividerAttachment attachment;
            [SerializeField] private bool hasAttachment;
            public string Id => id;
            public Vector2 Position => position;
            // Unity's inline serialization can recreate an empty class for null.
            // The explicit flag preserves the distinction between free and attached.
            public DividerAttachment Attachment => hasAttachment ? attachment : null;
            internal DividerNode(Vector2 position, string id = null)
            { this.position = position; this.id = id ?? Guid.NewGuid().ToString("N"); }
            internal DividerNode Copy() => new DividerNode(position, id)
                { attachment = Attachment?.Copy(), hasAttachment = hasAttachment };
            internal void Move(Vector2 value) => position = value;
            internal void Attach(DividerAttachment value)
            { attachment = value; hasAttachment = value != null; }
        }

        // Dividers have no wall output. Intersections share authored node identities.
        [Serializable]
        public sealed class Divider
        {
            [SerializeField] private string id, startId, endId;
            public string Id => id;
            public string StartId => startId;
            public string EndId => endId;
            internal Divider(string startId, string endId, string id = null)
            { this.startId = startId; this.endId = endId; this.id = id ?? Guid.NewGuid().ToString("N"); }
            internal Divider Copy() => new Divider(startId, endId, id);
        }

        // One authored face region, independent of the rooms on the other side.
        // Geometry keys locate the region; its identity and finish survive reshaping.
        [Serializable]
        public sealed class SurfaceRegion
        {
            [SerializeField] private string id, faceKey;
            [SerializeField] private bool ceiling, overrideMaterial, cutAway;
            [SerializeField] private Material material;
            public string Id => id;
            public string FaceKey => faceKey;
            public bool Ceiling => ceiling;
            public bool OverrideMaterial => overrideMaterial;
            public Material Material => material;
            // Floor omission removes the slab volume; the independent ceiling finish remains.
            public bool CutAway => cutAway;
            public bool IsCustomized => overrideMaterial;
            internal SurfaceRegion(string faceKey, bool ceiling, string id = null)
            { this.faceKey = faceKey; this.ceiling = ceiling; this.id = id ?? Guid.NewGuid().ToString("N"); }
            internal SurfaceRegion Copy(string key, bool keepId = true) => new SurfaceRegion(key, ceiling, keepId ? id : null)
                { overrideMaterial = overrideMaterial, material = material, cutAway = cutAway };
            internal void SetCutAway(bool value)
            {
                if (ceiling && value) throw new InvalidOperationException("Omit the floor region to remove its slab volume.");
                cutAway = value;
            }
            internal void SetFinish(bool useOverride, Material value)
            { overrideMaterial = useOverride; material = useOverride ? value : null; }
            internal bool SamePayload(SurfaceRegion other) => ceiling == other.ceiling && cutAway == other.cutAway
                && overrideMaterial == other.overrideMaterial && material == other.material;
        }

        public sealed partial class Boundary
        {
            [SerializeField] private List<DividerNode> dividerNodes = new List<DividerNode>();
            [SerializeField] private List<Divider> dividers = new List<Divider>();
            [SerializeField] private List<SurfaceRegion> regions = new List<SurfaceRegion>();
            private ReadOnlyModelList<DividerNode> dividerNodeView;
            private ReadOnlyModelList<Divider> dividerView;
            private ReadOnlyModelList<SurfaceRegion> regionView;
            public IReadOnlyList<DividerNode> DividerNodes => ReadOnlyModelList<DividerNode>.For(dividerNodes, ref dividerNodeView);
            public IReadOnlyList<Divider> Dividers => ReadOnlyModelList<Divider>.For(dividers, ref dividerView);
            public IReadOnlyList<SurfaceRegion> Regions => ReadOnlyModelList<SurfaceRegion>.For(regions, ref regionView);
            public Vector2 DividerPosition(string id) => dividerNodes.First(node => node.Id == id).Position;

            internal bool TryValidateSurfaceData(out string reason)
            {
                reason = "Keep distinct hole vertex identities matching their contour.";
                if (openings.Any(opening => !opening.HasValidPointIds)) return false;
                reason = "Keep distinct slab surface region identities and geometry keys.";
                if (regions == null || regions.Any(segment => segment == null || segment.Ceiling && segment.CutAway || string.IsNullOrEmpty(segment.Id)
                    || string.IsNullOrEmpty(segment.FaceKey))
                    || regions.Select(segment => segment.Id).Distinct().Count() != regions.Count
                    || regions.Select(segment => segment.FaceKey).Distinct().Count() != regions.Count) return false;
                reason = "Keep distinct divider nodes and edges with valid endpoint references.";
                if (dividerNodes == null || dividers == null || dividerNodes.Any(node => node == null || string.IsNullOrEmpty(node.Id))
                    || dividers.Any(edge => edge == null || string.IsNullOrEmpty(edge.Id) || edge.StartId == edge.EndId)
                    || dividerNodes.Select(node => node.Id).Distinct().Count() != dividerNodes.Count
                    || dividers.Select(edge => edge.Id).Distinct().Count() != dividers.Count) return false;
                if (dividerNodes.Count == 0 && dividers.Count == 0) { reason = ""; return true; }
                var indices = dividerNodes.Select((node, index) => (node.Id, index)).ToDictionary(item => item.Id, item => item.index);
                if (dividers.Any(edge => edge.StartId == null || edge.EndId == null || !indices.ContainsKey(edge.StartId) || !indices.ContainsKey(edge.EndId))) return false;
                if (dividers.SelectMany(edge => new[] { edge.StartId, edge.EndId }).Distinct().Count() != dividerNodes.Count) return false;
                if (!OutlineGeometry.ValidatePoints(dividerNodes.Select(node => node.Position).ToArray(), out reason)) return false;
                reason = "Divider attachments need distinct host endpoints and a finite fraction between zero and one.";
                if (dividerNodes.Any(node => node.Attachment != null &&
                    (string.IsNullOrEmpty(node.Attachment.StartId) || string.IsNullOrEmpty(node.Attachment.EndId)
                    || node.Attachment.StartId == node.Attachment.EndId || !float.IsFinite(node.Attachment.Fraction)
                    || node.Attachment.Fraction < 0 || node.Attachment.Fraction > 1))) return false;
                reason = "Hole attachments must reference two existing vertices on the same opening.";
                if (dividerNodes.Any(node => node.Attachment != null && !string.IsNullOrEmpty(node.Attachment.OpeningId)
                    && (!string.IsNullOrEmpty(node.Attachment.LevelId) || !openings.Any(opening => opening.Id == node.Attachment.OpeningId
                        && opening.PointIndex(node.Attachment.StartId) >= 0 && opening.PointIndex(node.Attachment.EndId) >= 0)))) return false;
                var graph = PlanarNoding.Split(dividerNodes.Select(node => node.Position).ToArray(),
                    dividers.Select(edge => new Vector2Int(indices[edge.StartId], indices[edge.EndId])).ToArray());
                reason = "Divider intersections must use shared authored nodes. Reconcile the divider edit before publication.";
                if (graph.Vertices.Count != dividerNodes.Count || graph.Pieces.Count != dividers.Count
                    || graph.Pieces.Select(piece => (Math.Min(piece.Start, piece.End), Math.Max(piece.Start, piece.End))).Distinct().Count() != dividers.Count) return false;
                reason = "";
                return true;
            }

            internal bool TryAddDivider(Vector2 start, Vector2 end, out string reason)
            {
                if (!OutlineGeometry.ValidatePoints(new[] { start, end }, out reason)) return false;
                start = PlanarNoding.AtPrecision(start); end = PlanarNoding.AtPrecision(end);
                var nodes = dividerNodes.Select(node => node.Copy()).ToList();
                var edges = dividers.Select(edge => edge.Copy()).ToList();
                string AddPoint(Vector2 point)
                {
                    // Geometry commands never perform cursor snapping. A nearby but
                    // distinct node is a validation problem, not permission to move a perimeter.
                    var node = nodes.FirstOrDefault(item => PlanarNoding.AtPrecision(item.Position).Equals(point));
                    if (node == null) { node = new DividerNode(point); nodes.Add(node); }
                    return node.Id;
                }
                edges.Add(new Divider(AddPoint(start), AddPoint(end)));
                return TryPublishDividerGraph(nodes, edges, out reason);
            }

            internal bool TryInsertDividerNode(string dividerId, Vector2 point, out string addedId, out string reason)
            {
                addedId = null;
                reason = "Choose a point inside an existing divider edge.";
                var edge = dividers.FirstOrDefault(item => item.Id == dividerId);
                if (edge == null) return false;
                var start = dividerNodes.First(n => n.Id == edge.StartId).Position;
                var end = dividerNodes.First(n => n.Id == edge.EndId).Position;
                point = PlanarNoding.AtPrecision(point);
                if (!OutlineGeometry.PointOnSegment(point, start, end)
                    || Vector2.Distance(point, start) <= OutlineGeometry.Tolerance
                    || Vector2.Distance(point, end) <= OutlineGeometry.Tolerance) return false;
                var nodes = dividerNodes.Select(node => node.Copy()).ToList();
                var added = nodes.FirstOrDefault(node => node.Position == point);
                if (added == null) { added = new DividerNode(point); nodes.Add(added); }
                var edges = dividers.Where(item => item.Id != dividerId).Select(item => item.Copy()).ToList();
                edges.Add(new Divider(edge.StartId, added.Id, edge.Id));
                edges.Add(new Divider(added.Id, edge.EndId));
                if (!TryPublishDividerGraph(nodes, edges, out reason)) return false;
                addedId = added.Id;
                return true;
            }

            internal bool TryMoveDividerNodes(IReadOnlyDictionary<string, Vector2> positions, out string reason)
            {
                reason = "Choose existing divider nodes.";
                if (positions.Keys.Any(id => !dividerNodes.Any(node => node.Id == id))) return false;
                var nodes = dividerNodes.Select(node => node.Copy()).ToList();
                foreach (var node in nodes) if (positions.TryGetValue(node.Id, out var point)) node.Move(point);
                return TryPublishDividerGraph(nodes, dividers, out reason);
            }

            internal bool TryRemoveDividers(IEnumerable<string> ids, out string reason)
            {
                var selected = ids == null ? new HashSet<string>() : new HashSet<string>(ids);
                reason = "Choose existing dividers on this slab boundary.";
                if (selected.Count == 0 || selected.Any(id => !dividers.Any(edge => edge.Id == id))) return false;
                // Removing edges cannot introduce intersections. Keep surviving identities
                // and drop only nodes no longer used by any edge.
                var remaining = dividers.Where(edge => !selected.Contains(edge.Id)).Select(edge => edge.Copy()).ToList();
                var usedNodes = new HashSet<string>(remaining.SelectMany(edge => new[] { edge.StartId, edge.EndId }));
                dividerNodes = dividerNodes.Where(node => usedNodes.Contains(node.Id)).Select(node => node.Copy()).ToList();
                dividers = remaining;
                reason = "";
                return true;
            }

            internal bool TryPublishDividerGraph(List<DividerNode> nodes, IReadOnlyList<Divider> edges, out string reason)
            {
                reason = "Divider endpoints must refer to different nodes.";
                if (edges.Any(edge => edge.StartId == edge.EndId)) return false;
                if (!OutlineGeometry.ValidatePoints(nodes.Select(node => node.Position).ToArray(), out reason)) return false;
                var indices = nodes.Select((node, index) => (node.Id, index)).ToDictionary(item => item.Id, item => item.index);
                var graph = PlanarNoding.Split(nodes.Select(node => node.Position).ToArray(),
                    edges.Select(edge => new Vector2Int(indices[edge.StartId], indices[edge.EndId])).ToArray());
                if (!OutlineGeometry.ValidatePoints(graph.Vertices, out reason)) return false;
                var retained = new Dictionary<int, string>();
                for (int i = 0; i < nodes.Count; i++) retained.Add(graph.OriginalVertices[i], nodes[i].Id);
                var newNodes = graph.Vertices.Select((point, index) =>
                {
                    // Noding creates snap intersections, not host relationships.
                    var node = retained.TryGetValue(index, out var id) ? nodes.First(n => n.Id == id).Copy() : new DividerNode(point);
                    node.Move(point);
                    return node;
                }).ToList();
                var newEdges = new List<Divider>();
                var unique = new HashSet<(int, int)>(); var used = new HashSet<int>();
                foreach (var piece in graph.Pieces)
                    if (unique.Add((Math.Min(piece.Start, piece.End), Math.Max(piece.Start, piece.End))))
                        newEdges.Add(new Divider(newNodes[piece.Start].Id, newNodes[piece.End].Id,
                            used.Add(piece.Source) ? edges[piece.Source].Id : null));
                dividerNodes = newNodes.Where(node => newEdges.Any(edge => edge.StartId == node.Id || edge.EndId == node.Id)).ToList();
                dividers = newEdges;
                reason = "";
                return true;
            }
            internal void ReplaceDividerGraph(Boundary value)
            { dividerNodes = value.DividerNodes.Select(node => node.Copy()).ToList(); dividers = value.Dividers.Select(edge => edge.Copy()).ToList(); }
            internal void ReplaceRegions(IEnumerable<SurfaceRegion> value) => regions = value.Select(item => item.Copy(item.FaceKey)).ToList();
        }
    }
}
