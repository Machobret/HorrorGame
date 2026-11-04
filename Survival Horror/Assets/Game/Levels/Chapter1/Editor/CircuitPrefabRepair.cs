#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Chapter1.Editor
{
    /// <summary>Repairs the early generated prefabs once the circuit components have their own script assets.</summary>
    [InitializeOnLoad]
    internal static class CircuitPrefabRepair
    {
        static CircuitPrefabRepair()
        {
            EditorApplication.delayCall += Repair;
        }

        private static void Repair()
        {
            RepairLamp("Assets/Game/Levels/Chapter1/Prefabs/ActivatableLamp.prefab");
            RepairShutter("Assets/Game/Levels/Chapter1/Prefabs/ActivatableDoorShutter.prefab");
        }

        private static void RepairLamp(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (!root) return;
            CircuitDrivenLamp lamp = root.GetComponent<CircuitDrivenLamp>();
            if (!lamp) lamp = root.AddComponent<CircuitDrivenLamp>();
            lamp.Lights = root.GetComponentsInChildren<Light>(true);
            Transform bulbTransform = root.transform.Find("Bulb");
            Renderer bulb = bulbTransform ? bulbTransform.GetComponent<Renderer>() : null;
            lamp.EmissiveRenderers = bulb ? new[] { bulb } : root.GetComponentsInChildren<Renderer>(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void RepairShutter(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (!root) return;
            CircuitDrivenShutter shutter = root.GetComponent<CircuitDrivenShutter>();
            if (!shutter) shutter = root.AddComponent<CircuitDrivenShutter>();
            if (!shutter.MovingPart) shutter.MovingPart = root.transform.Find("ShutterPanel");
            if (shutter.MovingPart)
                shutter.BlockingColliders = shutter.MovingPart.GetComponentsInChildren<Collider>(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
