using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Traces rings in an already split planar graph. Callers decide which rings are
    // rooms, holes or surface segments; this walk has no room or height policy.
    internal static class PlanarFaceWalk
    {
        internal static IEnumerable<List<int>> Trace(IReadOnlyList<Vector2> vertices, IReadOnlyList<Vector2Int> edges)
        {
            int From(int edge) => edge % 2 == 0 ? edges[edge / 2].x : edges[edge / 2].y;
            int To(int edge) => From(edge ^ 1);
            var outgoing = new Dictionary<int, List<int>>();
            var angles = new double[edges.Count * 2];
            for (int edge = 0; edge < angles.Length; edge++)
            {
                if (!outgoing.TryGetValue(From(edge), out var choices))
                    outgoing.Add(From(edge), choices = new List<int>());
                choices.Add(edge);
                var direction = vertices[To(edge)] - vertices[From(edge)];
                angles[edge] = Math.Atan2(direction.y, direction.x);
            }
            foreach (var choices in outgoing.Values)
                choices.Sort((a, b) => angles[a].CompareTo(angles[b]));
            var visited = new bool[angles.Length];
            for (int first = 0; first < angles.Length; first++)
            {
                if (visited[first]) continue;
                var walk = new List<int>();
                int edge = first;
                do
                {
                    visited[edge] = true;
                    walk.Add(edge);
                    var choices = outgoing[To(edge)];
                    int reverse = choices.IndexOf(edge ^ 1);
                    // Take the clockwise neighbour of the reversed direction.
                    // The traced face stays on the left at T and X junctions.
                    edge = choices[(reverse + choices.Count - 1) % choices.Count];
                } while (edge != first);
                RemoveSpurs(walk);
                if (walk.Count > 0) yield return walk;
            }
        }

        private static void RemoveSpurs(List<int> walk)
        {
            // An incomplete branch walks out and back along the same edge. It
            // remains in the authored graph but does not divide the surface.
            bool removed = true;
            while (removed && walk.Count >= 2)
            {
                removed = false;
                for (int i = 0; i < walk.Count; i++)
                {
                    int next = (i + 1) % walk.Count;
                    if ((walk[i] ^ 1) != walk[next]) continue;
                    walk.RemoveAt(Math.Max(i, next));
                    walk.RemoveAt(Math.Min(i, next));
                    removed = true;
                    break;
                }
            }
        }
    }
}
