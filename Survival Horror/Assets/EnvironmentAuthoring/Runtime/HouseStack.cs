using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Authoring data only. Levels keep their existing HousePlan identities.
    [Serializable]
    public sealed partial class HouseStack
    {
        public HouseStack Copy() => new HouseStack
        {
            floorThickness = floorThickness, ceilingThickness = ceilingThickness, anchorId = anchorId,
            insideMaterial = insideMaterial, outsideMaterial = outsideMaterial, floorMaterial = floorMaterial,
            ceilingMaterial = ceilingMaterial, ceilingTextureSize = ceilingTextureSize,
            defaultTrim = defaultTrim?.Copy(), hasDefaultTrim = hasDefaultTrim,
            levels = levels.ConvertAll(level => level.Copy()),
            boundaries = boundaries.ConvertAll(boundary => boundary.Copy()),
            booleanVolumes = booleanVolumes.ConvertAll(volume => volume.Copy()),
            booleanCutMaterial = booleanCutMaterial
        };
        public float SlabThickness => FloorThickness + CeilingThickness;
        public float SurfaceOffset => SlabThickness / 2;
        public Level Anchor => Find(anchorId);
        public float Elevation(Level level)
        {
            int anchor = Levels.IndexOf(Anchor), selected = Levels.IndexOf(level);
            if (anchor < 0 || selected < 0) return float.NaN;
            float height = 0;
            if (selected < anchor)
                for (int i = anchor - 1; i >= selected; i--) height -= Levels[i].Span;
            else
                for (int i = anchor; i < selected; i++) height += Levels[i].Span;
            return height;
        }
        public float FloorElevation(Level level) => Elevation(level) + SurfaceOffset;
        public Level Find(HousePlan plan) => Levels.Find(l => l != null && l.Plan == plan);
        public Level Find(string id) => Levels.Find(l => l != null && l.Id == id);
        public Boundary Above(Level level) => Boundaries.Find(b => b.LowerId == level.Id);
        public Boundary Below(Level level) => Boundaries.Find(b => b.UpperId == level.Id);
        public float WallHeight(Level level) => level.Span - SlabThickness;

        public bool TrySetSlabThickness(float total, float finish, out string reason)
        {
            float structure = total - finish;
            reason = "Use finite thicknesses with at least 0.01 m for both finish and structure.";
            if (!float.IsFinite(total) || !float.IsFinite(finish) || finish < .01f || structure < .01f - .00000001f) return false;
            // Canonicalize only float rounding at the one-centimetre minimum.
            structure = Mathf.Max(.01f, structure);
            total = finish + structure;
            if (!ValidSpans(Levels.Select(level => level.Span).ToArray(), total, out reason)) return false;
            floorThickness = finish;
            ceilingThickness = structure;
            return true;
        }

        public bool TrySetLevelHeight(Level level, float height, out string reason)
        {
            reason = "Choose an existing level and a finite wall height of at least 0.3 m.";
            int selected = Levels.IndexOf(level);
            if (selected < 0 || !float.IsFinite(height) || height < .3f) return false;
            var spans = Levels.Select(item => item.Span).ToArray();
            spans[selected] = height + SlabThickness;
            if (!ValidSpans(spans, SlabThickness, out reason)) return false;
            level.SetSpan(spans[selected]);
            return true;
        }

        public bool TrySetLevelElevation(Level level, float elevation, bool moveOuterLevels, out string reason)
        {
            reason = "Choose an existing level and a finite elevation.";
            int selected = Levels.IndexOf(level), anchor = Levels.IndexOf(Anchor);
            if (selected < 0 || anchor < 0 || !float.IsFinite(elevation)) return false;
            reason = "Level 1 stays at 0 m. Move the House root to relocate the building.";
            if (selected == anchor)
            {
                if (elevation != 0) return false;
                reason = "";
                return true;
            }
            float delta = elevation - Elevation(level);
            var spans = Levels.Select(item => item.Span).ToArray();
            if (selected > anchor)
            {
                spans[selected - 1] += delta;
                // A local edit leaves the next plane (including the roof) fixed.
                if (!moveOuterLevels) spans[selected] -= delta;
            }
            else
            {
                // Basement spans rise to the level above; changing one moves its
                // floor and deeper floors while the Level 1 anchor stays fixed.
                spans[selected] -= delta;
                if (!moveOuterLevels && selected > 0) spans[selected - 1] += delta;
            }
            if (!ValidSpans(spans, SlabThickness, out reason)) return false;
            for (int i = 0; i < Levels.Count; i++) Levels[i].SetSpan(spans[i]);
            return true;
        }

        private bool ValidSpans(float[] spans, float thickness, out string reason)
        {
            reason = "Each level needs a distinct identity and an existing Level 1 anchor.";
            if (levels == null || levels.Count == 0 || levels.Any(level => level == null || string.IsNullOrEmpty(level.Id))
                || levels.Select(level => level.Id).Distinct().Count() != levels.Count || Anchor == null) return false;
            reason = "Keep finite level spans and at least 0.3 m of default clearance on every level.";
            if (!float.IsFinite(thickness) || thickness < .02f || spans.Length != levels.Count
                || spans.Any(span => !float.IsFinite(span) || span - thickness < .2999f)) return false;
            // Finite spans can still overflow or collapse to the same float position.
            // Include the top roof plane in this check, not just the level floors.
            var elevations = new float[spans.Length + 1];
            int anchor = Levels.IndexOf(Anchor);
            for (int i = anchor; i < spans.Length; i++) elevations[i + 1] = elevations[i] + spans[i];
            for (int i = anchor - 1; i >= 0; i--) elevations[i] = elevations[i + 1] - spans[i];
            for (int i = 0; i < spans.Length; i++)
            {
                float floor = elevations[i] + thickness / 2;
                // Match BoundaryTop/SlabAt arithmetic, including the unshared roof.
                // At large elevations, nominal span subtraction can hide float collapse.
                float top = i + 1 == spans.Length ? floor + (spans[i] - thickness) + thickness
                    : elevations[i + 1] + thickness / 2;
                var slab = new HouseVerticalLayout.Slab(top, thickness, 0);
                if (!float.IsFinite(elevations[i]) || !float.IsFinite(elevations[i + 1]) || !float.IsFinite(top)
                    || elevations[i + 1] - elevations[i] - thickness < .2999f
                    || slab.Bottom - floor < .2999f) return false;
            }
            reason = "";
            return true;
        }

        public bool TryRemoveOpening(Boundary boundary, string id)
            => Boundaries.Contains(boundary) && boundary.RemoveOpening(id);

        public bool TrySetOpening(Boundary boundary, string id, IReadOnlyList<Vector2> points, out string reason)
        {
            if (!Boundaries.Contains(boundary))
            {
                reason = "Boundary no longer exists.";
                return false;
            }

            if (!OutlineGeometry.Validate(points, true, out reason))
                return false;
            var existing = boundary.Openings.FirstOrDefault(opening => opening.Id == id);
            if (existing != null && existing.Points.Count != points.Count
                && boundary.DividerNodes.Any(node => node.Attachment?.OpeningId == id))
            {
                reason = "Insert hole points explicitly before reshaping a hole with attached dividers.";
                return false;
            }
            boundary.SetOpening(id, points);
            return true;
        }
        // Read-only views prevent callers from bypassing the domain operations.
        // New houses keep the 20 cm slab envelope, with a 5 cm top finish.
        // Serialized houses retain their authored layer thicknesses.
        [SerializeField] private float floorThickness = .05f;
        [SerializeField] private float ceilingThickness = .15f;
        [SerializeField] private Material insideMaterial, outsideMaterial, floorMaterial;
        [SerializeField] private Material ceilingMaterial;
        [SerializeField] private WallTrim defaultTrim;
        [SerializeField] private bool hasDefaultTrim;
        public WallTrim DefaultTrim => hasDefaultTrim ? defaultTrim : null;
        internal void SetDefaultTrim(WallTrim value) { defaultTrim = value?.Copy(); hasDefaultTrim = value != null; }
        [SerializeField] private Vector2 ceilingTextureSize = new Vector2(2, 2);
        [SerializeField] private string anchorId;
        [SerializeField] private List<Level> levels = new List<Level>();
        [SerializeField] private List<Boundary> boundaries = new List<Boundary>();
        private ReadOnlyModelList<Level> levelView;
        private ReadOnlyModelList<Boundary> boundaryView;
        public float FloorThickness => floorThickness;
        public float CeilingThickness => ceilingThickness;
        public Material InsideMaterial => insideMaterial;
        public Material OutsideMaterial => outsideMaterial;
        public Material FloorMaterial => floorMaterial;
        public Material CeilingMaterial => ceilingMaterial;
        public Vector2 CeilingTextureSize => ceilingTextureSize;
        public IReadOnlyList<Level> Levels => ReadOnlyModelList<Level>.For(levels, ref levelView);
        public IReadOnlyList<Boundary> Boundaries => ReadOnlyModelList<Boundary>.For(boundaries, ref boundaryView);

        [Serializable]
        public sealed class Level
        {
            [SerializeField] private string id = Guid.NewGuid().ToString("N");
            [SerializeField] private HousePlan plan;
            [SerializeField] private Transform content;
            [SerializeField] private float span = 3;
            public string Id => id;
            public HousePlan Plan => plan;
            public Transform Content => content;
            public float Span => span;

            public Level(HousePlan plan = null, float span = 3, Transform content = null,
                string id = null)
            {
                this.plan = plan; this.span = span; this.content = content;
                this.id = id ?? Guid.NewGuid().ToString("N");
            }
            // Setup/reference remapping and domain publication only. Ordinary editor edits
            // validate a candidate through HouseBuilding before committing serialized data.
            internal void AssignContent(Transform value) => content = value;
            internal void Bind(HousePlan value, Transform contents) { plan = value; content = contents; }
            internal void SetSpan(float value) => span = value;
            internal Level Copy() => new Level(plan, span, content, id);

        }

        [Serializable]
        public sealed class Opening
        {
            [SerializeField] private string id = Guid.NewGuid().ToString("N");
            [SerializeField] private List<Vector2> points = new List<Vector2>();
            [SerializeField] private List<string> pointIds = new List<string>();
            private ReadOnlyModelList<Vector2> pointView;
            public string Id => id;
            public IReadOnlyList<Vector2> Points => ReadOnlyModelList<Vector2>.For(points, ref pointView);
            public Opening(IEnumerable<Vector2> points = null, string id = null)
            { this.id = id ?? Guid.NewGuid().ToString("N"); this.points = points?.ToList() ?? new List<Vector2>(); }
            // Getters and copying never initialize old data. A command assigns stable
            // point identities only when the contour is edited or first attached.
            internal void EnsurePointIds()
            {
                if (pointIds != null && pointIds.Count == points.Count) return;
                pointIds = points.Select(_ => Guid.NewGuid().ToString("N")).ToList();
            }
            internal string PointId(int index) => pointIds[index];
            internal bool HasPointIds => pointIds != null && pointIds.Count == points.Count;
            internal int PointIndex(string value) => pointIds?.IndexOf(value) ?? -1;
            internal Opening Copy() => new Opening(points, id) { pointIds = pointIds?.ToList() ?? new List<string>() };
            internal bool HasValidPointIds => pointIds == null || pointIds.Count == 0
                || pointIds.Count == points.Count && pointIds.All(value => !string.IsNullOrEmpty(value))
                    && pointIds.Distinct().Count() == pointIds.Count;
            internal void ReplacePoints(IEnumerable<Vector2> value)
            {
                var replacement = value.ToList();
                if (points.SequenceEqual(replacement)) return;
                points = replacement;
                EnsurePointIds();
            }
            internal void InsertPoint(int after, Vector2 point)
            {
                EnsurePointIds();
                points.Insert(after + 1, point);
                pointIds.Insert(after + 1, Guid.NewGuid().ToString("N"));
            }
        }

        [Serializable]
        public sealed partial class Boundary
        {
            [SerializeField] private string id = Guid.NewGuid().ToString("N");
            [SerializeField] private string lowerId;
            [SerializeField] private string upperId;
            [SerializeField] private List<Opening> openings = new List<Opening>();
            private ReadOnlyModelList<Opening> openingView;
            public string Id => id;
            // Unity reloads absent endpoints as empty strings. Null and empty
            // describe the same ground/roof absence to every reader.
            public string LowerId => lowerId ?? "";
            public string UpperId => upperId ?? "";
            public IReadOnlyList<Opening> Openings => ReadOnlyModelList<Opening>.For(openings, ref openingView);
            public Boundary(string lowerId = null, string upperId = null, string id = null)
            { this.lowerId = lowerId; this.upperId = upperId; this.id = id ?? Guid.NewGuid().ToString("N"); }
            internal void Reconnect(string lower, string upper) { lowerId = lower; upperId = upper; }
            internal void SetOpening(string existingId, IReadOnlyList<Vector2> points)
            {
                var opening = openings.Find(o => o.Id == existingId);
                if (opening == null) openings.Add(new Opening(points));
                else opening.ReplacePoints(points);
            }
            internal bool RemoveOpening(string openingId) => openings.RemoveAll(o => o.Id == openingId) > 0;
            internal void ReplaceLayout(Boundary value)
            {
                openings = value.Openings.Select(opening => opening.Copy()).ToList();
                ReplaceDividerGraph(value);
                ReplaceRegions(value.Regions);
            }

            // A repeated storey owns new records. Asset references are shared, but
            // attachments must never keep editing the source storey's graph.
            internal Boundary CopyForNewLevel(string sourceLevelId, string targetLevelId,
                IReadOnlyDictionary<string, string> cornerIds)
            {
                var next = new Boundary();
                var ids = new Dictionary<string, string>();
                foreach (var opening in openings)
                {
                    var copy = new Opening(opening.Points);
                    copy.EnsurePointIds();
                    ids.Add(opening.Id, copy.Id);
                    for (int i = 0; i < opening.Points.Count; i++)
                        if (opening.HasPointIds)
                            ids.Add(opening.PointId(i), copy.PointId(i));
                    next.openings.Add(copy);
                }
                foreach (var node in dividerNodes)
                {
                    var copy = new DividerNode(node.Position);
                    ids.Add(node.Id, copy.Id);
                    next.dividerNodes.Add(copy);
                }
                for (int i = 0; i < dividerNodes.Count; i++)
                {
                    var attachment = dividerNodes[i].Attachment;
                    if (attachment == null) continue;
                    bool wall = !string.IsNullOrEmpty(attachment.LevelId);
                    string start, end;
                    if (wall && attachment.LevelId == targetLevelId)
                    { start = attachment.StartId; end = attachment.EndId; }
                    else
                    {
                        var hosts = wall ? cornerIds : ids;
                        if (wall && attachment.LevelId != sourceLevelId
                            || !hosts.TryGetValue(attachment.StartId, out start)
                            || !hosts.TryGetValue(attachment.EndId, out end)) continue;
                    }
                    string hole = string.IsNullOrEmpty(attachment.OpeningId) ? null : ids[attachment.OpeningId];
                    next.dividerNodes[i].Attach(new DividerAttachment(wall ? targetLevelId : null, start, end, attachment.Fraction, hole));
                }
                next.dividers = dividers.Select(edge => new Divider(ids[edge.StartId], ids[edge.EndId])).ToList();
                next.regions = regions.Select(segment => segment.Copy(segment.FaceKey, keepId: false)).ToList();
                return next;
            }
            internal Boundary Copy() => new Boundary(lowerId, upperId, id)
            {
                openings = openings.ConvertAll(opening => opening.Copy()),
                dividerNodes = dividerNodes.ConvertAll(node => node.Copy()),
                dividers = dividers.ConvertAll(divider => divider.Copy()),
                regions = regions.ConvertAll(segment => segment.Copy(segment.FaceKey))
            };
        }

        internal void RegisterLevel(Level level, bool below = false)
        {
            if (level == null || levels.Any(l => l.Id == level.Id)) throw new InvalidOperationException("Use a distinct level identity.");
            if (levels.Count == 0) anchorId = level.Id;
            if (below) levels.Insert(0, level); else levels.Add(level);
        }
        internal void RegisterBoundary(Boundary boundary)
        {
            if (boundary == null || boundaries.Any(b => b.Id == boundary.Id)) throw new InvalidOperationException("Use a distinct slab boundary identity.");
            boundaries.Add(boundary);
        }
        internal bool CanRemoveLevel(Level level, out string reason)
        {
            reason = "Choose an existing level.";
            int index = Levels.IndexOf(level);
            if (index < 0) return false;
            reason = "Level 1 is the reference level and cannot be deleted.";
            if (level == Anchor) return false;
            reason = "Delete the levels above or below this one first.";
            if (index != 0 && index != Levels.Count - 1) return false;
            reason = "";
            return true;
        }

        internal bool TryRemoveLevel(Level level, out string reason)
        {
            if (!CanRemoveLevel(level, out reason)) return false;
            bool bottom = Levels.IndexOf(level) == 0;
            var shared = bottom ? Above(level) : Below(level);
            var extreme = bottom ? Below(level) : Above(level);
            reason = "The level needs its adjoining slab boundaries.";
            if (shared == null || extreme == null) return false;
            // Keep the remaining storey's slab, identities and payload; its height
            // does not change when the outermost storey is removed.
            shared.Reconnect(bottom ? null : shared.LowerId, bottom ? shared.UpperId : null);
            boundaries.Remove(extreme);
            levels.Remove(level);
            reason = "";
            return true;
        }
        internal void ConfigureDefaultMaterials(Material inside, Material outside, Material floor)
        { insideMaterial = inside; outsideMaterial = outside; floorMaterial = floor; }

        internal void ConfigureCeilingFinish(Material material, Vector2 repeat)
        { ceilingMaterial = material; ceilingTextureSize = repeat; }

        internal bool TryValidateLayout(out string reason)
        {
            if (!BooleanVolumesValid(out reason)) return false;
            reason = "Keep finite slab thicknesses of at least 0.01 m.";
            if (!float.IsFinite(floorThickness) || !float.IsFinite(ceilingThickness)
                || floorThickness < .01f || ceilingThickness < .01f) return false;
            if (!ValidSpans(levels?.Select(level => level?.Span ?? float.NaN).ToArray()
                ?? Array.Empty<float>(), SlabThickness, out reason)) return false;
            reason = "Keep one slab boundary between each adjacent level, plus the ground and roof boundaries.";
            if (boundaries == null || boundaries.Count != levels.Count + 1
                || boundaries.Any(b => b == null || string.IsNullOrEmpty(b.Id))
                || boundaries.Select(b => b.Id).Distinct().Count() != boundaries.Count) return false;
            foreach (var boundary in boundaries)
                if (!boundary.TryValidateSurfaceData(out reason)) return false;
            // Ground and roof have one absent endpoint.
            // Boundary ordering is irrelevant; ownership comes from the level IDs.
            for (int i = 0; i <= levels.Count; i++)
            {
                string lower = i == 0 ? "" : levels[i - 1].Id;
                string upper = i == levels.Count ? "" : levels[i].Id;
                if (boundaries.Count(b => b.LowerId == lower && b.UpperId == upper) != 1) return false;
            }
            reason = "";
            return true;
        }
    }
}
