using System;
using System.Collections.Generic;

namespace Game.Editor.AssetLibrary
{
    internal sealed class LibraryGridCard
    {
        public LibraryEntry Entry { get; }
        public IReadOnlyList<LibraryEntry> Variants { get; }
        public int VariantCount => Variants.Count;
        public bool Expanded { get; }
        public bool IsFamily => VariantCount > 1;

        internal LibraryGridCard(LibraryEntry entry, IReadOnlyList<LibraryEntry> variants = null, bool expanded = false)
        {
            Entry = entry;
            Variants = variants ?? Array.Empty<LibraryEntry>();
            Expanded = expanded;
        }

        public static List<LibraryGridCard> Build(IReadOnlyList<LibraryEntry> matches,
            IReadOnlyDictionary<string, bool> expansion, bool expandByDefault)
        {
            var families = new Dictionary<string, List<LibraryEntry>>();
            foreach (var entry in matches)
            {
                if (string.IsNullOrEmpty(entry.family)) continue;
                if (!families.TryGetValue(entry.family, out var members))
                    families.Add(entry.family, members = new List<LibraryEntry>());
                members.Add(entry);
            }

            var cards = new List<LibraryGridCard>(matches.Count);
            foreach (var entry in matches)
            {
                if (string.IsNullOrEmpty(entry.family) || families[entry.family].Count == 1)
                {
                    // A single matching variant is directly selectable and draggable from search.
                    cards.Add(new LibraryGridCard(entry));
                    continue;
                }

                var members = families[entry.family];
                if (members[0] != entry) continue;
                bool open = expansion.TryGetValue(entry.family, out bool requested) ? requested : expandByDefault;
                cards.Add(new LibraryGridCard(entry, members, open));
            }
            return cards;
        }
    }

    internal sealed class LibraryGridLayout
    {
        internal readonly struct Row
        {
            public readonly int First, Count;
            public readonly float Top;
            public readonly bool InSection;

            public Row(int first, int count, float top, bool inSection)
            {
                First = first;
                Count = count;
                Top = top;
                InSection = inSection;
            }
        }

        internal readonly struct Section
        {
            public readonly LibraryGridCard Family;
            public readonly float Top, Bottom;

            public Section(LibraryGridCard family, float top, float bottom)
            {
                Family = family;
                Top = top;
                Bottom = bottom;
            }
        }

        public const float SectionInset = 8;
        public const float SectionHeader = 28;
        public readonly List<Row> Rows = new();
        public readonly List<Section> Sections = new();
        public readonly List<LibraryGridCard> Cells = new();
        public readonly List<LibraryEntry> ThumbnailEntries = new();
        public readonly int Columns;
        public readonly float RowHeight, Height;

        public LibraryGridLayout(IReadOnlyList<LibraryGridCard> cards, int columns, float rowHeight)
        {
            Columns = columns;
            RowHeight = rowHeight;
            float top = 0;
            for (int start = 0; start < cards.Count; start += columns)
            {
                int end = Math.Min(start + columns, cards.Count);
                Rows.Add(new Row(Cells.Count, end - start, top, false));
                for (int i = start; i < end; i++) Cells.Add(cards[i]);
                top += rowHeight;
                for (int i = start; i < end; i++)
                {
                    var family = cards[i];
                    if (!family.Expanded) continue;
                    float sectionTop = top + 2;
                    top = sectionTop + SectionHeader;
                    for (int first = 0; first < family.VariantCount; first += columns)
                    {
                        int count = Math.Min(columns, family.VariantCount - first);
                        Rows.Add(new Row(Cells.Count, count, top, true));
                        for (int j = first; j < first + count; j++)
                            Cells.Add(new LibraryGridCard(family.Variants[j]));
                        top += rowHeight;
                    }
                    float bottom = top + 4;
                    Sections.Add(new Section(family, sectionTop, bottom));
                    top = bottom + 8;
                }
            }
            Height = top;
            foreach (var card in Cells) ThumbnailEntries.Add(card.Entry);
        }

        public int FirstRowAfter(float y)
        {
            int first = 0, end = Rows.Count;
            while (first < end)
            {
                int middle = first + (end - first) / 2;
                if (Rows[middle].Top <= y) first = middle + 1;
                else end = middle;
            }
            return first;
        }
    }
}
