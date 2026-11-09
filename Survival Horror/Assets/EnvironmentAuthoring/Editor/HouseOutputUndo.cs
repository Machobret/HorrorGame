using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // The House adapter identifies roots; the shared publisher owns native history.
    internal static class HouseOutputUndo
    {
        internal static void Prepare(Component source) => HouseRebuildCoordinator.PrepareBuilding(
            source as HouseBuilding ?? (source != null ? HouseBuilding.For(source.GetComponent<HousePlan>()) : null));

        internal static void Refresh(Component source, string action, HouseGenerationData data = null)
        {
            if (source == null) return;
            var building = source as HouseBuilding ?? HouseBuilding.For(source.GetComponent<HousePlan>());
            Run(building != null ? building.gameObject : source.gameObject, () =>
            {
                if (source is HouseBuilding house) HouseRebuildCoordinator.RefreshNow(house, data: data);
                else HouseRebuildCoordinator.RefreshNow(source.GetComponent<HousePlan>(), data: data);
            }, action);
        }

        internal static void Run(GameObject owner, Action build, string action) =>
            GeneratedOutputUndo.Run(owner, () => Roots(owner), build, action);

        internal static void Destroy(Object target) => GeneratedOutputUndo.Destroy(target);
        internal static void Remove(GameObject root) => GeneratedOutputUndo.Remove(root);
        internal static T AddComponent<T>(GameObject target) where T : Component => GeneratedOutputUndo.AddComponent<T>(target);

        private static GameObject[] Roots(GameObject owner)
        {
            var plans = owner.GetComponentsInChildren<HousePlan>(true);
            var builders = plans.Select(plan => plan.GetComponent<HouseWallBuilder>()).Where(builder => builder != null).ToArray();
            var buildings = owner.GetComponentsInChildren<HouseBuilding>(true);
            // Include a uniquely owned root even if it was accidentally moved outside its owner.
            return Resources.FindObjectsOfTypeAll<HouseWallOutput>()
                .Where(output => output.gameObject.scene == owner.scene && (builders.Contains(output.Owner) || output.transform.IsChildOf(owner.transform)))
                .Select(output => output.gameObject)
                .Concat(Resources.FindObjectsOfTypeAll<HouseSlabOutput>()
                    .Where(output => output.gameObject.scene == owner.scene && (buildings.Contains(output.Owner) || output.transform.IsChildOf(owner.transform)))
                    .Select(output => output.gameObject))
                .Concat(Resources.FindObjectsOfTypeAll<HouseCollisionOutput>()
                    .Where(output => output.gameObject.scene == owner.scene && (plans.Contains(output.Owner) || output.transform.IsChildOf(owner.transform)))
                    .Select(output => output.gameObject)).Distinct().ToArray();
        }
    }
}
