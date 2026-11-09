using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseHeightWalls
    {
        internal struct Section
        {
            // Top is the ceiling underside used for headroom validation. CoverageTop
            // includes the slab edge; it must never make an opening fit a low ceiling.
            public float From, To, Top, CoverageTop;
        }

        internal static List<Section> FitSections(List<Section> logical, Vector2 wallStart, Vector2 wallEnd,
            Vector2 spanStart, Vector2 spanEnd)
        {
            var delta = wallEnd - wallStart;
            if (logical.Count == 0 || delta.sqrMagnitude < .000001f) return new List<Section>();
            float from = Vector2.Dot(spanStart - wallStart, delta) / delta.sqrMagnitude;
            float to = Vector2.Dot(spanEnd - wallStart, delta) / delta.sqrMagnitude;
            if (Mathf.Abs(to - from) < .000001f) return new List<Section>();
            var cuts = new List<float> { 0, 1 };
            for (int i = 1; i < logical.Count; i++)
            {
                float cut = (logical[i].From - from) / (to - from);
                if (cut > .00001f && cut < .99999f) cuts.Add(cut);
            }
            cuts.Sort();
            var result = new List<Section>();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                // Junction fitting can extend past either authored endpoint. Continue
                // that wall's terminal height, rather than sample a neighbouring room.
                float middle = Mathf.Clamp01(Mathf.Lerp(from, to, (cuts[i] + cuts[i + 1]) / 2));
                int index = logical.FindIndex(s => middle <= s.To);
                // Sub-millimetre terminal intervals can be omitted during sectioning.
                var section = logical[index < 0 ? logical.Count - 1 : index];
                section.From = cuts[i];
                section.To = cuts[i + 1];
                result.Add(section);
            }
            return result;
        }

        internal static List<Section> Sections(HouseStack building, HouseStack.Level level,
            Vector2 start, Vector2 end, float defaultHeight, Clipper2Lib.PathsD coverage)
        {
            var cuts = new List<float>{0, 1};
            var delta = end - start;
            if (building != null && level != null)
            {
                // A slab may stop at an outdoor wall. Only its covered span needs
                // the extra facade height concealing the exposed slab edge.
                foreach (var path in coverage)
                    for (int i = 0; i < path.Count; i++)
                        SplitAt(new Vector2((float)path[i].x, (float)path[i].y),
                            new Vector2((float)path[(i + 1) % path.Count].x, (float)path[(i + 1) % path.Count].y));
            }

            void SplitAt(Vector2 a, Vector2 b)
            {
                var side = b - a;
                float denominator = Cross(delta, side);
                if (Mathf.Abs(denominator) < .000001f)
                {
                    // A wall along a segment seam must still split at that seam's ends.
                    if (Mathf.Abs(Cross(a - start, delta)) < .000001f && delta.sqrMagnitude > 0)
                    {
                        AddCut(Vector2.Dot(a - start, delta) / delta.sqrMagnitude);
                        AddCut(Vector2.Dot(b - start, delta) / delta.sqrMagnitude);
                    }
                    return;
                }
                float t = Cross(a - start, side) / denominator, u = Cross(a - start, delta) / denominator;
                if (u >= -.00001f && u <= 1.00001f) AddCut(t);
            }
            void AddCut(float t) { if (t > .00001f && t < .99999f) cuts.Add(t); }

            cuts = cuts.Distinct().OrderBy(t => t).ToList();
            var result = new List<Section>();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                if ((cuts[i + 1] - cuts[i]) * delta.magnitude < .001f) continue;
                var point = start + delta * ((cuts[i] + cuts[i + 1]) / 2);
                var section = new Section
                {
                    From = cuts[i], To = cuts[i + 1],
                    Top = building == null || level == null ? defaultHeight : building.WallHeight(level)
                };
                section.CoverageTop = building == null || level == null ? section.Top
                    : HouseHeightQueries.WallTop(building, level, point, coverage, section.Top);
                if (result.Count > 0)
                {
                    var last = result[result.Count - 1];
                    if (Mathf.Abs(last.Top - section.Top) < .0001f
                        && Mathf.Abs(last.CoverageTop - section.CoverageTop) < .0001f)
                    {
                        last.To = section.To;
                        result[result.Count - 1] = last;
                        continue;
                    }
                }
                result.Add(section);
            }
            return result;
        }

        internal static float CornerTop(HousePlan plan, HouseWallLayout.CornerCover cover, HouseGenerationData data)
        {
            // Both walls start on the same level plane. Cover only their shared height.
            float top = Mathf.Min(TerminalTop(cover.WallA), TerminalTop(cover.WallB));
            return cover.CoveringWall != null && TerminalTop(cover.CoveringWall) >= top - .0001f ? 0 : top;
            float TerminalTop(string wallId)
            {
                var wall = plan.Data.Walls.First(w => w.Id == wallId);
                var start = plan.Data.GetPosition(wall.StartId); var end = plan.Data.GetPosition(wall.EndId);
                var sections = data.HeightSections(plan, start, end);
                return sections.Count == 0 ? 0 : ((start - cover.Node).sqrMagnitude < (end - cover.Node).sqrMagnitude
                    ? sections[0] : sections[sections.Count - 1]).CoverageTop;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        internal static string Signature(HousePlan plan, IEnumerable<HouseWallLayout.Span> spans, HouseGenerationData data)
        {
            if (data.Building == null)
                return "";
            var pieces = spans.Select(span => (span, sections: data.HeightSections(plan, span))).ToArray();
            // Keep healthy modules and their manual mesh overrides when height fitting
            // produces their original dimensions, regardless of House metadata changes.
            if (pieces.All(piece => piece.sections.All(s =>
                Mathf.Abs(s.Top - piece.span.Definition.Height) < .0001f && Mathf.Abs(s.CoverageTop - s.Top) < .0001f))) return "";
            string signature = string.Join("|", pieces.SelectMany(piece => piece.sections).Select(s => $"{s.From:R},{s.To:R},0,{s.Top:R},{s.CoverageTop:R}"));
            var definitions = new HashSet<WallModuleDefinition>(pieces.Select(piece => piece.span.Definition));
            foreach (var piece in pieces)
            {
                if (!piece.sections.Any(section =>
                    section.Top > piece.span.Definition.Height + .0001f)) continue;
                var wall = plan.Data.Walls.First(w => w.Id == piece.span.WallId);
                var blank = HouseWallLayout.DefinitionFor(plan.Data, wall, plan.GetComponent<HouseWallBuilder>().Data);
                // Opening-only walls can emit blank bands absent from the horizontal
                // span inputs. Hash that dependency once; ordinary blank walls already do.
                if (blank != null && definitions.Add(blank))
                    signature += "|blank:" + data.KitSignature(blank);
            }
            return signature;
        }

        internal static string Validate(HousePlan plan, HouseWallLayout.Result layout, HouseGenerationData data)
        {
            var building = data.Building;
            if (building == null)
                return "";
            if (data.SlabProblem.Length > 0) return data.SlabProblem;
            foreach (var span in layout.Spans)
            {
                if (!float.IsFinite(span.Definition.Height) || span.Definition.Height < .01f)
                    return "Height-fitted wall modules must be at least 0.01 m tall.";
                var sections = data.HeightSections(plan, span);
                if (sections.Any(s => s.Top < .3f))
                    return "A floor/ceiling change leaves less than 0.3 m of wall height.";
                if (span.DoorwayId == null)
                    continue;
                if (sections.Any(section => section.Top < span.Definition.OpeningOutline.yMax + .01f))
                    return "The level ceiling intersects a door/window opening. Increase the level height or move/remove that opening.";
            }

            return "";
        }

        internal static void Place(Transform parent, HousePlan plan, HouseWallLayout.Span span, HouseGenerationData data, float topInset = 0)
        {
            var kit = plan.GetComponent<HouseWallBuilder>().Data;
            var wall = plan.Data.Walls.First(w => w.Id == span.WallId);
            var blank = HouseWallLayout.DefinitionFor(plan.Data, wall, kit);
            var blankFamily = kit.BlanksFor(plan.Data, wall);
            foreach (var section in data.HeightSections(plan, span))
            {
                var start = Vector2.Lerp(span.Start, span.End, section.From);
                var end = Vector2.Lerp(span.Start, span.End, section.To);
                var part = new HouseWallLayout.Span{WallId = span.WallId, DoorwayId = span.DoorwayId, OpeningKind = span.OpeningKind, Insert = span.Insert, Depths = span.Depths, Start = start, End = end, Definition = span.Definition};
                // Only the rendered solid head changes. Opening anchors, logical
                // headroom and the conservative collision height stay unchanged.
                float top = section.Top - topInset;
                float extension = section.CoverageTop - section.Top;
                Band(parent, part, 0, Mathf.Min(top, part.Definition.Height),
                    part.Definition.Height >= top - .0001f ? extension : 0, blankFamily);
                if (blank == null || !float.IsFinite(blank.Height) || blank.Height < .01f
                    || span.Definition.Height <= 0)
                    continue; // Imported invalid definitions must never stall a stacking loop.
                // Above a door use solid blank geometry, never another doorway/window body.
                part.Definition = blank;
                part.DoorwayId = null;
                part.Insert = null;
                for (float y = span.Definition.Height; y < top - .0001f; y += blank.Height)
                {
                    if (y + blank.Height <= y) break;
                    Band(parent, part, y, Mathf.Min(blank.Height, top - y),
                        y + blank.Height >= top - .0001f ? extension : 0, blankFamily);
                }

            }
        }

        internal static void Band(Transform parent, HouseWallLayout.Span span, float bottom, float height,
            float topExtension = 0, IEnumerable<WallModuleDefinition> blankFamily = null)
        {
            if (height <= .0001f)
                return;
            int first = parent.childCount;
            HouseWallOutputBuilder.PlaceSpan(parent, span, height, topExtension, blankFamily);
            var root = parent.GetChild(first);
            root.localPosition += Vector3.up * bottom;
        }
    }
}
