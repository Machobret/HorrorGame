using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // A half-metre is the crop boundary, not a maximum allowed stretch percentage.
    internal static class WallBlankLengths
    {
        internal readonly struct Piece
        {
            internal readonly WallModuleDefinition Definition;
            internal readonly float Length;
            internal readonly WallModule.LengthFit Fit;
            internal Piece(WallModuleDefinition definition, float length, WallModule.LengthFit fit = WallModule.LengthFit.ScaleNative)
            { Definition = definition; Length = length; Fit = fit; }
        }

        internal static List<Piece> Fit(WallModuleDefinition primary, float length,
            IEnumerable<WallModuleDefinition> blankFamily = null)
        {
            // The caller knows the authored family. A shared primary asset cannot
            // identify it when interior and exterior slots use different variants.
            var sources = (blankFamily ?? new[] { primary }).Where(d => d != null)
                .Distinct().OrderByDescending(d => d.Width).ToArray();
            var smallest = sources[sources.Length - 1];
            if (length < .5f && length < smallest.Width)
                return new List<Piece> { new Piece(smallest, length, WallModule.LengthFit.Cropped) };
            // Family widths are half-metre multiples. Try the two surrounding native
            // totals, then distribute one scale across their pieces to avoid a tiny tail.
            float units = length / smallest.Width;
            int low = Mathf.Max(1, Mathf.FloorToInt(units)), high = Mathf.Max(1, Mathf.CeilToInt(units));
            float lowError = Mathf.Abs(units / low - 1), highError = Mathf.Abs(units / high - 1);
            int count = lowError <= highError ? low : high;
            if (count > 4096) throw new InvalidOperationException("Use a wider wall family: at most 4096 native units fit in one span.");
            if (sources.Length == 1)
                return Enumerable.Range(0, count).Select(_ => new Piece(smallest, length / count)).ToList();

            // Valid mixed families represent half-metre units. Imported widths may
            // differ by the validation tolerance; subtracting those floats can drop
            // a whole final piece. Choose in nominal units, then scale the actual sum.
            int remainingUnits = count * Mathf.RoundToInt(smallest.Width * 2);
            var selected = new List<WallModuleDefinition>();
            float nativeLength = 0;
            foreach (var source in sources)
            {
                int sourceUnits = Mathf.RoundToInt(source.Width * 2);
                int repeats = remainingUnits / sourceUnits;
                for (int i = 0; i < repeats; i++) selected.Add(source);
                nativeLength += repeats * source.Width;
                remainingUnits -= repeats * sourceUnits;
            }
            float scale = length / nativeLength;
            return selected.Select(source => new Piece(source, source.Width * scale)).ToList();
        }
    }
}
