using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    [Serializable]
    public sealed partial class FloorPlan
    {
        public bool GenerateCollision = true;
        public bool ShowFloors = true;
        public Material InsideMaterial;
        public Material OutsideMaterial;
        public Material FloorMaterial;
        public Material CeilingMaterial;
        // Metres per repeat in House X/Z space, shared by inherited room finishes.
        public Vector2 FloorTextureSize = new Vector2(2, 2);
        public enum WallType { Auto, Interior, Exterior }
        // Every edge participates in room topology. Presence controls construction only;
        // None still closes rooms, but contributes no wall, junction or collision.
        public enum PhysicalPresence { Wall, None }
        [Serializable]
        public sealed class Corner
        {
            [SerializeField] private string id;
            [SerializeField] private Vector2 position;

            public string Id => id;
            public Vector2 Position => position;

            public Corner(Vector2 position)
            {
                id = Guid.NewGuid().ToString("N");
                this.position = position;
            }

            internal void RenewIdentity() => id = Guid.NewGuid().ToString("N");
            internal void Move(Vector2 newPosition) => position = newPosition;
            internal Corner Copy() => new Corner(position) { id = id };
        }

        [Serializable]
        public sealed class Wall
        {
            [SerializeField] private string id;
            [SerializeField] private string startId;
            [SerializeField] private string endId;
            [SerializeField] private WallType type;
            [SerializeField] private PhysicalPresence presence;
            [SerializeField] private Material leftMaterial;
            [SerializeField] private Material rightMaterial;
            [SerializeField] private WallFamily family;
            [SerializeField] private WallTrim trim;
            [SerializeField] private bool hasTrim;
            [SerializeField] private bool trimDisabled;
            public bool TrimDisabled => trimDisabled;
            public bool OverridesTrim => hasTrim || trimDisabled;
            // Keep the serialized name so existing doorways load without migration. Kind defaults to Doorway.
            [SerializeField] private List<Opening> doorways = new List<Opening>();
            private ReadOnlyModelList<Opening> openingView;

            public string Id => id;
            public string StartId => startId;
            public string EndId => endId;
            public WallType Type => type;
            public PhysicalPresence Presence => presence;
            public bool IsPhysical => presence == PhysicalPresence.Wall;
            public Material LeftMaterial => leftMaterial;
            public Material RightMaterial => rightMaterial;
            public WallFamily Family => family;
            // Unity inline serialization can recreate a class saved as null.
            // Presence is explicit so Undo/reopen cannot resurrect a removed trim.
            public WallTrim Trim => hasTrim ? trim : null;
            internal List<Opening> MutableOpenings => doorways ?? (doorways = new List<Opening>());
            public IReadOnlyList<Opening> Openings => ReadOnlyModelList<Opening>.For(MutableOpenings, ref openingView);
            internal Wall Copy() => new Wall(id, startId, endId, type, presence)
                { doorways = MutableOpenings.ConvertAll(d => d.Copy()), leftMaterial = leftMaterial, rightMaterial = rightMaterial,
                    family = family, trim = Trim?.Copy(), hasTrim = hasTrim, trimDisabled = trimDisabled };

            public Wall(string startId, string endId)
                : this(Guid.NewGuid().ToString("N"), startId, endId) { }

            internal Wall(string id, string startId, string endId, WallType type = WallType.Auto,
                PhysicalPresence presence = PhysicalPresence.Wall)
            {
                this.id = id;
                this.startId = startId;
                this.endId = endId;
                this.type = type;
                this.presence = presence;
            }

            internal void RebindForNewLevel(IReadOnlyDictionary<string, string> corners)
            {
                id = Guid.NewGuid().ToString("N");
                startId = corners[startId]; endId = corners[endId];
            }
            internal void SetEnd(string cornerId) => endId = cornerId;
            internal void SetType(WallType value) => type = value;
            internal void SetPresence(PhysicalPresence value) => presence = value;
            internal void SetMaterials(Material left, Material right) { leftMaterial = left; rightMaterial = right; }
            internal void SetFamily(WallFamily value) => family = value;
            internal void SetTrim(WallTrim value) { trim = value?.Copy(); hasTrim = value != null; trimDisabled = value == null; }
            internal void ResetTrim() { trim = null; hasTrim = false; trimDisabled = false; }
            internal void CopyTrimFrom(Wall other) { trim = other.Trim?.Copy(); hasTrim = other.hasTrim; trimDisabled = other.trimDisabled; }
        }

        [Serializable]
        public sealed class Cell
        {
            [SerializeField] private string id;
            [SerializeField] private string startCornerId;
            [SerializeField] private List<string> wallIds;
            private ReadOnlyModelList<string> wallView;
            [SerializeField] private Material wallMaterial;
            [SerializeField] private Material floorMaterial;
            [SerializeField] private Material ceilingMaterial;
            // Older rooms deserialize as indoor. Outdoor is room meaning, independent
            // of whether its surrounding edges happen to have physical walls.
            [SerializeField] private bool outdoor;

            public string Id => id;
            public string StartCornerId => startCornerId;
            public IReadOnlyList<string> WallIds => ReadOnlyModelList<string>.For(wallIds, ref wallView);
            public Material WallMaterial => wallMaterial;
            public Material FloorMaterial => floorMaterial;
            public Material CeilingMaterial => ceilingMaterial;
            public bool IsOutdoor => outdoor;

            public Cell(string id, string startCornerId, List<string> wallIds, Material wallMaterial = null,
                Material floorMaterial = null, Material ceilingMaterial = null, bool outdoor = false)
            {
                this.id = id;
                this.startCornerId = startCornerId;
                this.wallIds = new List<string>(wallIds);
                this.wallMaterial = wallMaterial;
                this.floorMaterial = floorMaterial;
                this.ceilingMaterial = ceilingMaterial;
                this.outdoor = outdoor;
            }

            internal void InsertWall(int index, string wallId) => wallIds.Insert(index, wallId);
            internal Cell Copy() => new Cell(id, startCornerId, wallIds, wallMaterial, floorMaterial, ceilingMaterial, outdoor);
            internal void SetOutdoor(bool value) => outdoor = value;
            internal void SetWallMaterial(Material material) => wallMaterial = material;
            internal void SetSurfaceMaterial(bool ceiling, Material material)
            {
                if (ceiling) ceilingMaterial = material;
                else floorMaterial = material;
            }
        }

        // Cells store boundary walls only. Incomplete interior walls live in the same wall list,
        // but no cell boundary references them. Neighbours share the same wall and corners.
        // Positions live only on corners; preview triangles are rebuilt, never saved.
        [SerializeField] private List<Corner> corners = new List<Corner>();
        [SerializeField] private List<Wall> walls = new List<Wall>();
        [SerializeField] private List<Cell> cells = new List<Cell>();
        private ReadOnlyModelList<Corner> cornerView;
        private ReadOnlyModelList<Wall> wallView;
        private ReadOnlyModelList<Cell> cellView;

        public IReadOnlyList<Corner> Corners => ReadOnlyModelList<Corner>.For(corners, ref cornerView);
        public IReadOnlyList<Wall> Walls => ReadOnlyModelList<Wall>.For(walls, ref wallView);
        public IReadOnlyList<Cell> Cells => ReadOnlyModelList<Cell>.For(cells, ref cellView);
        public string CellId => cells.Count > 0 ? cells[0].Id : "";
        public bool IsClosed => cells.Count > 0;

        // Candidates keep stable IDs and shared materials, but never share mutable graph data.
        public FloorPlan Copy() => new FloorPlan
        {
            corners = corners.ConvertAll(c => c.Copy()),
            walls = walls.ConvertAll(w => w.Copy()),
            cells = cells.ConvertAll(c => c.Copy()),
            GenerateCollision = GenerateCollision,
            ShowFloors = ShowFloors,
            InsideMaterial = InsideMaterial, OutsideMaterial = OutsideMaterial,
            FloorMaterial = FloorMaterial, CeilingMaterial = CeilingMaterial,
            FloorTextureSize = FloorTextureSize
        };

        // New levels copy authored settings but receive their own graph identities.
        internal FloorPlan CopyForNewLevel() => CopyForNewLevel(out _);

        internal FloorPlan CopyForNewLevel(out Dictionary<string, string> cornerIds)
        {
            var next = Copy();
            cornerIds = new Dictionary<string, string>();
            foreach (var corner in next.corners)
            {
                string original = corner.Id;
                corner.RenewIdentity();
                cornerIds.Add(original, corner.Id);
            }
            var wallIds = new Dictionary<string, string>();
            foreach (var wall in next.walls)
            {
                string original = wall.Id;
                wall.RebindForNewLevel(cornerIds);
                wallIds.Add(original, wall.Id);
                foreach (var opening in wall.MutableOpenings) opening.RenewIdentity();
            }
            var copiedCorners = cornerIds;
            next.cells = next.cells.ConvertAll(room => new Cell(Guid.NewGuid().ToString("N"), copiedCorners[room.StartCornerId],
                room.WallIds.Select(id => wallIds[id]).ToList(), room.WallMaterial, room.FloorMaterial,
                room.CeilingMaterial, room.IsOutdoor));
            return next;
        }

        public Vector2 GetPosition(string cornerId) => corners[CornerIndex(cornerId)].Position;

        public int ConnectedWallCount(string cornerId)
            => walls.FindAll(w => w.IsPhysical && (w.StartId == cornerId || w.EndId == cornerId)).Count;

        public bool TrySetWallType(int index, WallType type)
        {
            if (index < 0 || index >= walls.Count || !Enum.IsDefined(typeof(WallType), type)) return false;
            walls[index].SetType(type);
            return true;
        }

        public bool TrySetRoomOutdoor(IEnumerable<string> ids, bool outdoor)
        {
            var selected = ids.Distinct().Select(id => cells.Find(room => room.Id == id)).ToArray();
            if (selected.Length == 0 || selected.Any(room => room == null)) return false;
            foreach (var room in selected) room.SetOutdoor(outdoor);
            return true;
        }

        public bool TrySetWallPresence(IEnumerable<string> ids, PhysicalPresence presence)
        {
            if (!Enum.IsDefined(typeof(PhysicalPresence), presence)) return false;
            var selected = ids.Distinct().Select(id => walls.Find(wall => wall.Id == id)).ToArray();
            if (selected.Length == 0 || selected.Any(wall => wall == null)) return false;
            foreach (var wall in selected)
            {
                wall.SetPresence(presence);
                if (presence == PhysicalPresence.None)
                {
                    wall.MutableOpenings.Clear();
                    wall.SetTrim(null);
                }
            }
            return true;
        }

        public WallType GetEffectiveWallType(Wall wall)
        {
            if (wall.Type != WallType.Auto) return wall.Type;
            var rooms = cells.FindAll(c => IndexOfWall(c, wall.Id) >= 0);
            if (rooms.Count > 0) return rooms.Count == 2 && rooms.TrueForAll(room => !room.IsOutdoor)
                ? WallType.Interior : WallType.Exterior;
            // Open walls outside the enclosed rooms belong to the exterior kit until closed.
            Vector2 middle = (GetPosition(wall.StartId) + GetPosition(wall.EndId)) / 2;
            return cells.Exists(c => !c.IsOutdoor && OutlineGeometry.ContainsPoint(GetCellBoundary(c), middle))
                ? WallType.Interior : WallType.Exterior;
        }

        // A facade can have an outdoor room on its other side. Room attachment still
        // means a free topological side, so fitting uses this separate facing query.
        public bool TryGetExteriorOutward(Wall wall, out Vector2 outward)
        {
            outward = default;
            var left = RoomOnSide(wall, true);
            var right = RoomOnSide(wall, false);
            bool leftIndoor = left != null && !left.IsOutdoor;
            bool rightIndoor = right != null && !right.IsOutdoor;
            var along = (GetPosition(wall.EndId) - GetPosition(wall.StartId)).normalized;
            if (leftIndoor == rightIndoor)
            {
                // An outdoor-only enclosure still has a stable construction-facing
                // direction. Between two outdoor rooms retain the stored direction.
                if (left != null && right != null && !leftIndoor)
                {
                    outward = new Vector2(-along.y, along.x);
                    return true;
                }
                if ((left == null) == (right == null)) return false;
                leftIndoor = left != null;
            }
            outward = new Vector2(-along.y, along.x) * (leftIndoor ? -1 : 1);
            return true;
        }

        private int CornerIndex(string id)
        {
            // Geometry reads call this for every edge. A captured search predicate
            // allocates on each lookup; the direct scan keeps the same first-ID rule.
            for (int i = 0; i < corners.Count; i++)
                if (corners[i].Id == id) return i;
            return -1;
        }

        public List<string> GetCellCornerIds(Cell cell)
            => GetCellCornerIds(cell, walls);

        private static List<string> GetCellCornerIds(Cell cell, List<Wall> walls)
        {
            var boundary = new List<string>(cell.WallIds.Count);
            string next = cell.StartCornerId;
            foreach (string wallId in cell.WallIds)
            {
                Wall wall = null;
                for (int i = 0; i < walls.Count; i++)
                    if (walls[i].Id == wallId) { wall = walls[i]; break; }
                if (wall == null || (wall.StartId != next && wall.EndId != next))
                    throw new InvalidOperationException("The cell boundary has a disconnected wall.");
                boundary.Add(next);
                // A shared wall is walked in opposite directions by its two cells.
                next = wall.StartId == next ? wall.EndId : wall.StartId;
            }
            if (next != cell.StartCornerId)
                throw new InvalidOperationException("The cell boundary is not closed.");
            return boundary;
        }

        public List<Vector2> GetCellBoundary(Cell cell) => GetCellCornerIds(cell).ConvertAll(GetPosition);

        private List<Vector2> GetCellBoundary(Cell cell, IReadOnlyList<Vector2> positions)
        {
            return GetCellCornerIds(cell).ConvertAll(id => positions[CornerIndex(id)]);
        }

        private static Vector2 PositionOf(string id, List<Corner> corners)
            => corners.Find(c => c.Id == id).Position;

        public List<Vector2> GetPositions()
        {
            var positions = new List<Vector2>();
            foreach (var corner in corners)
                positions.Add(corner.Position);
            return positions;
        }

    }
}
