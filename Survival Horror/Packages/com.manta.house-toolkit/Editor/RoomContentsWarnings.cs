using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static class RoomContentsWarnings
    {
        static readonly Dictionary<HouseRoom, List<(GameObject item, Bounds bounds)>> cache = new();
        static readonly List<HouseRoom> affected = new();

        static RoomContentsWarnings()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
            EditorApplication.hierarchyChanged += Clear;
            Undo.undoRedoPerformed += Clear;
        }

        static void Clear()
        {
            if (cache.Count == 0) return;
            cache.Clear();
            RefreshViews();
        }

        static void RefreshViews()
        {
            HouseProperties.RepaintOpen();
            SceneView.RepaintAll();
        }

        static void OnChanges(ref ObjectChangeEventStream changes)
        {
            if (cache.Count == 0) return;
            bool dirty = false;
            for (int i = 0; i < changes.length; i++)
            {
                if (changes.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    // Reparenting, prefab updates and component additions can affect
                    // several rooms, including the previous owner of a moved group.
                    Clear();
                    return;
                }
                changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                var obj = EditorUtility.EntityIdToObject((EntityId)change.instanceId);
                var t = obj is Component component ? component.transform : (obj as GameObject)?.transform;
                if (!t) continue;
                var owner = t.GetComponentInParent<HouseRoom>(true);
                if (owner) dirty |= cache.Remove(owner);
                else if (t.childCount != 0 && (obj is Transform || obj is HouseFloor || obj is GameObject))
                {
                    affected.Clear();
                    foreach (var room in cache.Keys)
                        if (!room || room.transform.IsChildOf(t)) affected.Add(room);
                    foreach (var room in affected) dirty |= cache.Remove(room);
                }
            }
            if (dirty) RefreshViews();
        }

        internal static IReadOnlyList<(GameObject item, Bounds bounds)> ForRoom(HouseRoom room)
        {
            if (!cache.TryGetValue(room, out var warnings))
            {
                warnings = new List<(GameObject, Bounds)>();
                RoomAssignment.FindOutsideContents(room, warnings);
                cache.Add(room, warnings);
            }
            return warnings;
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void Draw(HouseRoom room, GizmoType gizmoType)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Camera.current ||
                Camera.current.cameraType != CameraType.SceneView) return;
            var previous = Gizmos.color;
            Gizmos.color = new Color(1f, .18f, .13f);
            foreach (var (item, bounds) in ForRoom(room))
            {
                if (!item || !item.activeInHierarchy || SceneVisibilityManager.instance.IsHidden(item)) continue;
                // Emitters occupy a point. Give them a visible marker too.
                Gizmos.DrawWireCube(bounds.center, Vector3.Max(bounds.size, Vector3.one * .18f));
            }
            Gizmos.color = previous;
        }
    }
}
