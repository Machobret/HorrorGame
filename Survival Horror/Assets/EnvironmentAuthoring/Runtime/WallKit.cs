using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring
{
    [Serializable]
    public sealed class WallKit
    {
        public WallFamily DefaultFamily;
        public WallFamily ExteriorFamily;

        public WallOpeningInsert[] Inserts = Array.Empty<WallOpeningInsert>();
        public WallModuleDefinition ReferenceBlank => DefaultFamily != null ? DefaultFamily.ReferenceBlank : null;
        public float DefaultHeight => ReferenceBlank != null ? ReferenceBlank.Height : 2.8f;
        public IEnumerable<WallFamily> FamilyDefinitions(FloorPlan plan = null)
        {
            var seen = new HashSet<WallFamily>();
            if (DefaultFamily != null && seen.Add(DefaultFamily)) yield return DefaultFamily;
            if (ExteriorFamily != null && seen.Add(ExteriorFamily)) yield return ExteriorFamily;
            if (plan == null) yield break;
            foreach (var wall in plan.Walls)
                if (wall.Family != null && seen.Add(wall.Family)) yield return wall.Family;
        }
        // A per-wall choice is deliberate. Automatic walls follow current room
        // ownership, so enclosure edits change geometry as well as finishes.
        public WallFamily FamilyFor(FloorPlan plan, FloorPlan.Wall wall)
            => wall.Family != null ? wall.Family
                : plan.GetEffectiveWallType(wall) == FloorPlan.WallType.Exterior && ExteriorFamily != null
                    ? ExteriorFamily : DefaultFamily;

        public IEnumerable<WallModuleDefinition> BlanksFor(FloorPlan plan, FloorPlan.Wall wall)
        {
            var family = FamilyFor(plan, wall);
            return family != null ? family.Blanks() : Array.Empty<WallModuleDefinition>();
        }
        public bool ShowWalls = true;
        public bool OverrideTrim;
        public WallTrim DefaultTrim = new WallTrim();

        public IEnumerable<WallModuleDefinition> AllDefinitions(FloorPlan plan = null)
        {
            foreach (var family in FamilyDefinitions(plan))
                foreach (var piece in family.Definitions()) yield return piece;
        }

        // Candidate settings own their containers; the referenced module assets stay shared.
        public WallKit Copy()
        {
            var copy = (WallKit)MemberwiseClone();
            copy.Inserts = (WallOpeningInsert[])Inserts?.Clone();
            copy.DefaultTrim = DefaultTrim?.Copy();
            return copy;
        }
    }
}
