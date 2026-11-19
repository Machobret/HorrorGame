using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    internal static partial class HouseScene
    {
        internal static bool Cutaway;
        static readonly Dictionary<House, (string floorId, bool enabled)> pinnedCutaways = new();
        static readonly HashSet<GameObject> hidden = new();

        // Use Unity's native visibility operations without adding a preview to the Undo history.
        static readonly Type visibilityState = typeof(SceneVisibilityManager).Assembly.GetType("UnityEditor.SceneVisibilityState", true);
        static readonly Action<GameObject, bool, bool> setPreviewHidden =
            (Action<GameObject, bool, bool>)visibilityState.GetMethod("SetGameObjectHidden").CreateDelegate(typeof(Action<GameObject, bool, bool>));
        static readonly Action<Scene> persistVisibility =
            (Action<Scene>)visibilityState.GetMethod("GeneratePersistentDataForLoadedScene").CreateDelegate(typeof(Action<Scene>));

        internal static bool IsCutawayPinned(House house) => house && pinnedCutaways.ContainsKey(house);

        internal static bool CutawayFor(House house) =>
            house && (house == House ? Cutaway : pinnedCutaways.TryGetValue(house, out var pin) && pin.enabled);

        internal static void ToggleCutaway(House house)
        {
            if (!house || house.wallsOnly) return;
            if (house == House) Cutaway = !Cutaway;
            else if (pinnedCutaways.TryGetValue(house, out var pin)) pinnedCutaways[house] = (pin.floorId, !pin.enabled);
            ApplyCutaway();
            Repaint();
        }

        internal static void SetCutawayPinned(House house, bool value)
        {
            if (!house || house.wallsOnly) return;
            if (!value) pinnedCutaways.Remove(house);
            else if (house == House) pinnedCutaways[house] = (Floor.floorId, Cutaway);
            ApplyCutaway();
            Repaint();
        }

        static void RememberPinnedCutaway()
        {
            if (Floor && IsCutawayPinned(House)) pinnedCutaways[House] = (Floor.floorId, Cutaway);
        }

        static void EndCutawaySession()
        {
            pinnedCutaways.Clear();
            Leave();
        }

        static void OnSceneClosing(Scene scene, bool removing)
        {
            foreach (var house in pinnedCutaways.Keys.Where(h => !h || h.gameObject.scene == scene).ToArray())
                pinnedCutaways.Remove(house);
            if (editingHouse && editingHouse.gameObject.scene == scene) Leave();
            else ApplyCutaway();
        }

        static void OnSceneSaved(Scene scene)
        {
            if (!hidden.Any(go => go && go.scene == scene)) return;
            SaveUnderlyingVisibility(scene);
            ApplyCutaway();
        }

        static void SaveUnderlyingVisibility(Scene scene)
        {
            // Unity can snapshot Scene visibility when saving. Temporarily exclude every
            // preview in this scene so the artist's manually hidden objects are preserved.
            foreach (var go in hidden.Where(go => go && go.scene == scene).ToArray())
            {
                setPreviewHidden(go, false, false);
                hidden.Remove(go);
            }
            if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path)) persistVisibility(scene);
        }

        internal static void ApplyCutaway()
        {
            if (!Floor && pinnedCutaways.Count == 0 && hidden.Count == 0) return;
            RememberPinnedCutaway();
            var desired = new HashSet<GameObject>();
            if (Floor && Cutaway) AddCutaway(Floor, desired);
            foreach (var (house, pin) in pinnedCutaways.ToArray())
            {
                if (!house) pinnedCutaways.Remove(house);
                else if (house != House && pin.enabled)
                    AddCutaway(HouseEditing.Floors(house).FirstOrDefault(floor => floor.floorId == pin.floorId), desired);
            }

            var restoredScenes = new HashSet<Scene>();
            foreach (var go in hidden.ToArray())
            {
                if (go && desired.Contains(go)) continue;
                if (go)
                {
                    setPreviewHidden(go, false, false);
                    restoredScenes.Add(go.scene);
                }
                hidden.Remove(go);
            }
            foreach (var scene in restoredScenes) SaveUnderlyingVisibility(scene);

            var visibility = SceneVisibilityManager.instance;
            foreach (var go in desired)
            {
                if (visibility.IsHidden(go)) continue;
                setPreviewHidden(go, true, false);
                hidden.Add(go);
            }
        }

        static void AddCutaway(HouseFloor floor, HashSet<GameObject> desired)
        {
            if (!floor || floor.WallsOnly || !floor.House) return;
            foreach (var upper in HouseEditing.Floors(floor.House))
                if (upper.transform.localPosition.y > floor.transform.localPosition.y)
                    foreach (var child in upper.GetComponentsInChildren<Transform>(true))
                        desired.Add(child.gameObject);

            foreach (var slab in floor.GetComponentsInChildren<HouseSlab>(true))
                if (slab.overhead)
                {
                    desired.Add(slab.gameObject);
                    foreach (Transform child in slab.transform)
                        if (child.TryGetComponent<BooleanCutResult>(out _)) desired.Add(child.gameObject);
                }
        }
    }
}
