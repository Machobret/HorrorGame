using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    public static class HouseEditing
    {
        public static HouseFloor[] Floors(House house) => house.GetComponentsInChildren<HouseFloor>(true)
            .Where(f=>f.House==house).OrderBy(f=>f.transform.localPosition.y).ToArray();

        public static House Create(Vector3 position,WallFamily family)
        {
            var go=new GameObject("House");
            go.transform.position=position;
            var house=go.AddComponent<House>();house.exteriorFamily=house.interiorFamily=family;
            Undo.RegisterCreatedObjectUndo(go,"Create house");AddFloor(house);
            return house;
        }

        public static HouseFloor AddFloor(House house) => AddFloor(house, false);

        public static HouseFloor AddFloor(House house, bool copyLayout) =>
            AddFloor(house, Floors(house).LastOrDefault(), copyLayout, false);

        internal static HouseFloor AddFloor(House house, HouseFloor source, bool copyLayout, bool below)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add floor");
            try
            {
                var result = CreateFloor(house, source, copyLayout, below);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                return result;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static void Shift(HouseFloor floor,float distance,string action)
        {
            Undo.RecordObject(floor.transform,action);
            floor.transform.localPosition+=Vector3.up*distance;
        }

        static void NameFloors(House house)
        {
            var levels=Floors(house);
            int ground=Array.FindIndex(levels,f=>f.transform.localPosition.y>=-.001f);
            if(ground<0)ground=levels.Length;
            for(int i=0;i<levels.Length;i++)
            {
                var go=levels[i].gameObject;
                if(go.name!="Ground floor" && !System.Text.RegularExpressions.Regex.IsMatch(go.name,@"^(Floor|Basement) [1-9][0-9]*$"))continue;
                int offset=i-ground;
                string label=offset<0?"Basement "+(-offset):offset>0?"Floor "+(offset+1):"Ground floor";
                if(go.name==label)continue;
                Undo.RecordObject(go,"Name floor");go.name=label;
            }
        }

        static HouseFloor CreateFloor(House house, HouseFloor previous, bool copyLayout, bool below)
        {
            var levels=Floors(house);
            int index=Array.IndexOf(levels,previous);
            if(previous && index<0)throw new ArgumentException("Floor belongs to a different house.");
            string name="Ground floor";
            if(previous)
            {
                int insertion=below?index:index+1;
                bool shiftDown=insertion==0 || (insertion<levels.Length && levels[insertion-1].transform.localPosition.y<-.001f);
                float step=previous.height+house.slabThickness;
                for(int i=0;i<levels.Length;i++)
                    if(shiftDown?i<insertion:i>=insertion)Shift(levels[i],shiftDown?-step:step,"Insert floor");
            }
            var go=new GameObject(name);
            go.transform.SetParent(house.transform,false);
            if(previous)go.transform.SetLocalPositionAndRotation(previous.transform.localPosition+
                Vector3.up*((below?-1:1)*(previous.height+house.slabThickness)),previous.transform.localRotation);
            var floor=go.AddComponent<HouseFloor>();
            Undo.RegisterCreatedObjectUndo(go,"Add floor");
            Undo.RegisterCompleteObjectUndo(floor,"Add floor");
            if (previous && copyLayout)
            {
                EditorUtility.CopySerialized(previous, floor);
                go.name = name;
                floor.transform.localRotation = previous.transform.localRotation;
                floor.transform.localScale = previous.transform.localScale;
            }
            floor.floorId=Guid.NewGuid().ToString("N");
            if(previous)
            {
                floor.height=previous.height;
                if(!below && index==levels.Length-1 && previous.roofOpenings.Count>0)
                {
                    Undo.RecordObject(previous,"Keep ceiling openings");
                    foreach (var opening in previous.roofOpenings)
                        if (!floor.floorOpenings.Any(existing => SameOutline(existing.points, opening.points)))
                            floor.floorOpenings.Add(new SlabOpening((Vector2[])opening.points.Clone()));
                    previous.roofOpenings.Clear();
                }
            }
            if (below || index < levels.Length-1) floor.roofOpenings.Clear();
            if (previous && copyLayout) CopyLayout(previous, floor);
            NameFloors(house);
            SlabBuilder.Rebuild(house);
            if(house.booleanCuts.Count>0)BooleanOutput.Rebuild(house);
            return floor;
        }

        internal static HouseFloor DeleteFloor(HouseFloor floor)
        {
            var house=floor.House;
            var levels=Floors(house);
            int index=Array.IndexOf(levels,floor);
            var next=index>0?levels[index-1]:levels.Length>1?levels[1]:null;
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete floor");
            try
            {
                if(next && index==levels.Length-1)
                {
                    Undo.RecordObject(next,"Keep shared ceiling openings");
                    next.roofOpenings=floor.floorOpenings.Select(o=>new SlabOpening((Vector2[])o.points.Clone())).ToList();
                }
                float step=floor.height+house.slabThickness;
                bool basement=floor.transform.localPosition.y<-.001f;
                for(int i=0;i<levels.Length;i++)
                    if(basement?i<index:i>index)Shift(levels[i],basement?step:-step,"Close floor gap");
                Undo.DestroyObjectImmediate(floor.gameObject);
                NameFloors(house);
                SlabBuilder.Rebuild(house);
                if(house.booleanCuts.Count>0)BooleanOutput.Rebuild(house);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                return next;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static bool SameOutline(Vector2[] a, Vector2[] b)
        {
            if (a.Length != b.Length || a.Length == 0) return false;
            for (int start = 0; start < b.Length; start++)
                if (a[0] == b[start] && (Matches(start, 1) || Matches(start, -1))) return true;

            return false;

            bool Matches(int first, int direction)
            {
                for (int i = 1; i < a.Length; i++)
                    if (a[i] != b[(first + direction * i + b.Length) % b.Length]) return false;
                return true;
            }
        }

        static void CopyLayout(HouseFloor source, HouseFloor floor)
        {
            // Corners are floor-local. Keeping their keys preserves wall and room references.
            T CopyComponent<T>(T original) where T : Component
            {
                var go = new GameObject(original.name);
                go.transform.SetParent(floor.transform, false);
                go.transform.SetLocalPositionAndRotation(original.transform.localPosition, original.transform.localRotation);
                go.transform.localScale = original.transform.localScale;
                var copy = go.AddComponent<T>();
                EditorUtility.CopySerialized(original, copy);
                Undo.RegisterCreatedObjectUndo(go, "Copy floor layout");
                return copy;
            }

            foreach (var original in source.GetComponentsInChildren<HouseRoom>(true))
                CopyComponent(original).roomId = Guid.NewGuid().ToString("N");

            foreach (var original in source.GetComponentsInChildren<HouseWall>(true))
            {
                var wall = CopyComponent(original);
                foreach (var opening in wall.openings) opening.id = Guid.NewGuid().ToString("N");
                if (!WallBuilder.Rebuild(wall, out var issue)) throw new InvalidOperationException(issue);
                var originals = original.GetComponentsInChildren<InsertAttachment>(true).ToDictionary(a => a.openingId);
                var copies = wall.GetComponentsInChildren<InsertAttachment>(true).ToDictionary(a => a.openingId);
                for (int i = 0; i < original.openings.Count; i++)
                    if (originals.TryGetValue(original.openings[i].id, out var from) && copies.TryGetValue(wall.openings[i].id, out var to))
                        CopyInsertSettings(from, to);
            }
            if (!WallJunctions.Refresh(floor, out var reason)) throw new InvalidOperationException(reason);
        }

        static void CopyInsertSettings(InsertAttachment from, InsertAttachment to)
        {
            var original = from.transform.Cast<Transform>().FirstOrDefault(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            var copy = to.transform.Cast<Transform>().FirstOrDefault(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            if (!original || !copy) return;
            var changes = PrefabUtility.GetPropertyModifications(original.gameObject);
            if (changes == null) return;
            // A prefab override can reference another component inside the same instance.
            // Resolve that reference against the new door, including its two exit markers.
            var objects = copy.GetComponentsInChildren<Component>(true).Where(c => c)
                .Cast<UnityEngine.Object>().Concat(copy.GetComponentsInChildren<Transform>(true).Select(t => (UnityEngine.Object)t.gameObject))
                .ToLookup(o => PrefabUtility.GetCorrespondingObjectFromSource(o));
            foreach (var change in changes)
            {
                var value = change.objectReference;
                var t = value is Component component ? component.transform : (value as GameObject)?.transform;
                if (t && t.IsChildOf(original))
                    change.objectReference = objects[PrefabUtility.GetCorrespondingObjectFromSource(value)].FirstOrDefault();
            }
            PrefabUtility.SetPropertyModifications(copy.gameObject, changes);
            to.lastFrame = from.lastFrame;
            to.lastGlass = from.lastGlass;
        }

        // Adopt the original prototype's placed wall transforms once, preserving their door instances.
        public static House Adopt(HouseFloor floor,out string reason)
        {
            int group=Undo.GetCurrentGroup();
            var root=floor.transform.parent;
            if(!root)
            {
                var go=new GameObject("House");go.transform.SetPositionAndRotation(floor.transform.position,floor.transform.rotation);
                Undo.RegisterCreatedObjectUndo(go,"Create house");Undo.SetTransformParent(floor.transform,go.transform,"Adopt floor");root=go.transform;
            }
            var house=root.GetComponent<House>();
            if(!house){house=Undo.AddComponent<House>(root.gameObject);house.exteriorFamily=house.interiorFamily=floor.defaultFamily;}
            foreach(var f in Floors(house))
            {
                if(!string.IsNullOrEmpty(f.floorId))continue;
                Undo.RecordObject(f,"Adopt floor");f.floorId=Guid.NewGuid().ToString("N");
                if(f.defaultFamily && f.defaultFamily==house.exteriorFamily)f.defaultFamily=null;
                if(!new FloorEdit(f).Commit("Connect existing walls",out reason)){Undo.RevertAllDownToGroup(group);return null;}
            }
            Undo.CollapseUndoOperations(group);reason=null;return house;
        }

        public static bool Refresh(House house,out string reason)
        {
            var plans=Floors(house).Select(f=>new FloorEdit(f)).ToArray();
            foreach(var plan in plans)if(!plan.Prepare(out reason))return false;
            foreach(var plan in plans)if(!plan.Commit("Update house",out reason))return false;
            foreach(var wall in house.GetComponentsInChildren<HouseWall>(true))if(!WallBuilder.Rebuild(wall,out reason))return false;
            reason=null;return true;
        }
    }
}
