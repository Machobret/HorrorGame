using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace EnvironmentAuthoring.Editor
{
    // Filename metadata is interpreted once, by the explicit family importer.
    public sealed class WallAssetName
    {
        public string Family, Shape;
        public FloorPlan.OpeningKind Kind;
        public bool Solid, Insert;
        public float Width, Height, Thickness;
        public static readonly int[] ThicknessPresets = { 100, 150, 200, 225, 250, 300, 400, 500, 600 };

        public static bool TryParse(string path, out WallAssetName value, out string reason)
        {
            value = null;
            reason = "Use Wall_01_Solid_01_W2000_H2800_T150.fbx or Insert_Window_01_W1000_H2800_Model01.fbx.";
            string name = Path.GetFileNameWithoutExtension(path);
            var wall = Regex.Match(name, @"^Wall_(?<family>[A-Za-z0-9]+)_(?<kind>Solid|Window|Door)_(?<shape>[A-Za-z0-9]+)_W(?<width>\d+)_H(?<height>\d+)_T(?<depth>\d+)$");
            var insert = Regex.Match(name, @"^Insert_(?<kind>Window|Door)_(?<shape>[A-Za-z0-9]+)_W(?<width>\d+)_H(?<height>\d+)_Model(?<style>[A-Za-z0-9]+)$");
            var match = wall.Success ? wall : insert;
            if (!match.Success) return false;
            int Millimetres(string key) => int.TryParse(match.Groups[key].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
            int width = Millimetres("width"), height = Millimetres("height"), depth = wall.Success ? Millimetres("depth") : 0;
            bool solid = match.Groups["kind"].Value == "Solid";
            reason = "Solid widths are 500/1000/2000 mm; opening widths are 1000/2000 mm. Height must be positive and whole-wall thickness must use a declared preset (100/150/200/225/250/300/400/500/600 mm).";
            if (height <= 0 || width != 1000 && width != 2000 && !(solid && width == 500)
                || wall.Success && Array.IndexOf(ThicknessPresets, depth) < 0) return false;
            value = new WallAssetName { Family = match.Groups["family"].Value, Shape = solid ? null : match.Groups["shape"].Value,
                Kind = match.Groups["kind"].Value == "Window" ? FloorPlan.OpeningKind.Window : FloorPlan.OpeningKind.Doorway,
                Solid = solid, Insert = insert.Success, Width = width / 1000f, Height = height / 1000f, Thickness = depth / 1000f };
            reason = "";
            return true;
        }
    }
}
