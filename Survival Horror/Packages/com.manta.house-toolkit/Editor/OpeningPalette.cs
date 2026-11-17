using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    public static class OpeningPalette
    {
        static HouseInsert[] library;
        static OpeningPalette() => EditorApplication.projectChanged += () => library = null;

        public static List<HouseInsert> Choices(HouseWall wall, OpeningKind kind)
        {
            var result = new List<HouseInsert>();
            Fill(wall,kind,result);
            return result;
        }

        internal static void Fill(HouseWall wall, OpeningKind kind, List<HouseInsert> result)
        {
            result.Clear();
            if (!wall || wall.boundaryOnly) return;
            var floor = wall.Floor;
            var family = wall.Family;
            if (!floor || !family || family.IsComplex && !family.panel) return;
            float depth = wall.Thickness;
            Add(family.defaultInserts);
            if (kind == OpeningKind.Panel) return;
            library ??= AssetDatabase.FindAssets("t:HouseInsert")
                .Select(guid => AssetDatabase.LoadAssetAtPath<HouseInsert>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Add(library);

            void Add(HouseInsert[] items)
            {
                if (items == null) return;
                foreach (var insert in items)
                {
                    if (!insert || insert.kind != kind || result.Contains(insert) || !family.SupportsInsertAsset(insert)) continue;
                    if (insert is GarageDoorInsert garage)
                    {
                        if (!family.IsComplex && wall.length > GarageDoorInsert.Jamb * 2 && floor.height > GarageDoorInsert.Header)
                            result.Add(garage);
                        continue;
                    }
                    if (depth < insert.minimumThickness || insert.maximumThickness > 0 && depth > insert.maximumThickness) continue;
                    var surround = family.FindSurround(insert);
                    if (family.panel && !surround && kind != OpeningKind.Panel) continue;
                    float width = surround ? surround.size.x : insert.OpeningSize.x;
                    float head = insert.OpeningSize.y + (surround ? surround.aperture.y : 0);
                    if (width <= wall.length + .0005f && head <= floor.height + .0005f) result.Add(insert);
                }
            }
        }
    }
}
