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
            root.GetComponentInChildren<ScatterCell>(true) || root.GetComponentInChildren<MeshDecal>(true) || root.GetComponentInChildren<House>(true) ||
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
            if (HouseOwnership.IsArchitecture(node.gameObject) || HouseOwnership.IsAttachmentPart(node.gameObject) ||
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

        // Assignment and editor warnings use the same bounds, emitters and trigger shapes.
        sealed class ItemGeometry
        {
            readonly Renderer[] renderers;
            readonly List<RoomTriggerShape> shapes = new List<RoomTriggerShape>();
            internal readonly bool global;
            internal bool Measurable => renderers.Length > 0 || shapes.Count > 0;

            HouseFloor projectedFloor;
            Bounds box;
            RoomTriggerShape.Projection[] projected;

            internal ItemGeometry(Transform item)
            {
                renderers = item.GetComponentsInChildren<Renderer>(true);
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
            }

            internal Bounds WorldBounds()
            {
                var result = renderers.Length > 0 ? renderers[0].bounds : shapes[0].WorldBounds();
                foreach (var renderer in renderers) result.Encapsulate(renderer.bounds);
                foreach (var shape in shapes) result.Encapsulate(shape.WorldBounds());
                return result;
            }

            internal void Compare(RoomVolume volume, out bool touches, out bool fits)
            {
                var room = volume.room;
                var floor = volume.floor;
                touches = false;
                fits = true;
                if (projectedFloor != floor)
                {
                    projectedFloor = floor;
                    if (renderers.Length > 0) box = FloorBounds(floor, renderers);
                    if (shapes.Count > 0) projected = shapes.Select(s => s.Project(floor)).ToArray();
                }
                if (renderers.Length > 0)
                {
                    var min = box.min;
                    var max = box.max;
                    var rect = Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                    touches = min.y <= floor.height+Tolerance && max.y >= -Tolerance && Intersects(room.polygon,rect);
                    fits &= touches && min.y >= -Tolerance && max.y <= floor.height+Tolerance && Contains(room.polygon,rect);
                }
                if (shapes.Count > 0)
                {
                    foreach (var shape in projected)
                    {
                        RoomTriggerShape.Compare(shape,volume.footprintMetres,volume.heightMetres,out bool overlap,out bool contained);
                        touches |= overlap;
                        fits &= contained;
                    }
                }
            }
        }

        internal static void FindOutsideContents(HouseRoom room, List<(GameObject item, Bounds bounds)> warnings)
        {
            if (room.transform.childCount == 0) return;
            var volume = new RoomVolume(room);
            if (volume.footprintMetres == null || !volume.floor.House) return;
            foreach (Transform child in room.transform)
                foreach (var item in Candidates(child, volume.floor.House))
                {
                    if (item.GetComponentInParent<HouseRoom>(true) != room) continue;
                    var geometry = new ItemGeometry(item);
                    if (!geometry.Measurable) continue;
                    geometry.Compare(volume, out _, out bool fits);
                    if (!fits) warnings.Add((item.gameObject, geometry.WorldBounds()));
                }
        }

        static void AssignOne(Transform item, House house, RoomVolume[] rooms, Result result, string action)
        {
            var geometry = new ItemGeometry(item);
            if (!geometry.Measurable) return;
            var owners = new List<HouseRoom>();
            bool touchesHouse = false, touchesOther = false;
            foreach (var volume in rooms)
            {
                if (volume.footprintMetres == null) continue;
                geometry.Compare(volume, out bool touches, out bool fits);
                if (!touches) continue;
                if (volume.floor.House != house) { touchesOther = true; continue; }
                touchesHouse = true;
                if (fits && !geometry.global) owners.Add(volume.room);
            }
            var current = item.parent ? item.parent.GetComponentInParent<HouseFloor>() : null;
            if (owners.Count == 1 && !touchesOther)
            {
                var room = owners[0];
                var container = Container(room.transform, "Contents", action);
                if (item.parent != container)
                {
                    Undo.SetTransformParent(item, container, action);
                    result.moved++;
                }
            }
            else
            {
                if (!geometry.global && current && item.GetComponentInParent<HouseRoom>())
                {
                    var shared = Container(current.transform, "Shared contents", action);
                    if (item.parent != shared) Undo.SetTransformParent(item, shared, action);
                }
                if (touchesHouse || current) result.review.Add(item.gameObject);
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
