using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    internal sealed class WallDecalLayout
    {

        internal readonly struct Choice
        {
            public readonly WallDecalAtlas atlas;
            public readonly WallDecalAtlas.Poster poster;
            public Choice(WallDecalAtlas atlas, WallDecalAtlas.Poster poster) { this.atlas = atlas; this.poster = poster; }
        }

        internal static List<Choice> Choices(WallDecalAtlas[] atlases, string theme)
        {
            var choices = new List<Choice>();
            if (atlases == null) return choices;
            foreach (var atlas in atlases)
            {
                if (!atlas || !atlas.material) continue;
                foreach (var poster in atlas.posters)
                    if (poster != null && !string.IsNullOrEmpty(poster.id) && Positive(poster.size) &&
                        (string.IsNullOrEmpty(theme) || Array.Exists(poster.themes ?? Array.Empty<string>(),
                            t => string.Equals(t, theme, StringComparison.OrdinalIgnoreCase))))
                        choices.Add(new Choice(atlas, poster));
            }
            return choices;
        }

        static bool Positive(Vector2 size) => float.IsFinite(size.x) && float.IsFinite(size.y) && size.x > 0 && size.y > 0;

    }
}
