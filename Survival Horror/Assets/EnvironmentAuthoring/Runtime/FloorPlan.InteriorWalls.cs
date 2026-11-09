namespace EnvironmentAuthoring
{
    public sealed partial class FloorPlan
    {
        public bool IsIncompleteWall(int wallIndex)
        {
            return wallIndex >= 0 && wallIndex < walls.Count
                && !cells.Exists(c => IndexOfWall(c, walls[wallIndex].Id) >= 0);
        }

        public bool CanRemoveInteriorWall(int wallIndex, out string reason)
        {
            reason = IsIncompleteWall(wallIndex) ? "" : "Choose an incomplete wall. Delete shared boundaries to merge their rooms.";
            return reason.Length == 0;
        }

        public bool TryRemoveInteriorWall(int wallIndex, out string reason)
        {
            if (!CanRemoveInteriorWall(wallIndex, out reason))
                return false;
            walls.RemoveAt(wallIndex);
            // Boundary anchors survive. Only endpoints with no remaining wall are removed.
            corners.RemoveAll(c => !walls.Exists(w => w.StartId == c.Id || w.EndId == c.Id));
            return true;
        }
    }
}
