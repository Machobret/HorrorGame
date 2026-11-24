using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorrorGame.UI
{
    internal enum ReporterPanel { Profile, Inventory, Ledger, Display }

    internal sealed class ReporterFocus
    {
        internal sealed class Target
        {
            public string Id { get; }
            public ReporterPanel Panel { get; }
            public Vector2 Position { get; }
            public int EntryPriority { get; }
            public bool IsTablet { get; }

            public Target(string id, ReporterPanel panel, Vector2 position, int entryPriority = 0, bool isTablet = false)
            {
                Id = id;
                Panel = panel;
                Position = position;
                EntryPriority = entryPriority;
                IsTablet = isTablet;
            }
        }

        readonly Dictionary<ReporterPanel, string> remembered = new();
        string lastInventoryItem;
        public string Id { get; private set; }
        public ReporterPanel Panel { get; private set; } = ReporterPanel.Inventory;

        public void Select(Target target)
        {
            if (target == null) return;
            Id = target.Id;
            Panel = target.Panel;
            remembered[Panel] = Id;
            if (Panel == ReporterPanel.Inventory && !target.IsTablet) lastInventoryItem = Id;
        }

        public Target Restore(IReadOnlyList<Target> targets) =>
            targets.FirstOrDefault(target => target.Id == Id)
            ?? Preferred(targets, Panel)
            ?? Preferred(targets, ReporterPanel.Inventory)
            ?? Preferred(targets, ReporterPanel.Ledger)
            ?? targets.FirstOrDefault();

        public Target Preferred(IEnumerable<Target> targets, ReporterPanel panel)
        {
            remembered.TryGetValue(panel, out string id);
            return targets.FirstOrDefault(target => target.Panel == panel && target.Id == id)
                ?? targets.Where(target => target.Panel == panel).OrderByDescending(target => target.EntryPriority).FirstOrDefault();
        }

        public Target Move(IReadOnlyList<Target> targets, Vector2 direction)
        {
            Target current = Restore(targets);
            if (current == null) return null;

            if (current.IsTablet && direction.y > 0)
            {
                var items = targets.Where(target => target.Panel == ReporterPanel.Inventory && !target.IsTablet);
                return items.FirstOrDefault(target => target.Id == lastInventoryItem)
                    ?? items.OrderBy(target => target.Position.y).FirstOrDefault() ?? current;
            }

            Target best = null;
            float distance = float.PositiveInfinity;
            foreach (Target candidate in targets)
            {
                if (candidate == current || candidate.Panel != current.Panel || candidate.IsTablet) continue;
                Vector2 delta = candidate.Position - current.Position;
                float forward = Vector2.Dot(delta, direction);
                float sideways = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                if (forward <= 0.001f || sideways > forward * (direction.y == 0 ? 0.75f : 1.2f)) continue;
                float score = forward + 3 * sideways;
                if (score >= distance) continue;
                best = candidate;
                distance = score;
            }
            if (best != null) return best;

            // These are panel relationships. The number and identity of nodes in each is dynamic.
            if (current.Panel == ReporterPanel.Ledger && direction.x < 0)
                return Preferred(targets, ReporterPanel.Inventory) ?? current;
            if (current.Panel == ReporterPanel.Inventory && direction.y < 0)
                return targets.FirstOrDefault(target => target.IsTablet) ?? current;
            if (current.Panel != ReporterPanel.Ledger && direction.x > 0)
                return Preferred(targets, ReporterPanel.Ledger) ?? current;
            return current;
        }
    }
}
