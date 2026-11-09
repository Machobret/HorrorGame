using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseTrimMesh
    {
        [MenuItem("Assets/Create/Environment/Plain Trim Mesh")]
        private static void CreateAsset()
            => ProjectWindowUtil.CreateAsset(WallModuleExamples.BuildTrim(), "Plain Trim.asset");

        internal static Mesh Preview
        {
            get
            {
                const string key = "House trim attachment preview";
                return GeneratedMeshResources.FindCached(key)
                    ?? GeneratedMeshResources.Cache(key, WallModuleExamples.BuildTrim());
            }
        }
    }
}
