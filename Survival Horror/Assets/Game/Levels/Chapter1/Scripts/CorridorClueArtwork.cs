using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    internal static class CorridorClueArtwork
    {
        // Unit assignments are also used to print the fixed guard-office register.
        private static readonly string[] Tenants =
        {
            "Vincent Hale", "Miriam Bell", "Daniel Mercer", "Elena Ortiz", "Caleb Asher", "Nora Price",
            "Simon Reed", "Leah Sutton", "Helen Cho", "Arthur Boone", "Rosa Delgado", "Edwin Park"
        };
        private const string UnitIntro = "Need an electrician to check these storage units out:";
        private const string TenantIntro = "Need an electrician to check the storage units rented by:";
        private const string Signature = "-Mr. Johnson";
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ";
        private const int GlyphWidth = 24;
        private const int GlyphHeight = 48;
        private const int GlyphsPerRow = 16;

        public static List<DocumentPage> Create(bool[] solution, int[] bayNumbers, bool useTenants,
            Texture2D paper, Texture2D glyphs, out Texture2D texture, out Sprite sprite)
        {
            var entries = new List<string>();
            for (int number = 1; number <= solution.Length; number++)
            {
                int index = System.Array.IndexOf(bayNumbers, number);
                if (index >= 0 && solution[index])
                    entries.Add(useTenants ? Tenants[number - 1] : number.ToString());
            }

            var pixels = paper.GetPixels32();
            var ink = glyphs.GetPixels32();
            for (int i = 0; i < entries.Count; i++)
            {
                string entry = entries[i];
                for (int character = 0; character < entry.Length; character++)
                    StampGlyph(pixels, paper.width, paper.height, ink, glyphs.width, glyphs.height,
                        Alphabet.IndexOf(entry[character]), 111 + character * GlyphWidth, 306 + i * 73);
            }

            texture = new Texture2D(paper.width, paper.height, TextureFormat.RGBA32, false);
            texture.name = "Corridor clue page";
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);

            return new List<DocumentPage>
            {
                new DocumentPage
                {
                    Text = new LocalizableText
                    {
                        Unlocalized = GameText.Translate(useTenants ? TenantIntro : UnitIntro)
                            + "\n\n" + string.Join("\n", entries)
                    },
                    Caption = new LocalizableText { Unlocalized = Signature },
                    Artwork = sprite
                }
            };
        }

        private static void StampGlyph(Color32[] paper, int width, int height, Color32[] atlas,
            int atlasWidth, int atlasHeight, int glyph, int left, int top)
        {
            if (glyph < 0) return;
            int sourceX = glyph % GlyphsPerRow * GlyphWidth;
            int sourceTop = glyph / GlyphsPerRow * GlyphHeight;
            for (int y = 0; y < GlyphHeight; y++)
            {
                int source = (atlasHeight - 1 - sourceTop - y) * atlasWidth + sourceX;
                int destination = (height - 1 - top - y) * width + left;
                for (int x = 0; x < GlyphWidth; x++)
                {
                    Color32 letter = atlas[source + x];
                    if (letter.a == 0) continue;
                    paper[destination + x] = Color32.Lerp(paper[destination + x], letter, letter.a / 255f);
                    paper[destination + x].a = 255;
                }
            }
        }
    }
}
