using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public enum OpeningKind { Doorway, Window }

        [Serializable]
        public sealed class Opening
        {
            [SerializeField] private string id;
            [SerializeField] private float distance;
            [SerializeField] private float width;
            [SerializeField] private OpeningKind kind;
            [SerializeField] private string shape;
            [SerializeField] private WallOpeningInsert insert;
            [SerializeField] private OpeningFamily insertFamily;
            [SerializeField] private OpeningFamily.Condition condition;
            public OpeningFamily InsertFamily => insertFamily;
            public OpeningFamily.Condition Condition => condition;
            public string Shape => shape;
            public WallOpeningInsert Insert => insert;
            public string Id => id;
            public OpeningKind Kind => kind;
            // Metres from the wall's stored start, not a fraction of its changing length.
            public float Distance => distance;
            public float Width => width;
            internal Opening(float distance, float width, OpeningKind kind)
            { id = Guid.NewGuid().ToString("N"); this.distance = distance; this.width = width; this.kind = kind; }
            internal Opening Copy() => new Opening(distance, width, kind) { id = id, shape = shape,
                insert = insert, insertFamily = insertFamily, condition = condition };
            internal void SetAssets(string value, WallOpeningInsert chosenInsert, OpeningFamily family = null,
                OpeningFamily.Condition state = OpeningFamily.Condition.Clean)
            { shape = value; insert = chosenInsert; insertFamily = family; condition = state; }
            internal void RenewIdentity() => id = Guid.NewGuid().ToString("N");
            internal void Move(float value) => distance = value;
            internal void ChangeKind(OpeningKind value, float newWidth) { kind = value; width = newWidth; }
        }

        public bool CanPlaceOpening(string wallId, float distance, float width, string ignoreId, out string reason)
        {
            var wall = walls.Find(w => w.Id == wallId);
            reason = "Select an existing wall and a finite, positive opening width.";
            if (wall == null || !wall.IsPhysical || float.IsNaN(distance) || float.IsInfinity(distance)
                || float.IsNaN(width) || float.IsInfinity(width) || width <= 0) return false;
            reason = "The opening must fit entirely on its wall.";
            float length = Vector2.Distance(GetPosition(wall.StartId), GetPosition(wall.EndId));
            if (distance - width / 2 < -0.0001f || distance + width / 2 > length + 0.0001f) return false;
            reason = "Opening modules cannot overlap. Leave room for both surrounds.";
            foreach (var other in wall.Openings)
                if (other.Id != ignoreId && Mathf.Abs(other.Distance - distance) < (other.Width + width) / 2 - 0.0001f)
                    return false;
            reason = "";
            return true;
        }

        public bool TryAddOpening(string wallId, float distance, float width, OpeningKind kind, out string reason)
        {
            reason = "Choose a doorway or window.";
            if (!Enum.IsDefined(typeof(OpeningKind), kind)) return false;
            if (!CanPlaceOpening(wallId, distance, width, null, out reason)) return false;
            walls.Find(w => w.Id == wallId).MutableOpenings.Add(new Opening(distance, width, kind));
            return true;
        }

        public bool TryMoveOpening(string wallId, string doorwayId, float distance, out string reason)
        {
            var door = walls.Find(w => w.Id == wallId)?.MutableOpenings.Find(d => d.Id == doorwayId);
            reason = "The opening no longer exists.";
            if (door == null || !CanPlaceOpening(wallId, distance, door.Width, doorwayId, out reason)) return false;
            door.Move(distance);
            return true;
        }

        public bool TryRemoveOpening(string wallId, string doorwayId)
            => walls.Find(w => w.Id == wallId)?.MutableOpenings.RemoveAll(d => d.Id == doorwayId) > 0;

        public bool TryChangeOpening(string wallId, string id, OpeningKind kind, float width, out string reason)
        {
            var opening = walls.Find(w => w.Id == wallId)?.MutableOpenings.Find(o => o.Id == id);
            reason = "Choose an existing door or window.";
            if (opening == null || !Enum.IsDefined(typeof(OpeningKind), kind)) return false;
            if (!CanPlaceOpening(wallId, opening.Distance, width, id, out reason)) return false;
            opening.ChangeKind(kind, width);
            return true;
        }

        public bool TryMoveOpenings(IReadOnlyDictionary<string, float> targets, out string reason)
        {
            int found = 0;
            foreach (var wall in walls)
            {
                float length = Vector2.Distance(GetPosition(wall.EndId), GetPosition(wall.StartId));
                for (int i = 0; i < wall.Openings.Count; i++)
                {
                    var opening = wall.Openings[i];
                    float distance = targets.TryGetValue(opening.Id, out float target) ? target : opening.Distance;
                    if (targets.ContainsKey(opening.Id)) found++;
                    reason = "Keep openings within their walls.";
                    if (float.IsNaN(distance) || float.IsInfinity(distance) || distance - opening.Width / 2 < -0.0001f
                        || distance + opening.Width / 2 > length + 0.0001f) return false;
                    for (int j = 0; j < i; j++)
                    {
                        var other = wall.Openings[j];
                        float otherDistance = targets.TryGetValue(other.Id, out float next) ? next : other.Distance;
                        reason = "Opening modules cannot overlap.";
                        if (Mathf.Abs(distance - otherDistance) < (opening.Width + other.Width) / 2 - 0.0001f) return false;
                    }
                }
            }
            reason = "A selected opening no longer exists.";
            if (found != targets.Count) return false;
            foreach (var wall in walls) foreach (var opening in wall.Openings)
                if (targets.TryGetValue(opening.Id, out float distance)) opening.Move(distance);
            reason = "";
            return true;
        }
        // Keep the original doorway API available for older tools and tests.
        public bool CanPlaceDoorway(string wallId, float distance, float width, string ignoreId, out string reason)
            => CanPlaceOpening(wallId, distance, width, ignoreId, out reason);
        public bool TryAddDoorway(string wallId, float distance, float width, out string reason)
            => TryAddOpening(wallId, distance, width, OpeningKind.Doorway, out reason);
        public bool TryMoveDoorway(string wallId, string doorwayId, float distance, out string reason)
            => TryMoveOpening(wallId, doorwayId, distance, out reason);
        public bool TryRemoveDoorway(string wallId, string doorwayId) => TryRemoveOpening(wallId, doorwayId);
    }
}
