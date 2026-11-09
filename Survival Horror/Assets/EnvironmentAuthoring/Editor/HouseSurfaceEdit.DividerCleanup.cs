using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseSurfaceEdit
    {
        // Reshaping may strand a formerly useful knife stroke outside every slab.
        // Keep logical coverage beneath holes and unfinished chains within it.
        private static bool ClipDividers(HouseStack.Boundary boundary, PathsD coverage,
            FloorPlan lower, FloorPlan upper, out bool changed, out string reason)
        {
            changed = false; reason = "";
            var nodes = new List<HouseStack.DividerNode>();
            var edges = new List<HouseStack.Divider>();
            var before = boundary.Copy();
            string Node(Vector2 point)
            {
                point = PlanarNoding.AtPrecision(point);
                var node = nodes.FirstOrDefault(n => n.Position == point);
                if (node != null) return node.Id;
                node = before.DividerNodes.FirstOrDefault(n => n.Position == point)?.Copy()
                    ?? new HouseStack.DividerNode(point);
                nodes.Add(node);
                return node.Id;
            }
            foreach (var edge in before.Dividers)
            {
                var a = before.DividerPosition(edge.StartId); var b = before.DividerPosition(edge.EndId);
                var portions = SurfacePartition.ClipLine((a, b), coverage)
                    .Where(p => Vector2.Distance(p.a, p.b) > OutlineGeometry.Tolerance).ToArray();
                changed |= portions.Length != 1 || portions[0].a != a || portions[0].b != b;
                for (int i = 0; i < portions.Length; i++)
                    edges.Add(new HouseStack.Divider(Node(portions[i].a), Node(portions[i].b), i == 0 ? edge.Id : null));
            }
            if (!changed) return true;
            if (!boundary.TryPublishDividerGraph(nodes, edges, out reason)) return false;
            // A clipping endpoint belongs to the coverage wall, never to the old
            // divider span that we have just shortened or removed.
            foreach (var node in boundary.DividerNodes.Where(n => !before.DividerNodes.Any(old => old.Id == n.Id)))
                if (!HouseDividerAttachments.AttachToWall(node, upper, boundary.UpperId))
                    HouseDividerAttachments.AttachToWall(node, lower, boundary.LowerId);
            HouseDividerAttachments.ReleaseRemovedDividerHosts(boundary);
            return true;
        }
    }
}
