using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Run with PowerShell Add-Type -Path Tools/Art/BuildReporterMenuPreview.cs -ReferencedAssemblies System.Drawing.
public static class BuildReporterMenuPreview
{
    const string Source = "ArtSource/ReporterMenu/";
    const string Output = "Assets/Game/UI/ReporterMenu/Preview/";
    const string TabletPickup = "Assets/Game/Levels/Chapter1/Assets/MapTablet_Pickup.png";

    public static void Run(string project)
    {
        string template = ImporterTemplate(project);
        UpdateSurfaces(project, template);
        Import(project, template, Source + "PlayerId_PortraitPlaceholder.png", "MaverickPortrait");
        Import(project, template, "Assets/Game/Weapons/Revolver/Models/Revolver_Icon.png", "Revolver");
        Import(project, template, "Assets/AssetStore/HorrorEngine/ExampleContent/Items/Flashlight/Flashlight.png", "Flashlight");
        Import(project, template, "Assets/Game/Weapons/Ammunition/Shared/Models/AmmoSides/RevolverAmmo_PickupIcon.png", "Ammo");
        Import(project, template, "Assets/AssetStore/HorrorEngine/ExampleContent/Items/Batteries/Icon_Batteries_C.png", "Batteries");
        UpdateTabletPickup(project);
        Import(project, template, "Assets/Game/UI/InventoryFolder/Textures/Inventory_TornNotebook.png", "Notebook", trim: true);
        Import(project, template, "Assets/Game/UI/InventoryFolder/Textures/Inventory_ManilaFolder.png", "ReferenceFolder", trim: true);
        MakeNote(project, template, "InterviewLog");
        MakeNote(project, template, "RouteNotes");
        using (var pin = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(pin))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(32, 31);
                graphics.RotateTransform(39);
                graphics.FillRectangle(Brushes.White, -10, -23, 20, 7);
                graphics.FillRectangle(Brushes.White, -7, -16, 14, 16);
                graphics.FillRectangle(Brushes.White, -13, -1, 26, 6);
                graphics.FillPolygon(Brushes.White, new[] { new Point(-3, 4), new Point(3, 4), new Point(0, 24) });
            }
            Convert(pin, new Rectangle(0, 0, 64, 64), project, template, "Pin");
        }
        UpdateItemHalo(project);
    }

    public static void UpdateSurfaces(string project)
    {
        UpdateSurfaces(project, ImporterTemplate(project));
    }

    public static void UpdateRowSurfaces(string project)
    {
        UpdateSurfaces(project, ImporterTemplate(project), rowsOnly: true);
    }

    public static void UpdateTabletPickup(string project)
    {
        string destination = Path.Combine(project, TabletPickup);
        if (!File.Exists(destination + ".meta"))
            throw new InvalidOperationException("The tablet pickup requires its existing Unity metadata.");
        using (var master = new Bitmap(Path.Combine(project, "ArtSource/Tablet/MapTablet_Pickup.png")))
            SaveHalfSize(master, new Rectangle(0, 0, master.Width, master.Height), destination);
    }

    public static void UpdateChevronMarker(string project)
    {
        string template = ImporterTemplate(project).Replace("filterMode: 0", "filterMode: 1");
        MakeSurface(project, template, null, "ChevronMarker", 64, 64, Color.White, 14);
    }

    public static void UpdateItemHalo(string project)
    {
        string template = ImporterTemplate(project).Replace("filterMode: 0", "filterMode: 1");
        const int size = 128;
        double radius = size / 2.0 - 2;
        using (var halo = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double dx = (x + .5 - size / 2.0) / radius;
                double dy = (y + .5 - size / 2.0) / radius;
                double falloff = Math.Max(0, 1 - dx * dx - dy * dy);
                halo.SetPixel(x, y, Color.FromArgb((int)(255 * falloff * falloff), Color.White));
            }
            Convert(halo, new Rectangle(0, 0, size, size), project, template, "ItemHalo");
        }
    }

    public static void UpdateEquippedHand(string project)
    {
        string template = ImporterTemplate(project).Replace("filterMode: 0", "filterMode: 1");
        Point[] silhouette =
        {
            new Point(8, 22), new Point(5, 19), new Point(2, 14), new Point(3, 12),
            new Point(5, 12), new Point(8, 15), new Point(8, 5), new Point(9, 4),
            new Point(11, 5), new Point(11, 11), new Point(12, 2), new Point(14, 2),
            new Point(15, 3), new Point(15, 11), new Point(16, 4), new Point(18, 4),
            new Point(19, 5), new Point(19, 12), new Point(20, 8), new Point(22, 8),
            new Point(23, 9), new Point(23, 15), new Point(22, 19), new Point(20, 22),
            new Point(20, 24), new Point(9, 24)
        };
        using (var hand = new Bitmap(72, 72, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(hand))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.ScaleTransform(2.7f, 2.7f);
                graphics.TranslateTransform(1, 0.5f);
                graphics.FillPolygon(Brushes.White, silhouette);
            }
            Convert(hand, new Rectangle(0, 0, 72, 72), project, template, "EquippedHand");
        }
    }

    static string ImporterTemplate(string project)
    {
        Directory.CreateDirectory(Path.Combine(project, Output));
        string template = File.ReadAllText(Path.Combine(project, "Assets/Game/UI/InventoryFolder/Textures/Inventory_TornNotebook.png.meta"));
        template = Regex.Replace(template, @"filterMode: \d", "filterMode: 0");
        template = Regex.Replace(template, @"textureCompression: \d", "textureCompression: 0");
        return template.Replace("spriteGenerateFallbackPhysicsShape: 1", "spriteGenerateFallbackPhysicsShape: 0");
    }

    static void UpdateSurfaces(string project, string template, bool rowsOnly = false)
    {
        var graphite = Color.FromArgb(47, 44, 40);
        var edge = Color.FromArgb(139, 121, 101);
        var paper = Color.FromArgb(225, 213, 191);
        var header = Color.FromArgb(70, 66, 58);
        var oxblood = Color.FromArgb(101, 49, 44);
        using (var noise = new Bitmap(128, 128, PixelFormat.Format32bppArgb))
        {
            var random = new Random(617);
            for (int y = 0; y < noise.Height; y++)
            for (int x = 0; x < noise.Width; x++)
                noise.SetPixel(x, y, Color.FromArgb(random.Next(0, 11), random.Next(2) == 0 ? Color.Black : Color.White));
            using (var grain = new TextureBrush(noise, WrapMode.Tile))
            {
                grain.ScaleTransform(2, 2);
                if (!rowsOnly)
                {
                    MakeSurface(project, template, grain, "PressCard", 1706, 862, paper, 28, edge, 11, masthead: oxblood);
                    string sliced = template.Replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}", "spriteBorder: {x: 24, y: 24, z: 24, w: 24}");
                    MakeSurface(project, sliced, grain, "Satchel", 1072, 1310, graphite, 27, edge, 10);
                    MakeSurface(project, template, grain, "LedgerPage", 956, 1600, paper, 25, edge, 9);
                }
                const int radius = 4;
                string border = string.Format("spriteBorder: {{x: {0}, y: {0}, z: {0}, w: {0}}}", radius);
                string row = template.Replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}", border);
                MakeSurface(project, row, grain, "Category", 192, 72, header, radius * 2);
                MakeSurface(project, row, grain, "Selection", 192, 72, oxblood, radius * 2);
                MakeSurface(project, row, grain, "ActionPaper", 192, 72, paper, radius * 2);
                MakeSurface(project, row, null, "ConditionBadge", 64, 32, Color.White, radius * 2);
            }
        }
    }

    static void MakeSurface(string project, string template, Brush grain, string name, int width, int height,
        Color stock, int radius, Color edge = default(Color), int rim = 0, Color masthead = default(Color))
    {
        var bounds = new Rectangle(0, 0, width, height);
        using (var art = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(art))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (rim > 0)
                    FillRounded(g, bounds, radius, edge, grain, Color.Empty);
                FillRounded(g, Rectangle.Inflate(bounds, -rim, -rim), radius - rim, stock, grain, masthead);
            }
            Convert(art, bounds, project, template, name);
        }
    }

    static void FillRounded(Graphics g, Rectangle rect, int radius, Color color, Brush grain, Color masthead)
    {
        int d = radius * 2;
        using (var outline = new GraphicsPath())
        using (var fill = new SolidBrush(color))
        {
            outline.AddArc(rect.X, rect.Y, d, d, 180, 90);
            outline.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            outline.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            outline.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            outline.CloseFigure();
            g.FillPath(fill, outline);
            if (!masthead.IsEmpty)
            {
                var state = g.Save();
                g.SetClip(new Rectangle(rect.X, rect.Y, rect.Width, (int)(rect.Height * .138f)));
                using (var banner = new SolidBrush(masthead))
                    g.FillPath(banner, outline);
                g.Restore(state);
            }
            if (grain != null)
                g.FillPath(grain, outline);
        }
    }

    static void MakeNote(string root, string template, string name)
    {
        string fixture = Path.Combine(root, "Assets/Game/Levels/Chapter1/Assets/ReporterMenuPreview", name + ".asset");
        var pages = Regex.Matches(File.ReadAllText(fixture),
            @"(?m)^  - Text:\r?\n      IsLocalized: 0\r?\n      Unlocalized: ([^\r\n]+)");
        if (pages.Count == 0)
            throw new InvalidOperationException("No transcript text found in " + fixture);
        using (var source = new Bitmap(Path.Combine(root, "Assets/Game/UI/InventoryFolder/Textures/Inventory_TornNotebook.png")))
        using (var note = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        using (var type = new Font("Courier New", 34, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var ink = new SolidBrush(Color.FromArgb(235, 54, 44, 35)))
        {
            using (var graphics = Graphics.FromImage(note))
            {
                graphics.DrawImageUnscaled(source, 0, 0);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float x = note.Width * .175f, y = note.Height * .133f, leading = note.Height * .040f;
                foreach (Match page in pages)
                {
                    string line = "";
                    foreach (string word in page.Groups[1].Value.Trim().Split(' '))
                    {
                        string candidate = line.Length == 0 ? word : line + " " + word;
                        if (graphics.MeasureString(candidate, type).Width > note.Width * .74f && line.Length != 0)
                        {
                            graphics.DrawString(line, type, ink, x, y);
                            y += leading;
                            candidate = word;
                        }
                        line = candidate;
                    }
                    graphics.DrawString(line, type, ink, x, y);
                    y += 2 * leading;
                }
            }
            Convert(note, VisibleBounds(note), root, template, name);
        }
    }

    static void Import(string root, string template, string source, string name, bool trim = false)
    {
        using (var image = new Bitmap(Path.Combine(root, source)))
        {
            var full = new Rectangle(0, 0, image.Width, image.Height);
            var crop = trim ? VisibleBounds(image) : full;
            if (!full.Contains(crop) || crop.IsEmpty)
                throw new InvalidOperationException("Invalid sprite crop: " + source);
            Convert(image, crop, root, template, name);
        }
    }

    static void Convert(Bitmap original, Rectangle crop, string root, string template, string name)
    {
        string path = Path.Combine(root, Output, name + ".png");
        SaveHalfSize(original, crop, path);
        string meta = path + ".meta";
        if (!File.Exists(meta))
            File.WriteAllText(meta, Regex.Replace(template, "guid: [a-f0-9]{32}", "guid: " + GuidFor(name)));
        Console.WriteLine("{0} {1}x{2} {3}", name, (crop.Width + 1) / 2, (crop.Height + 1) / 2, GuidFor(name));
    }

    static void SaveHalfSize(Bitmap original, Rectangle crop, string path)
    {
        int width = (crop.Width + 1) / 2, height = (crop.Height + 1) / 2;
        using (var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var sharpened = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var sampling = new ImageAttributes())
                {
                    sampling.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(original, new Rectangle(0, 0, width, height), crop.X, crop.Y, crop.Width, crop.Height,
                        GraphicsUnit.Pixel, sampling);
                }
            }

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                Color a = resized.GetPixel(Math.Max(0, x - 1), y), b = resized.GetPixel(Math.Min(width - 1, x + 1), y);
                Color c = resized.GetPixel(x, Math.Max(0, y - 1)), d = resized.GetPixel(x, Math.Min(height - 1, y + 1));
                bool interior = pixel.A >= 230 && a.A >= 230 && b.A >= 230 && c.A >= 230 && d.A >= 230;
                sharpened.SetPixel(x, y, Color.FromArgb(pixel.A,
                    Channel(pixel.R, a.R + b.R + c.R + d.R, interior),
                    Channel(pixel.G, a.G + b.G + c.G + d.G, interior),
                    Channel(pixel.B, a.B + b.B + c.B + d.B, interior)));
            }
            sharpened.Save(path, ImageFormat.Png);
        }
    }

    static int Channel(int value, int neighbors, bool sharpen)
    {
        var delta = sharpen ? (int)Math.Round(.28 * (value - neighbors / 4.0)) : 0;
        return Math.Max(0, Math.Min(255, value + delta));
    }

    static Rectangle VisibleBounds(Bitmap image)
    {
        int left = image.Width, top = image.Height, right = 0, bottom = 0;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            if (image.GetPixel(x, y).A < 128) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + 1);
            bottom = Math.Max(bottom, y + 1);
        }
        return Rectangle.FromLTRB(Math.Max(0, left - 2), Math.Max(0, top - 2),
            Math.Min(image.Width, right + 2), Math.Min(image.Height, bottom + 2));
    }

    static string GuidFor(string name)
    {
        using (var hash = MD5.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("ReporterMenuPreview/" + name)))
                .Replace("-", "").ToLowerInvariant();
    }
}
