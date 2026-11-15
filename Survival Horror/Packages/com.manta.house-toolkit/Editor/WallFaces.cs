using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class WallFaces
    {
        internal static HouseRoom RoomOnSide(HouseWall wall, bool positive, HouseRoom[] rooms)
        {
            var floor = wall.Floor;
            var a = floor.Position(wall.startCorner);
            var b = floor.Position(wall.endCorner);
            var side = Vector2.Perpendicular((b - a).normalized);
            var probe = (a + b) * .5f + side * (positive ? .0001f : -.0001f);
            foreach (var room in rooms)
            {
                if (room.corners == null || room.polygon == null) continue;
                for (int i = 0; i < room.corners.Length; i++)
                {
                    int from = room.corners[i], to = room.corners[(i + 1) % room.corners.Length];
                    if (from == wall.startCorner && to == wall.endCorner && positive ||
                        from == wall.endCorner && to == wall.startCorner && !positive) return room;
                }
                if (RoomGeometry.Inside(probe, room.polygon)) return room;
            }
            return null;
        }

        internal static Material Resolve(HouseWall wall, bool positive, HouseRoom[] rooms)
        {
            var local = positive ? wall.positiveFinish : wall.negativeFinish;
            if (local) return local;
            if (wall.surfaceOverride) return wall.surfaceOverride;
            var room = RoomOnSide(wall, positive, rooms);
            if (room && room.wallFinish) return room.wallFinish;
            var inherited = wall.Floor.FinishFor(room);
            return inherited ? inherited : wall.Family.surface;
        }

        static Mesh faceMesh;
        internal static void Apply(HouseWall wall, WallOutput output)
        {
            var rooms = wall.Floor.GetComponentsInChildren<HouseRoom>(true);
            var back = Resolve(wall, false, rooms);
            var front = Resolve(wall, true, rooms);
            if (!faceMesh) faceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Packages/com.manta.house-toolkit/Runtime/Wall Faces.asset");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            bool meshChanged = false;
            foreach (var renderer in output.surfaces)
            {
                if (!renderer) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = front == back ? cube : faceMesh;
                if (filter.sharedMesh != mesh)
                {
                    filter.sharedMesh = mesh;
                    meshChanged = true;
                    EditorUtility.SetDirty(filter);
                }
                var slots = front == back ? new[]{back} : new[]{back, front};
                if (renderer.sharedMaterials.SequenceEqual(slots)) continue;
                renderer.sharedMaterials = slots;
                EditorUtility.SetDirty(renderer);
            }
            if (meshChanged && wall.Floor.House && wall.Floor.House.booleanCuts.Count > 0)
                BooleanOutput.Apply(wall.Floor.House, output);
        }
    }
}
