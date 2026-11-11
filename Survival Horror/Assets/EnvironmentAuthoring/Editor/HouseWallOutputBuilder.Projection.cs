using System.Linq;
using UnityEditor;
using UnityEngine;
namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseWallOutputBuilder
    {
        internal static void ApplyWallProjectionRange(GameObject root)
        {
            var modules = root.GetComponentsInChildren<WallModule>(true).Where(module => module.Body != null).ToArray();
            if (modules.Length == 0) return;
            var renderers = modules.Select(module => module.Body.GetComponent<MeshRenderer>()).ToArray();
            float minimum = renderers.Min(renderer => renderer.bounds.min.y);
            float maximum = renderers.Max(renderer => renderer.bounds.max.y);
            foreach (var module in modules)
                if (module.ConfigureVerticalProjection(minimum, maximum)) EditorUtility.SetDirty(module);
        }
    }
}
