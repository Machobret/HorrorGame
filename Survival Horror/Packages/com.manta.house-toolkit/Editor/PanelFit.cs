using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class PanelFit
    {
        // Work in metres. Tolerate mesh import roundoff at an exact seam.
        const double Epsilon = .000001;

        // Short gaps beside a doorway may have to be narrow. Only runs large enough
        // for multiple bays establish the shared spacing for the rest of the wall.
        internal static float EvenTarget(float length, List<FittedOpening> openings, float nominal)
        {
            float target = nominal;
            float start = 0;
            foreach (var opening in openings)
            {
                Include(opening.left - start);
                start = opening.Right;
            }
            Include(length - start);
            return target;

            void Include(float run)
            {
                if (run <= nominal) return;
                target = Mathf.Min(target, run / MinimumCount(run, nominal));
            }
        }

        static int MinimumCount(float run, float nominal) =>
            Math.Max(1, (int)Math.Ceiling((run - Epsilon) / nominal));

        internal static int EvenCount(float run, float nominal, float target)
        {
            int fewer = Math.Max(MinimumCount(run, nominal), Mathf.FloorToInt(run / target));
            float largerBay = run / fewer;
            float smallerBay = run / (fewer + 1);
            return Mathf.Abs(smallerBay - target) < Mathf.Abs(largerBay - target) ? fewer + 1 : fewer;
        }

        internal static List<WallSegment> Choose(WallSegment[] panels, float run)
        {
            if (panels.Length == 1)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(run / panels[0].size.x));
                return Enumerable.Repeat(panels[0], count).ToList();
            }
            var choices = panels.OrderByDescending(panel => panel.size.x)
                .GroupBy(panel => panel.size.x).Select(group => group.First()).ToArray();
            var widths = choices.Select(panel => (double)panel.size.x).ToArray();
            var counts = new int[choices.Length];
            var best = new int[choices.Length];
            double bestWidth = double.PositiveInfinity;
            int bestCount = int.MaxValue;
            double smallest = widths[widths.Length - 1];
            double lowerBound = widths.All(width => width % smallest == 0)
                ? Math.Max(1, Math.Ceiling((run - Epsilon) / smallest)) * smallest
                : run;

            void Consider(int index, double total, int count)
            {
                if (total > bestWidth + Epsilon || (total >= bestWidth - Epsilon && count >= bestCount)) return;
                bestWidth = total;
                bestCount = count;
                Array.Copy(counts, best, index + 1);
                Array.Clear(best, index + 1, best.Length - index - 1);
            }

            void Search(int index, double covered, int used)
            {
                double width = widths[index];
                int enough = Math.Max(1, (int)Math.Ceiling((run - covered - Epsilon) / width));
                counts[index] = enough;
                Consider(index, covered + enough * width, used + enough);
                if (index + 1 == widths.Length) return;

                // Try wider panels first. Once a branch needs at least as many pieces
                // as a fit at the smallest possible total width, every branch using fewer of this width can be skipped.
                for (int n = enough - 1; n >= 0; n--)
                {
                    double total = covered + n * width;
                    int minimumMore = (int)Math.Ceiling((run - total - Epsilon) / widths[index + 1]);
                    if (bestWidth <= lowerBound + Epsilon && used + n + minimumMore >= bestCount) break;
                    counts[index] = n;
                    Search(index + 1, total, used + n);
                }
            }

            Search(0, 0, 0);
            var result = new List<WallSegment>(bestCount);
            for (int i = 0; i < choices.Length; i++)
                for (int n = 0; n < best[i]; n++) result.Add(choices[i]);
            return result;
        }
    }
}
