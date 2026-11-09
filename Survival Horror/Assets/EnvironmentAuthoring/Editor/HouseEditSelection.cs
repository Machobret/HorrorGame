using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [Serializable]
    public sealed class HouseEditSelection
    {
        // Keep numeric values stable when adding/removing transient selection kinds.
        public enum Kind { Node = 0, Wall = 1, Opening = 2, Room = 3, FloorSlab = 4, CeilingSlab = 5, SurfaceRegion = 6, SlabCut = 7, DividerNode = 8, Divider = 9, Trim = 11, BooleanShape = 12 }
        [Serializable] public sealed class Item
        {
            public Kind Type;
            public string Id;
            public string BoundaryId;
            public bool Ceiling;
            public Item(Kind type, string id, string boundaryId = null, bool ceiling = false)
            { Type = type; Id = id; BoundaryId = boundaryId; Ceiling = ceiling; }
        }
        public List<Item> Items = new List<Item>();
        // Keep the room's editing scope when one of its corners becomes the property
        // selection. Clicking a handle must not hide all its neighbouring handles.
        [SerializeField] private List<string> handleRooms = new List<string>();
        public bool Contains(Kind type, string id, string boundaryId = null) => Items.Any(i => i.Type == type && i.Id == id
            && (!BoundaryScoped(type) || i.BoundaryId == boundaryId));
        internal static bool BoundaryScoped(Kind type) => type == Kind.SurfaceRegion || type == Kind.SlabCut
            || type == Kind.DividerNode || type == Kind.Divider;
        public void Pick(Kind type, string id, bool add, bool subtract, HousePlan plan = null, string boundaryId = null, bool ceiling = false)
        {
            // Ruler slabs are single property targets, never part of a geometry drag group.
            if (type == Kind.FloorSlab || type == Kind.CeilingSlab || type == Kind.SlabCut || type == Kind.BooleanShape) add = subtract = false;
            else Items.RemoveAll(i => i.Type == Kind.FloorSlab || i.Type == Kind.CeilingSlab || i.Type == Kind.SlabCut);
            bool selected = Contains(type, id, boundaryId);
            if (subtract || (add && selected)) Items.RemoveAll(i => i.Type == type && i.Id == id && (!BoundaryScoped(type) || i.BoundaryId == boundaryId));
            else
            {
                if (!add) Items.Clear();
                if (!Contains(type, id, boundaryId)) Items.Add(new Item(type, id, boundaryId, ceiling));
            }
            UpdateRoomScope(plan, type == Kind.Node);
        }

        internal HashSet<string> HandleNodes(HousePlan plan)
        {
            var scope = RoomScope(plan, true);
            var rooms = plan.Data.Cells.Where(c => scope.Contains(c.Id)).ToArray();
            var roomIds = new HashSet<string>(rooms.Select(c => c.Id));
            var wallIds = new HashSet<string>(rooms.SelectMany(c => c.WallIds));
            var nodes = new HashSet<string>();
            // Incomplete walls belong spatially to a room without dividing it.
            // Resolve each wall's owners once for the whole selection, rather than
            // repeating the spatial query for every selected room on every repaint.
            if (rooms.Length > 0)
                foreach (var wall in plan.Data.Walls.Where(w => wallIds.Contains(w.Id)
                    || roomIds.Contains(plan.Data.RoomOnSide(w, true)?.Id) || roomIds.Contains(plan.Data.RoomOnSide(w, false)?.Id)))
                { nodes.Add(wall.StartId); nodes.Add(wall.EndId); }
            // Standalone wall runs and an explicitly inserted/selected corner remain
            // editable without displaying unrelated nodes across the whole house.
            nodes.UnionWith(MovingNodes(plan));
            return nodes;
        }
        public void Marquee(IEnumerable<string> nodes, bool add, bool subtract, HousePlan plan = null)
            => MarqueeItems(nodes.Select(id => new Item(Kind.Node, id)), add, subtract, plan);

        internal void MarqueeItems(IEnumerable<Item> targets, bool add, bool subtract, HousePlan plan)
        {
            Items.RemoveAll(i => i.Type == Kind.FloorSlab || i.Type == Kind.CeilingSlab || i.Type == Kind.SlabCut);
            if (!add && !subtract) Items.Clear();
            foreach (var target in targets)
            {
                if (subtract) Items.RemoveAll(i => i.Type == target.Type && i.Id == target.Id && i.BoundaryId == target.BoundaryId);
                else if (!Contains(target.Type, target.Id, target.BoundaryId)) Items.Add(target);
            }
            UpdateRoomScope(plan, true);
        }

        public void Clear()
        {
            Items.Clear();
            handleRooms.Clear();
        }

        private void UpdateRoomScope(HousePlan plan, bool retainCorners)
            => handleRooms = RoomScope(plan, retainCorners);

        private List<string> RoomScope(HousePlan plan, bool retainCorners)
        {
            var explicitRooms = Items.Where(i => i.Type == Kind.Room).Select(i => i.Id).ToList();
            if (plan != null)
            {
                var building = HouseBuilding.For(plan);
                var level = building?.Find(plan);
                if (level != null)
                    foreach (var group in Items.Where(i => i.Type == Kind.SurfaceRegion).GroupBy(i => i.BoundaryId))
                    {
                        var patches = HouseSceneQueries.BoundaryPatches(building, group.Key);
                        foreach (var item in group)
                        {
                            var patch = patches.FirstOrDefault(p => p.Floor.Id == item.Id);
                            string roomId = patch == null ? null : patch.Upper?.Id == level.Id ? patch.UpperRoom
                                : patch.Lower?.Id == level.Id ? patch.LowerRoom : null;
                            if (roomId != null) explicitRooms.Add(roomId);
                        }
                    }
            }
            // Retain a room only while editing its own corners. Wall/opening selection
            // is a new scope, not a reason to keep the previously selected room's points.
            if (retainCorners && plan != null && Items.Count > 0 && Items.All(i => i.Type == Kind.Node))
            {
                var selectedNodes = new HashSet<string>(Items.Select(i => i.Id));
                foreach (var room in plan.Data.Cells.Where(c => handleRooms.Contains(c.Id)))
                    // Most walls do not touch these points. Reject those before
                    // asking the spatial room-ownership question.
                    if (plan.Data.Walls.Any(w => (selectedNodes.Contains(w.StartId) || selectedNodes.Contains(w.EndId))
                        && (room.WallIds.Contains(w.Id) || plan.Data.RoomOnSide(w, true) == room || plan.Data.RoomOnSide(w, false) == room))) explicitRooms.Add(room.Id);
            }
            return explicitRooms;
        }
        public HashSet<string> MovingNodes(HousePlan plan)
        {
            var ids = new HashSet<string>(Items.Where(i => i.Type == Kind.Node).Select(i => i.Id));
            foreach (var wall in plan.Data.Walls.Where(w => Contains(Kind.Wall, w.Id)))
            { ids.Add(wall.StartId); ids.Add(wall.EndId); }
            return ids;
        }
        internal HashSet<string> MovingDividerNodes(HouseStack.Boundary boundary)
        {
            var result = new HashSet<string>(Items.Where(i => i.Type == Kind.DividerNode && i.BoundaryId == boundary.Id).Select(i => i.Id));
            foreach (var divider in boundary.Dividers.Where(d => Contains(Kind.Divider, d.Id, boundary.Id)))
            { result.Add(divider.StartId); result.Add(divider.EndId); }
            return result;
        }

        internal HashSet<string> HandleDividerNodes(HousePlan plan, HouseStack.Boundary boundary)
        {
            var result = MovingDividerNodes(boundary);
            var selectedNodes = new HashSet<string>(result);
            foreach (var divider in boundary.Dividers)
                if (selectedNodes.Contains(divider.StartId) || selectedNodes.Contains(divider.EndId))
                { result.Add(divider.StartId); result.Add(divider.EndId); }
            var building = HouseBuilding.For(plan);
            foreach (var item in Items.Where(i => i.Type == Kind.SurfaceRegion && i.BoundaryId == boundary.Id))
            {
                var patch = HouseSceneQueries.BoundaryPatches(building, boundary.Id).FirstOrDefault(p => p.Floor.Id == item.Id);
                if (patch == null) continue;
                foreach (var node in boundary.DividerNodes)
                    if (SlabGeometry.Contains(patch.Shape, node.Position)) result.Add(node.Id);
            }
            return result;
        }

        private static bool BoundaryItemExists(HousePlan plan, Item item)
        {
            var building = HouseBuilding.For(plan);
            var level = building?.Find(plan);
            var boundary = building?.Boundaries.FirstOrDefault(b => b.Id == item.BoundaryId);
            // Existence belongs to saved identities, not an earlier event's geometry.
            return level != null && boundary != null
                && (boundary.UpperId == level.Id || boundary.LowerId == level.Id)
                && (item.Type == Kind.SurfaceRegion ? boundary.Regions.Any(region => !region.Ceiling && region.Id == item.Id)
                    : item.Type == Kind.SlabCut ? boundary.Openings.Any(cut => cut.Id == item.Id)
                    : item.Type == Kind.DividerNode ? boundary.DividerNodes.Any(node => node.Id == item.Id)
                    : boundary.Dividers.Any(divider => divider.Id == item.Id));
        }
        public void Prune(HousePlan plan)
        {
            handleRooms.RemoveAll(id => !plan.Data.Cells.Any(c => c.Id == id));
            Items.RemoveAll(i => i.Type == Kind.Node ? !plan.Data.Corners.Any(c => c.Id == i.Id)
                : i.Type == Kind.Wall || i.Type == Kind.Trim ? !plan.Data.Walls.Any(w => w.Id == i.Id)
                : i.Type == Kind.Opening ? !plan.Data.Walls.Any(w => w.Openings.Any(o => o.Id == i.Id))
                : i.Type == Kind.Room ? !plan.Data.Cells.Any(c => c.Id == i.Id)
                : i.Type == Kind.BooleanShape ? HouseBuilding.For(plan)?.Data.BooleanVolumes.Any(volume => volume.Id == i.Id) != true
                : BoundaryScoped(i.Type) ? !BoundaryItemExists(plan, i)
                : (i.Type != Kind.FloorSlab && i.Type != Kind.CeilingSlab)
                    || HouseBuilding.For(plan)?.Find(plan)?.Id != i.Id);
            UpdateRoomScope(plan, true);
        }
    }

}
