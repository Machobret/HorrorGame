using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using LibTessDotNet;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace EnvironmentAuthoring.Editor
{
    // Removed floor areas remain editable; independent openings keep their own targets.
    internal static class RoomSelectionGeometry
    {
        internal sealed class RoomArea
        {
            internal PathsD Footprint, Floor, Holes;
            internal float Height;
        }

        internal static List<RoomArea> Areas(HousePlan plan, FloorPlan.Cell room)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = level == null ? null : building.Below(level);
            var cuts = boundary == null ? new PathsD() : SlabGeometry.Cuts(boundary);
            var result = new List<RoomArea>();
            void Add(PathsD shape, float height) => result.Add(new RoomArea { Footprint = shape,
                Floor = SlabGeometry.Subtract(shape, cuts), Holes = SlabGeometry.Intersect(shape, cuts), Height = height });
            if (level == null) Add(SlabGeometry.Shape(plan.Data.GetCellBoundary(room)), 0);
            else
            {
                var areas = SlabGeometry.FloorAreas(level).Where(area => area.RoomId == room.Id).ToArray();
                var removed = boundary.Regions.Where(segment => segment.CutAway).Select(segment => segment.Id).ToArray();
                cuts = SlabGeometry.Union(new PathsD(cuts.Concat(areas.Where(area => removed.Contains(area.RegionId)).SelectMany(area => area.Shape))));
                // Room feedback is a uniform overlay, independent of the
                // atomic segment owners retained beneath an opening.
                Add(SlabGeometry.Union(new PathsD(areas.SelectMany(area => area.Shape))), 0);
            }
            return result;
        }

        internal static List<RoomArea> PatchAreas(HousePlan plan, HouseSlabLayout.Patch patch)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            bool ceiling = patch.Lower?.Id == level?.Id && patch.Upper?.Id != level?.Id;
            float height = patch.WalkingHeight - building.FloorElevation(level);
            if (ceiling) height -= building.Data.SlabThickness;
            var floor = SlabGeometry.VisibleShape(patch);
            return new List<RoomArea> { new RoomArea { Footprint = patch.Shape,
                Floor = floor, Holes = SlabGeometry.Subtract(patch.Shape, floor), Height = height } };
        }

        // Omitted floors retain their active-level authoring footprint. Independent
        // holes still require their own cut target or an explicit logical query.
        internal static bool PickSegment(HousePlan plan, Ray ray, bool logical,
            out HouseSlabLayout.Patch patch, out float height, out float distance)
        {
            patch = null; height = 0; distance = float.PositiveInfinity;
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = level == null ? null : building.Below(level);
            if (boundary == null) return false;
            foreach (var candidate in HouseSceneQueries.BoundaryPatches(building, boundary.Id))
            {
                if (candidate.Upper?.Id != level.Id || candidate.UpperRoom == null) continue;
                float localHeight = candidate.WalkingHeight - building.FloorElevation(level);
                var plane = new Plane(plan.transform.up, plan.transform.TransformPoint(new Vector3(0, localHeight, 0)));
                if (!plane.Raycast(ray, out float hit) || hit < 0 || hit >= distance) continue;
                var point = plan.transform.InverseTransformPoint(ray.GetPoint(hit));
                var shape = logical || candidate.Floor.CutAway ? candidate.Shape : HouseSceneQueries.PatchAreas(plan, candidate)[0].Floor;
                if (!SlabGeometry.Contains(shape, new Vector2(point.x, point.z))) continue;
                patch = candidate; height = localHeight; distance = hit;
            }
            return patch != null;
        }

        internal static bool Pick(HousePlan plan, Ray ray, out FloorPlan.Cell room, out float height, out float distance)
        {
            room = null;
            height = 0;
            distance = float.PositiveInfinity;
            var level = HouseBuilding.For(plan)?.Find(plan);
            // Picking needs logical footprints only, so avoid cutting holes or
            // recomputing all level regions once per room on every SceneView event.
            var areas = level == null
                ? plan.Data.Cells.Select(c => new SlabGeometry.FloorArea { RoomId = c.Id, Shape = SlabGeometry.Shape(plan.Data.GetCellBoundary(c)) }).ToList()
                : HouseSceneQueries.FloorAreas(level);
            foreach (var area in areas)
            {
                var plane = new Plane(plan.transform.up, plan.transform.TransformPoint(Vector3.zero));
                if (!plane.Raycast(ray, out float hit) || hit < 0 || hit >= distance) continue;
                var point = plan.transform.InverseTransformPoint(ray.GetPoint(hit));
                // Test the logical footprint, including slab holes that have no collider.
                if (!SlabGeometry.Contains(area.Shape, new Vector2(point.x, point.z))) continue;
                room = plan.Data.Cells.First(c => c.Id == area.RoomId);
                height = 0;
                distance = hit;
            }
            return room != null;
        }

        internal static PathsD Stripes(PathsD holes)
        {
            if (holes.Count == 0) return new PathsD();
            const double diagonal = .7071067811865476;
            var points = holes.SelectMany(path => path).ToArray();
            double minU = points.Min(p => (p.x + p.y) * diagonal);
            double maxU = points.Max(p => (p.x + p.y) * diagonal);
            double minV = points.Min(p => (p.y - p.x) * diagonal) - .01;
            double maxV = points.Max(p => (p.y - p.x) * diagonal) + .01;
            var bands = new PathsD();
            // Geometric stripes leave genuinely empty gaps; there is no black background.
            // Anchor them to the House so the preview does not swim when the camera moves.
            const double period = .4, width = .16;
            for (double u = System.Math.Floor(minU / period) * period; u < maxU; u += period)
                bands.Add(new PathD { Point(u, minV), Point(u + width, minV), Point(u + width, maxV), Point(u, maxV) });
            return SlabGeometry.Intersect(holes, bands);

            PointD Point(double u, double v) => new PointD((u - v) * diagonal, (u + v) * diagonal);
        }

        internal static Mesh Overlay(PathsD shape, float height)
        {
            return Overlay(new[] { new RoomArea { Floor = shape, Height = height } }, false);
        }

        internal static Mesh Overlay(IEnumerable<RoomArea> areas, bool stripes)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var area in areas)
                Append(stripes ? Stripes(area.Holes) : area.Floor, area.Height, vertices, triangles);
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Room selection preview", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Append(PathsD shape, float height, List<Vector3> vertices, List<int> triangles)
        {
            if (shape.Count == 0) return;
            var tess = new Tess();
            foreach (var path in shape)
                tess.AddContour(path.Select(p => new ContourVertex { Position = new Vec3((float)p.x, (float)p.y, 0) }).ToArray());
            tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3, normal: new Vec3(0, 0, 1));
            int offset = vertices.Count;
            vertices.AddRange(tess.Vertices.Select(v => new Vector3(v.Position.X, height, v.Position.Y)));
            for (int i = 0; i < tess.ElementCount; i++)
            {
                int a = tess.Elements[3 * i], b = tess.Elements[3 * i + 1], c = tess.Elements[3 * i + 2];
                if (a >= 0 && b >= 0 && c >= 0) triangles.AddRange(new[] { offset + a, offset + c, offset + b });
            }
        }
    }

}
