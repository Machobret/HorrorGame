using System.Collections.Generic;

namespace HorrorGame.Documents
{
    public sealed class DocumentLedgerState
    {
        internal const int PinLimit = 3;
        readonly HashSet<string> archived = new();
        readonly List<string> pins = new();
        public int Revision { get; private set; }

        public bool IsArchived(string id) => archived.Contains(id);
        public bool IsPinned(string id) => pins.Contains(id);

        // Called by gameplay when a clue stops being relevant. This also supports late pickups.
        public void Archive(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!archived.Add(id)) return;
            pins.Remove(id);
            Revision++;
        }

        public bool TogglePin(string id)
        {
            if (string.IsNullOrEmpty(id) || IsArchived(id)) return false;
            if (!pins.Remove(id)) AddPin(id);
            Revision++;
            return true;
        }

        void AddPin(string id)
        {
            if (pins.Count == PinLimit) pins.RemoveAt(0);
            pins.Add(id);
        }

        public List<string> CopyArchive() => new(archived);
        public List<string> CopyPins() => new(pins);

        public void Restore(IEnumerable<string> archivedIds, IEnumerable<string> pinnedIds, HashSet<string> collected)
        {
            Clear();
            if (archivedIds != null)
                foreach (string id in archivedIds) Archive(id);
            if (pinnedIds != null)
                foreach (string id in pinnedIds)
                    if (collected.Contains(id) && !IsArchived(id) && !IsPinned(id)) AddPin(id);
        }

        public void Clear()
        {
            archived.Clear();
            pins.Clear();
            Revision++;
        }
    }
}
