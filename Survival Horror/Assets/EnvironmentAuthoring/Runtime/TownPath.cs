using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Authoring graph only. Elevations are real local metres, not floor indices.
    [Serializable]
    public sealed class TownPath
    {
        [Serializable] public sealed class Point
        {
            public string Id;
            public Vector3 Position;
            internal Point(Vector3 position) { Id = Guid.NewGuid().ToString("N"); Position = position; }
            internal Point Copy() => new Point(Position) { Id = Id };
        }
        [Serializable] public sealed class Span
        {
            public string Id, Start, End;
            public TownModule Model;
            public Material Material, BackMaterial;
            internal Span(string start, string end) { Id = Guid.NewGuid().ToString("N"); Start = start; End = end; }
            internal Span Copy() => new Span(Start, End) { Id = Id, Model = Model, Material = Material, BackMaterial = BackMaterial };
        }
        [SerializeField] private List<Point> points = new List<Point>();
        [SerializeField] private List<Span> spans = new List<Span>();
        private ReadOnlyModelList<Point> pointView;
        private ReadOnlyModelList<Span> spanView;
        public IReadOnlyList<Point> Points => ReadOnlyModelList<Point>.For(points, ref pointView);
        public IReadOnlyList<Span> Spans => ReadOnlyModelList<Span>.For(spans, ref spanView);
        public Point Find(string id) => points.FirstOrDefault(point => point.Id == id);
        public Span FindSpan(string id) => spans.FirstOrDefault(span => span.Id == id);
        internal TownPath Copy() => new TownPath { points = points.Select(point => point.Copy()).ToList(), spans = spans.Select(span => span.Copy()).ToList() };
        internal string AddPoint(Vector3 position)
        {
            var found = points.FirstOrDefault(point => Vector3.Distance(point.Position, position) < .001f);
            if (found != null) return found.Id;
            var point = new Point(position); points.Add(point); return point.Id;
        }
        internal Span Connect(string start, string end)
        {
            if (start == end || Find(start) == null || Find(end) == null) return null;
            var found = spans.FirstOrDefault(span => span.Start == start && span.End == end || span.Start == end && span.End == start);
            if (found != null) return found;
            var edge = new Span(start, end); spans.Add(edge); return edge;
        }
        internal void MergePoint(string from, string to)
        {
            if (from == to) return;
            foreach (var span in spans)
            { if (span.Start == from) span.Start = to; if (span.End == from) span.End = to; }
            points.RemoveAll(point => point.Id == from);
        }
        internal void Remove(IEnumerable<string> pointIds, IEnumerable<string> spanIds)
        {
            var removed = new HashSet<string>(pointIds); var edges = new HashSet<string>(spanIds);
            spans.RemoveAll(span => removed.Contains(span.Start) || removed.Contains(span.End) || edges.Contains(span.Id));
            points.RemoveAll(point => removed.Contains(point.Id) || !spans.Any(span => span.Start == point.Id || span.End == point.Id));
        }
        internal bool Dissolve(string id)
        {
            var connected = spans.Where(span => span.Start == id || span.End == id).ToArray();
            if (connected.Length != 2) return false;
            var a = connected[0]; var b = connected[1];
            bool sameDirection = a.End == b.Start || a.Start == b.End;
            if (a.Model != b.Model
                || a.Material != (sameDirection ? b.Material : b.BackMaterial)
                || a.BackMaterial != (sameDirection ? b.BackMaterial : b.Material)) return false;
            string start = a.Start == id ? a.End : a.Start, end = b.Start == id ? b.End : b.Start;
            if (start == end) return false;
            spans.Remove(b); points.RemoveAll(point => point.Id == id);
            // Keep the surviving span's direction, so its two face assignments
            // cannot exchange places when the first edge points into the junction.
            if (a.Start == id) a.Start = end; else a.End = end;
            return true;
        }
        // The original identity stays on the start-side child; styles follow every child.
        internal void Split(Span span, IEnumerable<(float t, string point)> cuts)
        {
            var ordered = cuts.Where(cut => cut.t > .0001f && cut.t < .9999f).OrderBy(cut => cut.t).ToArray();
            if (ordered.Length == 0) return;
            var original = span.Copy(); string previous = original.Start; float from = 0;
            var targets = ordered.Concat(new[] { (t: 1f, point: original.End) });
            bool first = true;
            foreach (var cut in targets)
            {
                if (cut.t - from < .0001f || previous == cut.point) continue;
                var child = first ? span : original.Copy();
                if (!first) { child.Id = Guid.NewGuid().ToString("N"); spans.Add(child); }
                child.Start = previous; child.End = cut.point;
                previous = cut.point; from = cut.t; first = false;
            }
        }
        internal bool Valid(out string reason)
        {
            reason = "Points and spans need distinct identities, finite positions and existing, separated endpoints.";
            if (points.Any(point => point == null || string.IsNullOrEmpty(point.Id) || !Finite(point.Position))
                || spans.Any(span => span == null || string.IsNullOrEmpty(span.Id))
                || points.Select(point => point.Id).Distinct().Count() != points.Count
                || spans.Select(span => span.Id).Distinct().Count() != spans.Count) return false;
            var pairs = new HashSet<string>();
            var nodes = points.ToDictionary(point => point.Id);
            foreach (var span in spans)
            {
                if (span.Start == null || span.End == null || !nodes.TryGetValue(span.Start, out var a) || !nodes.TryGetValue(span.End, out var b)
                    || new Vector2(a.Position.x - b.Position.x, a.Position.z - b.Position.z).sqrMagnitude < .000001f) return false;
                string key = string.CompareOrdinal(span.Start, span.End) < 0 ? span.Start + span.End : span.End + span.Start;
                if (!pairs.Add(key)) return false;
            }
            reason = ""; return true;
        }
        internal static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
