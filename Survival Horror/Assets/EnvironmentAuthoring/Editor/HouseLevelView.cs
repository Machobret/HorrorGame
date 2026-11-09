using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Rendering-only cutaway: colliders and scripts remain active. Restore every prior flag.
    [InitializeOnLoad]
    internal static class HouseLevelView
    {
        private static readonly Dictionary<Renderer, bool> previous = new Dictionary<Renderer, bool>();
        private static HousePlan active;
        internal static bool Cutaway = true;
        internal static bool CeilingHidden(HousePlan plan) => Cutaway && active == plan;
        internal static bool HiddenLevel(HousePlan plan)
        {
            var building = HouseBuilding.For(active);
            return Cutaway && building != null && HouseBuilding.For(plan) == building && building.Levels.IndexOf(building.Find(plan)) > building.Levels.IndexOf(building.Find(active));
        }

        static HouseLevelView()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.playModeStateChanged += _ => Restore();
        }

        internal static void Set(HousePlan plan)
        {
            Restore();
            active = plan;
            Apply();
        }

        internal static void Restore()
        {
            foreach (var entry in previous)
                if (entry.Key != null)
                    entry.Key.forceRenderingOff = entry.Value;
            previous.Clear();
            active = null;
        }

        internal static void Apply()
        {
            var building = HouseBuilding.For(active);
            if (building == null)
                return;
            foreach (var entry in previous)
                if (entry.Key != null)
                    entry.Key.forceRenderingOff = entry.Value;
            previous.Clear();
            if (!Cutaway)
                return;
            int index = building.Levels.IndexOf(building.Find(active));
            foreach (var level in building.Levels.Skip(index + 1))
                if (level.Plan != null)
                    foreach (var renderer in level.Plan.GetComponentsInChildren<Renderer>(true))
                        // Shared slabs are parented to a persistent level for Undo safety;
                        // visibility still follows each panel's own level below.
                        if (renderer.GetComponentInParent<HouseSlabOutput>() == null) Hide(renderer);
            var output = HouseSlabBuilder.Output(building);
            if (output != null)
                foreach (var panel in output.Panels)
                {
                    int ownerIndex = building.Levels.IndexOf(building.Find(panel.Plan));
                    if (panel.Root != null && (ownerIndex > index || (ownerIndex == index && panel.Ceiling)))
                        foreach (var renderer in panel.Root.GetComponentsInChildren<Renderer>(true))
                            Hide(renderer);
                }
        }

        private static void Hide(Renderer renderer)
        {
            if (renderer == null)
                return;
            if (!previous.ContainsKey(renderer))
                previous.Add(renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }
}
