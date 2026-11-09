using System;
using System.Collections.Generic;

namespace EnvironmentAuthoring
{
    // Small queries for read-only model collections; edits remain on their owning model.
    internal static class AuthoringListQueries
    {
        internal static int IndexOf<T>(this IReadOnlyList<T> values, T value)
        { for (int i = 0; i < values.Count; i++) if (EqualityComparer<T>.Default.Equals(values[i], value)) return i; return -1; }
        internal static int FindIndex<T>(this IReadOnlyList<T> values, Predicate<T> match)
        { for (int i = 0; i < values.Count; i++) if (match(values[i])) return i; return -1; }
        internal static T Find<T>(this IReadOnlyList<T> values, Predicate<T> match)
        { int index = values.FindIndex(match); return index < 0 ? default : values[index]; }
        internal static bool Exists<T>(this IReadOnlyList<T> values, Predicate<T> match) => values.FindIndex(match) >= 0;
    }
}
