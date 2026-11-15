using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [Serializable]
    public sealed class GroundEdge
    {
        public int id;
        public Vector2 a, b;
    }

    [Serializable]
    public sealed class GroundFace
    {
        public int id;
        public int areaId;
        public Material areaMaterial, material;
        public Vector2[] polygon;
        public int[] triangles;
    }

    [Serializable]
    public sealed class GroundPlan
    {
        public int nextId = 1;
        public List<GroundEdge> edges = new List<GroundEdge>();
        public List<GroundFace> faces = new List<GroundFace>();
    }

    internal static class PavementGeometry
    {
        static Vector3 At(Vector2 point, float elevation) => new Vector3(point.x, elevation, point.y);

        internal static void Build(RoadMesh mesh, ProceduralSurface road)
        {
            var material = road.Pavement;
            float height = road.PavementElevation;
            foreach (var face in road.Areas.faces)
            {
                var hit = new RoadHit(RoadPart.Ground, face.id);
                for (int i = 0; i < face.triangles.Length; i += 3)
                {
                    var points = new Vector3[3];
                    for (int j = 0; j < 3; j++) points[j] = At(face.polygon[face.triangles[i+j]], height);
                    mesh.Face(face.material ? face.material : face.areaMaterial ? face.areaMaterial : material, hit, Vector3.up, points);
                }
            }
            var ports = new Dictionary<int, RoadGeometry.Arm>();
            foreach (var node in road.Paths.nodes)
            {
                var arms = RoadGeometry.Arms(road.Paths, node, road.StreetFamily, road.PathWidth);
                foreach (var arm in arms) ports.Add(arm.end.port, arm);
                if (arms.Count < 3) continue;
                for (int i = 0; i < arms.Count; i++)
                {
                    var arm = arms[i];
                    var next = arms[(i+1) % arms.Count];
                    var corner = RoadGeometry.CornerPoint(arm, next);
                    void Fan(RoadGeometry.Arm owner, Vector2 a, Vector2 b)
                    {
                        var span = owner.span;
                        mesh.Face(road.PathMaterial(span), new RoadHit(RoadPart.Path, span.id),
                            Vector3.up, new[]{ At(RoadGeometry.Flat(node.position),height), At(a,height), At(b,height) });
                    }
                    Fan(arm, arm.Right, arm.Left);
                    Fan(arm, arm.Left, corner);
                    Fan(next, corner, next.Right);
                }
            }
            foreach (var span in road.Paths.spans)
            {
                var a = ports[span.start.port];
                var b = ports[span.end.port];
                mesh.Face(road.PathMaterial(span), new RoadHit(RoadPart.Path, span.id),
                    Vector3.up, new[]{ At(a.Left,height), At(a.Right,height), At(b.Left,height), At(b.Right,height) });
            }
        }
    }
}
