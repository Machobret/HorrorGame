using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EnvironmentAuthoring
{
    // Saved lists stay owned by their model. Readers get a cached standard read-only view.
    internal sealed class ReadOnlyModelList<T> : ReadOnlyCollection<T>
    {
        private readonly List<T> source;
        private ReadOnlyModelList(List<T> source) : base(source) => this.source = source;

        internal static IReadOnlyList<T> For(List<T> source, ref ReadOnlyModelList<T> view)
        {
            if (source == null) return Array.Empty<T>();
            // Domain edits and JSON overwrite can replace a list; ordinary reads allocate nothing.
            if (view == null || view.source != source) view = new ReadOnlyModelList<T>(source);
            return view;
        }
    }
}
