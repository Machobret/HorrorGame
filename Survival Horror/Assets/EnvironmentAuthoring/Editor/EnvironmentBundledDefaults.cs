using UnityEditor;
using UnityEngine;
namespace EnvironmentAuthoring.Editor
{
    // Only this explicit menu changes creation defaults; importing the package does nothing to them.
    internal static class EnvironmentBundledDefaults
    {
        [MenuItem("Tools/Environment/Use bundled defaults")]
        internal static void Apply()
        {
            var settings = EnvironmentProjectSettings.instance;
            settings.InteriorFamily = Load<WallFamily>("5aff1a8fbaaaedd40a99623a5c9ed78e");
            settings.ExteriorFamily = Load<WallFamily>("8d2eddb37e7a6cd47bf65900fe77900f");
            settings.InsideWalls = Load<Material>("1effb7f05e4a58e49b2eb34b357740d1");
            settings.OutsideWalls = Load<Material>("f5fdab057b5757f408fd7fa6333bba71");
            settings.Floors = Load<Material>("694466a4d987c6b42861cacfeed3b2f9");
            settings.Ceilings = Load<Material>("1effb7f05e4a58e49b2eb34b357740d1");
            settings.CutSurface = Load<Material>("a13d239bc3b5d92448346fae7b1e118f");
            settings.WallModel = Load<TownModule>("b1b975231f36ac44cbf5c736a2b03670");
            settings.RoadSurface = Load<Material>("ebe363f33b4192a4086c151c672537aa");
            settings.SidewalkSurface = Load<Material>("dbea229acdf79fe4e919a7fb1dfde8d8");
            settings.CustomInserts = System.Array.Empty<WallOpeningInsert>();
            settings.SaveDefaults();
            EnvironmentProjectSettings.Open();
        }
        private static T Load<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
