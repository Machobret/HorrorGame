using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class RoomAssignment
    {
        const float Tolerance = .002f;
        const string Label = "Assign Room Contents";

        internal sealed class Result
        {
            internal int moved;
            internal readonly List<GameObject> review = new List<GameObject>();
        }

        static IEnumerable<Transform> Children(Transform parent) => parent.Cast<Transform>();

        // Never split a prefab or an authored group, even if only one child opted out.
        static bool Protected(Transform root) =>
            root.GetComponentInParent<ExcludeFromRoomAssignment>(true) ||
            root.GetComponentInChildren<ExcludeFromRoomAssignment>(true) ||
            root.GetComponentInChildren<FixtureAttachment>(true) || root.GetComponentInChildren<ProceduralSurface>(true) ||
            root.GetComponentInChildren<House>(true) ||
            root.GetComponentInChildren<HouseWall>(true) || root.GetComponentInChildren<HouseSlab>(true) ||
            root.GetComponentInChildren<CornerJoint>(true) || root.GetComponentInChildren<InsertAttachment>(true) ||
            root.GetComponentInChildren<HouseFloor>(true) || root.GetComponentInChildren<HouseRoom>(true) ||
            root.GetComponentInChildren<WallOutput>(true) || root.GetComponentInChildren<RoomContents>(true) ||
            (root.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.NotEditable)) != 0;

        static IEnumerable<Transform> Candidates(Transform node, House house)
        {
            if (node.GetComponent<ExcludeFromRoomAssignment>()) yield break;
            if (node.TryGetComponent<House>(out var owner) && owner != house) yield break;
            if (node.TryGetComponent<HouseFloor>(out var level) && level.House != house) yield break;
            if (node == house.transform || node.GetComponent<HouseFloor>() || node.GetComponent<HouseRoom>() ||
                node.GetComponent<RoomContents>() || house.transform.IsChildOf(node) || EnvironmentHierarchy.IsCategoryGroup(node))
            {
                foreach (var child in Children(node))
                    foreach (var item in Candidates(child, house)) yield return item;
            }
            else if (!Protected(node)) yield return node;
        }

        sealed class RoomVolume
        {
            internal readonly HouseRoom room;
            internal readonly HouseFloor floor;
            internal readonly Vector2[] footprintMetres;
            internal readonly float heightMetres;

            internal RoomVolume(HouseRoom room)
            {
                this.room = room;
                floor = room.GetComponentInParent<HouseFloor>();
                if (!floor || room.polygon == null || room.polygon.Length < 3) return;
                var t = floor.transform;
                var inverse = Quaternion.Inverse(t.rotation);
                footprintMetres = room.polygon.Select(p => {
                    var q = inverse * t.TransformVector(new Vector3(p.x,0,p.y));
                    return new Vector2(q.x,q.z);
                }).ToArray();
                heightMetres = Vector3.Dot(t.up,t.TransformVector(Vector3.up*floor.height));
            }
        }

        internal static Result Assign(House house)
        {
            var result = new Result();
            var roots = house.gameObject.scene.GetRootGameObjects();
            var rooms = roots.SelectMany(r => r.GetComponentsInChildren<HouseRoom>(true)).Select(r => new RoomVolume(r)).ToArray();
            var items = roots.SelectMany(r => Candidates(r.transform, house)).ToArray();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Label);
            try
            {
                foreach (var item in items) AssignOne(item, house, rooms, result, Label);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            return result;
        }

        // Called inside the wall edit's existing Undo group, after the new room polygons are published.
        internal static void Recheck(HouseFloor floor, Transform[] items, string action)
        {
            if (items.Length == 0) return;
            var rooms = floor.GetComponentsInChildren<HouseRoom>(true).Select(r => new RoomVolume(r)).ToArray();
            foreach (var item in items)
                if (item && !Protected(item)) AssignOne(item, floor.House, rooms, null, action);
        }

        static Transform Container(Transform owner, string name, string action)
        {
            var existing = Children(owner).FirstOrDefault(t => t.GetComponent<RoomContents>());
            if (existing) return existing;
            var group = new GameObject(name);
            group.transform.SetParent(owner, false);
            group.AddComponent<RoomContents>();
            Undo.RegisterCreatedObjectUndo(group, action);
            return group.transform;
        }

        static void AssignOne(Transform item, House house, RoomVolume[] rooms, Result result, string action)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            var shapes = new List<RoomTriggerShape>();
            bool global = false;
            foreach (var light in item.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional) global = true;
                else shapes.Add(RoomTriggerShape.Emitter(light.transform.position));
            }
            foreach (var audio in item.GetComponentsInChildren<AudioSource>(true))
            {
                if (audio.spatialBlend <= 0) global = true;
                else shapes.Add(RoomTriggerShape.Emitter(audio.transform.position));
            }
            foreach (var trigger in item.GetComponentsInChildren<Collider>(true))
            {
                if (!trigger.isTrigger) continue;
                var shape = RoomTriggerShape.From(trigger);
                if (shape != null) shapes.Add(shape);
                else global = true; // A group containing an unclassifiable trigger needs manual ownership.
            }
            if (renderers.Length == 0 && shapes.Count == 0) return;
            var projections = new Dictionary<HouseFloor, RoomTriggerShape.Projection[]>();
            var bounds = new Dictionary<HouseFloor, Bounds>();
            var owners = new List<HouseRoom>();
            bool touchesHouse = false;
            bool touchesOther = false;
            foreach (var volume in rooms)
            {
                if (volume.footprintMetres == null) continue;
                var room = volume.room;
                var floor = volume.floor;
                bool touches = false, fits = !global;
                if (renderers.Length > 0)
                {
                    if (!bounds.TryGetValue(floor, out var box))
                        bounds[floor] = box = FloorBounds(floor, renderers);
                    var min = box.min;
                    var max = box.max;
                    var rect = Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                    touches = min.y <= floor.height+Tolerance && max.y >= -Tolerance && Intersects(room.polygon,rect);
                    fits &= touches && min.y >= -Tolerance && max.y <= floor.height+Tolerance && Contains(room.polygon,rect);
                }
                if (shapes.Count > 0)
                {
                    if (!projections.TryGetValue(floor,out var projected))
                        projections[floor] = projected = shapes.Select(s=>s.Project(floor)).ToArray();
                    foreach (var shape in projected)
                    {
                        RoomTriggerShape.Compare(shape,volume.footprintMetres,volume.heightMetres,out bool overlap,out bool contained);
                        touches |= overlap;
                        fits &= contained;
                    }
                }
                if (!touches) continue;
                if (floor.House != house) { touchesOther = true; continue; }
                touchesHouse = true;
                if (fits) owners.Add(room);
            }
            var current = item.parent ? item.parent.GetComponentInParent<HouseFloor>() : null;
            if (owners.Count == 1 && !touchesOther)
            {
                var room = owners[0];
                var container = Container(room.transform, "Contents", action);
                if (item.parent != container)
                {
                    Undo.SetTransformParent(item, container, action);
                    if (result != null) result.moved++;
                }
            }
            else
            {
                if (!global && current && (result == null || item.GetComponentInParent<HouseRoom>()))
                {
                    var shared = Container(current.transform, "Shared contents", action);
                    if (item.parent != shared) Undo.SetTransformParent(item, shared, action);
                }
                if (touchesHouse || current) result?.review.Add(item.gameObject);
            }
        }

        static Bounds FloorBounds(HouseFloor floor, Renderer[] renderers)
        {
            Bounds total = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var local = r.localBounds;
                var matrix = floor.transform.worldToLocalMatrix * r.localToWorldMatrix;
                var size = local.size;
                var x = matrix.MultiplyVector(Vector3.right * size.x);
                var y = matrix.MultiplyVector(Vector3.up * size.y);
                var z = matrix.MultiplyVector(Vector3.forward * size.z);
                var box = new Bounds(matrix.MultiplyPoint3x4(local.center), new Vector3(
                    Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                    Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),
                    Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
                if (i == 0) total = box; else total.Encapsulate(box);
            }
            return total;
        }

        static bool Clip(float start, float step, float min, float max, ref float near, ref float far)
        {
            if (Mathf.Abs(step) < 1e-8f) return start >= min && start <= max;
            float a = (min-start)/step, b = (max-start)/step;
            near = Mathf.Max(near, Mathf.Min(a,b));
            far = Mathf.Min(far, Mathf.Max(a,b));
            return near <= far;
        }

        static bool EdgeInside(Vector2[] polygon, Rect rect)
        {
            for (int i=0, j=polygon.Length-1; i<polygon.Length; j=i++)
            {
                var a = polygon[j];
                var delta = polygon[i]-a;
                float near = 0, far = 1;
                if (Clip(a.x,delta.x,rect.xMin,rect.xMax,ref near,ref far) &&
                    Clip(a.y,delta.y,rect.yMin,rect.yMax,ref near,ref far)) return true;
            }
            return false;
        }

        static bool Intersects(Vector2[] polygon, Rect rect) =>
            RoomGeometry.Inside(rect.center, polygon) || EdgeInside(polygon, rect);

        static bool Contains(Vector2[] polygon, Rect rect)
        {
            // A concave room can cut through the middle even if every corner is inside.
            var inset = Vector2.Min(rect.size * .49f, Vector2.one * Tolerance);
            var inner = Rect.MinMaxRect(rect.xMin+inset.x,rect.yMin+inset.y,rect.xMax-inset.x,rect.yMax-inset.y);
            return RoomGeometry.Inside(inner.center, polygon) && !EdgeInside(polygon, inner);
        }
    }
}
