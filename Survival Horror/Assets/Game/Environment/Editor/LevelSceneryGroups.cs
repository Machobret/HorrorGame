using HouseToolkit.Editor;
using UnityEditor;

namespace HorrorGame.Environment.Editor
{
    [InitializeOnLoad]
    static class LevelSceneryGroups
    {
        static LevelSceneryGroups() => EnvironmentHierarchy.PrefabCategory = Category;

        static string Category(string path)
        {
            const string root = "Assets/Game/Environment/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(root, System.StringComparison.Ordinal)) return null;
            int end = path.IndexOf('/', root.Length);
            if (end < 0) return null;
            return path.Substring(root.Length, end - root.Length) switch
            {
                "Buildings" => "Buildings",
                "Vehicles" => "Vehicles",
                "StorageBoxes" or "StorageContainers" or "Furniture" => "Props",
                _ => null
            };
        }
    }
}
