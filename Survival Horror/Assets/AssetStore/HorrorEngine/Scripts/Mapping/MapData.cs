using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [System.Serializable]
    public class ShapeData
    {
        public Vector3[] Points;
        // Individual map paths may be open branches or closed loops. This is
        // intentionally stored with the points so it survives map serialization.
        public bool Closed;
        public ShapeEdge[] Connections;
    }

    [Serializable]
    public struct MapWallOpening
    {
        public Vector2 Start;
        public Vector2 End;
    }

    [Serializable]
    public class MapElementTransform
    {
        public Vector2 Offset;
        public Vector3 Scale;
        [Tooltip("Elements with higher ZOrder will show on top of those with lower order")]
        public float ZOrder;
        public float Rotation;
    }

    [Serializable]
    public class MapImageSerialized
    {
        public MapElementTransform Transform;
        public Texture2D Texture;
        public string UniqueId;
        public bool DefaultState;

        public bool GetState()
        {
            if (string.IsNullOrEmpty(UniqueId))
                return DefaultState;

            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                return savedObjectState.Active;
            }

            return DefaultState;
        }
    }

    [Serializable]
    public class MapDetailsSerializedData
    {
        public MapElementTransform Transform;
        public ShapeData Shape;
        public ShapeCreationProcess CreationProcess;
        public string UniqueId;
        public bool DefaultState;

        public bool GetState()
        {
            if (string.IsNullOrEmpty(UniqueId))
                return DefaultState;

            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                return savedObjectState.Active;
            }

            return DefaultState;
        }
    }

    [Serializable]
    public class MapRoomSerializedData
    {
        public string Name; // Not a UI/game text, used for gameobjects
        public string UniqueId;
        public MapElementTransform Transform;
        public ShapeData[] Shapes;
        public List<string> LinkedElements;
        public MapDetailsSerializedData[] Details;
        public MapImageSerialized[] Images;

        public MapRoomState GetState()
        {
             if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
             {
                string componentData = savedObjectState.GetComponentData<MapRoom>();
                if (!string.IsNullOrEmpty(componentData))
                    return Enum.Parse<MapRoomState>(componentData);
             }

            return MapRoomState.Unknown;
        }

    }

    [Serializable]
    public class MapDoorSerializedData
    {
        public string Name; // Not a UI/game text, used for gameobjects
        public string UniqueId;
        public MapElementTransform Transform;
        public Vector2 Size;

        public MapDoorState GetState()
        {
            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                string componentData = savedObjectState.GetComponentData<MapDoor>();
                if (!string.IsNullOrEmpty(componentData))
                    return Enum.Parse<MapDoorState>(componentData);
            }

            return MapDoorState.Unknown;
        }

    }

    [CreateAssetMenu(menuName = "Horror Engine/Mapping/MapData")]
    public class MapData : Register
    {
        public string ControllerUniqueId;

        public LocalizableText Name;
        public LocalizableText Abbreviation;

        // TODO - Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED = "NoName";

        [FormerlySerializedAs("Abbreviation")]
        [HideInInspector]
        public string Abbreviation_DEPRECATED = "";
        // ----


        public Vector2Int Size = new Vector2Int(25, 25);
        public int CellSize = 50;
        public float GlobalScale = 10f;
        public MapDataSet MapSet;
        public List<MapRoomSerializedData> Rooms = new List<MapRoomSerializedData>();
        public List<MapDoorSerializedData> Doors = new List<MapDoorSerializedData>();
        [Tooltip("Doorway spans in the same map coordinates as room offsets. These affect wall lines only.")]
        public List<MapWallOpening> WallOpenings = new List<MapWallOpening>();

        internal ShapeData[] GetWallShapes(ShapeData[] shapes, Matrix4x4 roomTransform, bool closePaths)
        {
            if (WallOpenings == null || WallOpenings.Count == 0) return shapes;

            var walls = new List<ShapeData>();
            var gaps = new List<Vector2>();
            foreach (var shape in shapes)
            {
                var points = shape.Points;
                int firstSpan = walls.Count;
                bool clipped = false;
                for (int i = 1; i < points.Length; i++)
                    clipped |= AddWallSpans(walls, gaps, roomTransform, points[i - 1], points[i]);
                if ((closePaths || shape.Closed) && points.Length >= 3)
                    clipped |= AddWallSpans(walls, gaps, roomTransform, points[points.Length - 1], points[0]);
                if (shape.Connections != null)
                    foreach (var edge in shape.Connections)
                        if ((uint)edge.From < points.Length && (uint)edge.To < points.Length && edge.From != edge.To)
                            clipped |= AddWallSpans(walls, gaps, roomTransform, points[edge.From], points[edge.To]);
                if (!clipped)
                {
                    walls.RemoveRange(firstSpan, walls.Count - firstSpan);
                    walls.Add(shape);
                }
            }
            return walls.ToArray();
        }

        private bool AddWallSpans(List<ShapeData> walls, List<Vector2> gaps, Matrix4x4 roomTransform,
            Vector3 from, Vector3 to)
        {
            Vector3 projectedFrom = roomTransform.MultiplyPoint3x4(from);
            Vector3 projectedTo = roomTransform.MultiplyPoint3x4(to);
            var origin = new Vector2(projectedFrom.x, -projectedFrom.z);
            var direction = new Vector2(projectedTo.x, -projectedTo.z) - origin;
            if (direction.sqrMagnitude < .000001f) return false;

            Vector2 normal = Vector2.Perpendicular(direction.normalized);
            float tolerance = .03f * GlobalScale;
            gaps.Clear();
            foreach (var opening in WallOpenings)
            {
                Vector2 start = opening.Start - origin;
                Vector2 end = opening.End - origin;
                if (Mathf.Abs(Vector2.Dot(start, normal)) > tolerance ||
                    Mathf.Abs(Vector2.Dot(end, normal)) > tolerance) continue;
                float a = Vector2.Dot(start, direction) / direction.sqrMagnitude;
                float b = Vector2.Dot(end, direction) / direction.sqrMagnitude;
                float lo = Mathf.Clamp01(Mathf.Min(a, b));
                float hi = Mathf.Clamp01(Mathf.Max(a, b));
                if (hi > lo) gaps.Add(new Vector2(lo, hi));
            }

            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = 0f;
            foreach (var gap in gaps)
            {
                if (gap.x > cursor) AddSpan(cursor, gap.x);
                cursor = Mathf.Max(cursor, gap.y);
            }
            if (cursor < 1f) AddSpan(cursor, 1f);
            return gaps.Count > 0;

            void AddSpan(float start, float end)
            {
                walls.Add(new ShapeData
                {
                    Points = new[] { Vector3.LerpUnclamped(from, to, start), Vector3.LerpUnclamped(from, to, end) }
                });
            }
        }

        public Matrix4x4 GetTRS(Vector2 offset, float rotation, Vector3 scale, float zOrder)
        {
            return Matrix4x4.TRS(new Vector3(offset.x, zOrder, -offset.y), Quaternion.Euler(0, -rotation, 0), GlobalScale * scale);
        }

        public bool GetVisited()
        {
            if (string.IsNullOrEmpty(ControllerUniqueId) || ObjectStateManager.Instance == null)
                return false;
            if (ObjectStateManager.Instance.GetState(ControllerUniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                string componentData = savedObjectState.GetComponentData<MapController>();
                if (!string.IsNullOrEmpty(componentData))
                    return Convert.ToBoolean(componentData);
            }

            return false;
        }

        public bool IsKnownByPlayer()
        {
            return GetVisited() || GameManager.Instance.Inventory.Maps.Contains(this);
        }
        
#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Name_DEPRECATED))
            {
                Name.Unlocalized = Name_DEPRECATED;
                Name_DEPRECATED = "";
                dirty = true;
            }

            if (!string.IsNullOrEmpty(Abbreviation_DEPRECATED))
            {
                Abbreviation.Unlocalized = Abbreviation_DEPRECATED;
                Abbreviation_DEPRECATED = "";
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }
        }
#endif
    }
}
