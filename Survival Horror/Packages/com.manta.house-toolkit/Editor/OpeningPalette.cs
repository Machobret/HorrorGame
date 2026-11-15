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
            var family = wall.Family;
            if (!family) return new List<HouseInsert>();
            library ??= AssetDatabase.FindAssets("t:HouseInsert")
                .Select(guid => AssetDatabase.LoadAssetAtPath<HouseInsert>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            return family.defaultInserts.Concat(library)
                .Where(item => item && item.prefab && item.kind == kind &&
                    (!family.panel || family.FindSurround(item)) &&
                    family.thickness >= item.minimumThickness &&
                    (item.maximumThickness <= 0 || family.thickness <= item.maximumThickness))
                .Distinct().ToList();
        }
    }
}
