using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    class BuildMaterials : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var wall in root.GetComponentsInChildren<HouseWall>(true)) WallBuilder.ApplyMaterials(wall);
        }
    }
}
