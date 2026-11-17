using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class HouseSelection
    {
        internal static Bounds OpeningBounds(HouseWall wall, PlacedOpening opening, bool wholeSegment)
        {
            var fit = new FittedOpening(opening, wall.Family.FindSurround(opening.insert));
            bool frame = wholeSegment && fit.segment;
            float width = frame ? fit.width : opening.Size.x;
            float height = frame ? Mathf.Min(wall.Floor.height, fit.segment.size.y) : opening.Size.y;
            var center = new Vector3(frame ? fit.left + width / 2 : opening.distance,
                (frame ? 0 : fit.sill) + height / 2, 0);
            return new Bounds(center, new Vector3(width, height, wall.Thickness));
        }

        // Frame the same geometry the Scene view highlights. Some picks (holes, endpoints
        // and boundary-only walls) have no renderers of their own.
        internal static bool TryFrame(HousePick pick, HouseFloor floor, out Bounds bounds)
        {
            bounds = default;
            if (pick.cut != null && pick.cutOwner)
                return TryFrameCut(pick.cutOwner, pick.roofCut, pick.cut.points, out bounds);
            if (pick.room && pick.slab)
            {
                float face = pick.underside ? -floor.House.slabThickness : 0;
                return PolygonBounds(pick.slab.transform, pick.room.polygon, face, face, out bounds);
            }
            if (pick.corner != 0)
            {
                bounds = new Bounds(floor.World(floor.Position(pick.corner)), Vector3.one);
                return true;
            }
            if (pick.storey)
            {
                bounds = new Bounds(floor.transform.position, Vector3.one);
                if (floor.corners.Count == 0) return true;
                bounds = new Bounds(floor.World(floor.corners[0].point), Vector3.zero);
                var rise = floor.transform.TransformVector(Vector3.up * floor.height);
                foreach (var corner in floor.corners)
                {
                    var point = floor.World(corner.point);
                    bounds.Encapsulate(point);
                    bounds.Encapsulate(point + rise);
                }
                return true;
            }
            if (!pick.wall) return false;
            bool found = false;
            foreach (var renderer in HouseHighlight.SelectedRenderers(pick))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            if (found) return true;
            if (pick.OpeningId != null)
            {
                string id = pick.OpeningId;
                var placement = pick.wall.openings.Find(o => o.id == id);
                if (placement == null || !placement.insert || !pick.wall.Family) return false;
                var local = OpeningBounds(pick.wall, placement, pick.segmentId != null);
                bounds = HouseBooleanMesh.PrimitiveBounds(pick.wall.transform.localToWorldMatrix *
                    Matrix4x4.TRS(local.center, Quaternion.identity, local.size));
                return true;
            }
            bounds = new Bounds(floor.World(floor.Position(pick.wall.startCorner)), Vector3.zero);
            bounds.Encapsulate(floor.World(floor.Position(pick.wall.endCorner)));
            return true;
        }

        internal static bool TryFrameCut(HouseFloor owner, bool roof, Vector2[] points, out Bounds bounds)
        {
            float thickness = owner.House.slabThickness;
            float bottom = roof ? owner.height : -thickness;
            return PolygonBounds(owner.transform, points, bottom, bottom + thickness, out bounds);
        }

        static bool PolygonBounds(Transform space, Vector2[] points, float bottom, float top, out Bounds bounds)
        {
            bounds = default;
            if (points == null || points.Length == 0) return false;
            Vector3 World(Vector2 point) => space.TransformPoint(new Vector3(point.x, bottom, point.y));
            bounds = new Bounds(World(points[0]), Vector3.zero);
            var rise = space.TransformVector(Vector3.up * (top - bottom));
            foreach (var point in points)
            {
                var world = World(point);
                bounds.Encapsulate(world);
                bounds.Encapsulate(world + rise);
            }
            return true;
        }

    }
}
