using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvironmentAuthoring.Editor
{
    // Commands/rebuild publish in dependency order. Background work checks freshness
    // and existing poses only; builders retain their own input/health policies.
    [InitializeOnLoad]
    internal static class HouseRebuildCoordinator
    {
        private static readonly Unity.Profiling.ProfilerMarker refreshSample = new Unity.Profiling.ProfilerMarker("House.RefreshOutputs");
        // Native views restore temporary cutaway/isolation after new output exists.
        // Builders themselves do not know which House or editor context is active.
        internal static event Action<HousePlan> OutputsRefreshed;
        private readonly struct BuildScope
        {
            private readonly Component source;
            private readonly bool forceWalls, includeDisabledWalls;
            internal BuildScope(Component source, bool forceWalls, bool includeDisabledWalls)
            { this.source = source; this.forceWalls = forceWalls; this.includeDisabledWalls = includeDisabledWalls; }
            internal bool ForceWalls(HousePlan plan) => forceWalls && (source is HouseBuilding || source == plan);
            internal bool IncludeDisabledWalls(HousePlan plan) => includeDisabledWalls && (source is HouseBuilding || source == plan);
        }

        // Background requests are checks, not queued geometry jobs or force scopes.
        private static readonly HashSet<Component> pending = new HashSet<Component>();
        private static readonly Queue<Component> pendingOrder = new Queue<Component>();
        private static double nextAudit;
        private static bool auditRequested = true;
        private static bool flushing;
        private static readonly HashSet<Component> stale = new HashSet<Component>();

        internal static bool NeedsRebuild(Component source) => source != null && stale.Contains(Boundary(source));

        static HouseRebuildCoordinator()
        {
            EditorApplication.update += Update;
            Undo.undoRedoPerformed += RequestAudit;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static void Request(HousePlan plan) => Queue(plan);
        internal static void Request(HouseBuilding building) => Queue(building);

        internal static bool RestoreOutputPose(Component output, Transform owner)
        {
            var pose = output.transform;
            if (pose.parent == owner && pose.localPosition == Vector3.zero
                && pose.localRotation == Quaternion.identity && pose.localScale == Vector3.one) return false;
            // Repair derived hierarchy outside Undo, retaining the existing children
            // and mesh overrides. Authored plan transforms are never changed here.
            pose.SetParent(owner, false);
            pose.localPosition = Vector3.zero;
            pose.localRotation = Quaternion.identity;
            pose.localScale = Vector3.one;
            EditorUtility.SetDirty(pose);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            return true;
        }

        // Generated meshes are scene-owned. Prefab assets and inherited prefab components
        // cannot retain these outputs until a separate asset-baking workflow exists.
        internal static bool NeedsSceneInstance(HousePlan plan) =>
            (PrefabUtility.IsPartOfNonAssetPrefabInstance(plan) && !PrefabUtility.IsAddedComponentOverride(plan))
            || PrefabStageUtility.GetCurrentPrefabStage()?.scene == plan.gameObject.scene;

        internal static void RebuildHouse(HousePlan plan)
        {
            var building = plan != null ? HouseBuilding.For(plan) : null;
            if (building != null) RefreshNow(building);
            else RefreshNow(plan);
        }

        internal static void RefreshNow(HousePlan plan, bool forceWalls = false, HouseGenerationData data = null) => Refresh(plan, forceWalls, data);
        internal static void RefreshNow(HouseBuilding building, bool forceWalls = false, HouseGenerationData data = null) => Refresh(building, forceWalls, data);

        private static void Refresh(Component source, bool forceWalls, HouseGenerationData data)
        {
            if (source == null || Application.isPlaying || EditorUtility.IsPersistent(source)) return;
            var request = new BuildScope(source, forceWalls, true);
            source = Boundary(source);
            Publish(source, () => !forceWalls && Current(source, request), () =>
            {
                if (source is HouseBuilding house) Build(house, request, data);
                else Build((HousePlan)source, request, data);
            });
        }

        // A source command already owns its Undo group. A manual rebuild owns one
        // action only when output really changes; a no-op must preserve Redo.
        private static void Publish(Component source, Func<bool> current, Action build)
        {
            if (GeneratedOutputUndo.IsPublishing) { build(); return; }
            PrepareBuilding(source as HouseBuilding ?? HouseBuilding.For(source.GetComponent<HousePlan>()));
            GeneratedMeshResources.Prune();
            if (current()) { stale.Remove(Boundary(source)); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            const string action = "Rebuild House";
            Undo.SetCurrentGroupName(action);
            HouseOutputUndo.Run(source.gameObject, build, action);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            stale.Remove(Boundary(source));
        }

        internal static void RefreshWallsNow(HouseWallBuilder builder, bool force = false)
        {
            if (builder == null || Application.isPlaying) return;
            var plan = builder.GetComponent<HousePlan>();
            PrepareBuilding(HouseBuilding.For(plan));
            var data = HouseGenerationData.Create(plan);
            Publish(plan, () => !force && HouseWallOutputBuilder.IsCurrent(builder, data) && HouseBooleanOutput.WallsCurrent(plan, data) && HouseDoorObjects.IsCurrent(plan, data), () =>
            {
                HouseWallOutputBuilder.Build(builder, force, data);
                HouseBooleanOutput.Walls(plan, data);
                HouseDoorObjects.Build(plan, data);
                RepaintChangedOutput(data);
                NotifyChangedOutput(data, plan);
            });
        }

        internal static void RefreshSlabsNow(HouseBuilding building)
        {
            if (building == null || Application.isPlaying || EditorUtility.IsPersistent(building)) return;
            PrepareBuilding(building);
            var data = HouseGenerationData.Create(building);
            Publish(building, () => HouseSlabBuilder.IsCurrent(building, data) && HouseBooleanOutput.SlabsCurrent(building, data), () =>
            {
                HouseSlabBuilder.Build(building, data);
                HouseBooleanOutput.Slabs(building, data);
                RepaintChangedOutput(data);
                foreach (var level in building.Levels)
                    if (level?.Plan != null) NotifyChangedOutput(data, level.Plan);
            });
        }

        internal static void RefreshCollisionNow(HousePlan plan)
        {
            if (plan == null || Application.isPlaying || EditorUtility.IsPersistent(plan)) return;
            PrepareBuilding(HouseBuilding.For(plan));
            var data = HouseGenerationData.Create(plan);
            Publish(plan, () => HouseCollisionBuilder.IsCurrent(plan, data), () =>
            {
                HouseCollisionBuilder.Build(plan, data);
                RepaintChangedOutput(data);
                NotifyChangedOutput(data, plan);
            });
        }

        private static bool Current(Component source, BuildScope request)
        {
            var data = source is HouseBuilding house ? HouseGenerationData.Create(house) : HouseGenerationData.Create((HousePlan)source);
            bool Level(HousePlan plan)
            {
                var builder = plan.GetComponent<HouseWallBuilder>();
                return (builder == null || (!builder.enabled && !request.IncludeDisabledWalls(plan)) || HouseWallOutputBuilder.IsCurrent(builder, data))
                    && HouseBooleanOutput.WallsCurrent(plan, data) && HouseCollisionBuilder.IsCurrent(plan, data)
                    && HouseDoorObjects.IsCurrent(plan, data);
            }
            return source is HouseBuilding building
                ? HouseSlabBuilder.IsCurrent(building, data) && HouseBooleanOutput.SlabsCurrent(building, data)
                    && building.Levels.All(level => level?.Plan == null || Level(level.Plan))
                : Level((HousePlan)source);
        }

        private static Component Boundary(Component source)
        {
            if (source == null) return null;
            if (source is HousePlan plan)
            {
                var building = HouseBuilding.For(plan);
                return building != null ? (Component)building : plan;
            }
            return source;
        }

        private static void Queue(Component source)
        {
            source = Boundary(source);
            if (source != null && !EditorUtility.IsPersistent(source) && pending.Add(source)) pendingOrder.Enqueue(source);
        }

        private static void OnSceneClosed(Scene scene) => GeneratedMeshResources.RequestPrune();

        private static void RequestAudit() => auditRequested = true;

        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            // Finish the current round before scanning again. Re-enqueuing the
            // first Houses every 0.2 s would otherwise starve later ones.
            if (pending.Count == 0 && (auditRequested || EditorApplication.timeSinceStartup >= nextAudit))
            {
                auditRequested = false;
                nextAudit = EditorApplication.timeSinceStartup + .2;
                AuditLoadedScenes();
            }
            Flush(1); // Background freshness reads one House, never the whole level in one frame.
        }

        // Source commands publish immediately. Inspector/scripts/imports bypassing
        // commands are detected here and applied only by an explicit Rebuild.
        internal static void AuditLoadedScenes()
        {
            foreach (var building in Resources.FindObjectsOfTypeAll<HouseBuilding>())
                if (CanAudit(building)) Request(building);
            foreach (var plan in Resources.FindObjectsOfTypeAll<HousePlan>())
                if (CanAudit(plan) && (plan.enabled || (plan.GetComponent<HouseWallBuilder>()?.enabled ?? false)))
                    Request(plan);
        }

        private static bool CanAudit(Component source) => source != null && !EditorUtility.IsPersistent(source)
            && source.gameObject.scene.IsValid() && source.gameObject.scene.isLoaded
            && !EditorSceneManager.IsPreviewScene(source.gameObject.scene);

        internal static int Flush(int maxSources = int.MaxValue)
        {
            using var sample = refreshSample.Auto();
            if (flushing || Application.isPlaying) return 0;
            if (pending.Count == 0) { GeneratedMeshResources.Prune(); return 0; }
            flushing = true;
            try
            {
                stale.RemoveWhere(source => source == null || !source.gameObject.scene.isLoaded);
                var requests = pendingOrder.Take(maxSources).ToArray();
                foreach (var request in requests) { pendingOrder.Dequeue(); pending.Remove(request); }
                var work = requests.Select(Boundary).Where(source => source != null).Distinct().ToArray();
                foreach (var source in work)
                {
                    if (!source.gameObject.scene.IsValid() || !source.gameObject.scene.isLoaded) continue;
                    // Background work may restore existing poses, but never replace
                    // native children. External input changes wait for Rebuild.
                    PrepareBuilding(source as HouseBuilding ?? HouseBuilding.For(source.GetComponent<HousePlan>()));
                    RepairPoses(source);
                    if (Current(source, new BuildScope(source, false, false))) stale.Remove(source);
                    else stale.Add(source);
                }
                GeneratedMeshResources.Prune();
                return work.Length;
            }
            finally { flushing = false; }
        }

        private static void RepairPoses(Component source)
        {
            var plans = source is HouseBuilding building ? building.Levels.Where(level => level?.Plan != null).Select(level => level.Plan)
                : new[] { (HousePlan)source };
            foreach (var plan in plans)
            {
                var builder = plan.GetComponent<HouseWallBuilder>();
                var walls = Resources.FindObjectsOfTypeAll<HouseWallOutput>().Where(output => builder != null && output.Owner == builder
                    && output.gameObject.scene == plan.gameObject.scene).ToArray();
                if (walls.Length == 1 && !NeedsSceneInstance(plan)) RestoreOutputPose(walls[0], plan.transform);
                var collisions = Resources.FindObjectsOfTypeAll<HouseCollisionOutput>().Where(output => output.Owner == plan
                    && output.gameObject.scene == plan.gameObject.scene).ToArray();
                if (collisions.Length == 1 && !NeedsSceneInstance(plan)) RestoreOutputPose(collisions[0], plan.transform);
            }
            if (source is HouseBuilding house)
            {
                var output = HouseSlabBuilder.Output(house);
                if (output != null && house.TryValidateLayout(out _)) HouseSlabBuilder.PlaceOutput(output.transform, house);
            }
        }

        private static void Build(HouseBuilding building, BuildScope request, HouseGenerationData data = null)
        {
            // Level placement is owned by the building, not by slab geometry computation.
            PrepareBuilding(building);
            HouseSlabBuilder.RemoveOrphanedOutput(building);
            data ??= HouseGenerationData.Create(building);
            HouseSlabBuilder.Build(building, data);
            HouseBooleanOutput.Slabs(building, data);
            foreach (var level in building.Levels)
                if (level?.Plan != null) BuildLevel(level.Plan, request, data);
            RepaintChangedOutput(data);
        }

        private static void Build(HousePlan plan, BuildScope request, HouseGenerationData data = null)
        {
            data ??= HouseGenerationData.Create(plan);
            BuildLevel(plan, request, data);
            RepaintChangedOutput(data);
        }

        private static void BuildLevel(HousePlan plan, BuildScope request, HouseGenerationData data)
        {
            HouseWallOutputBuilder.RemoveOrphanedOutput(plan);
            HouseCollisionBuilder.RemoveOrphanedOutput(plan);
            var builder = plan.GetComponent<HouseWallBuilder>();
            // Background refresh skips disabled builders. An explicit refresh can
            // rebuild one while retaining its saved visibility setting.
            if (builder != null && (builder.enabled || request.IncludeDisabledWalls(plan)))
                HouseWallOutputBuilder.Build(builder, request.ForceWalls(plan), data);
            HouseBooleanOutput.Walls(plan, data);
            HouseCollisionBuilder.Build(plan, data);
            HouseDoorObjects.Build(plan, data);
            NotifyChangedOutput(data, plan);
        }

        private static void NotifyChangedOutput(HouseGenerationData data, HousePlan plan)
        {
            // Healthy unchanged renderers retain their temporary visibility flags.
            // Auditing them is not new output or an authored-change notification.
            if (data.HasOutputChanges) OutputsRefreshed?.Invoke(plan);
        }

        private static void RepaintChangedOutput(HouseGenerationData data)
        {
            // The periodic audit must not repaint an unchanged Scene view.
            if (data.HasOutputChanges) SceneView.RepaintAll();
        }

        internal static void PrepareBuilding(HouseBuilding building)
        {
            if (building == null || EditorUtility.IsPersistent(building) || !building.TryValidateLayout(out _)) return;
            foreach (var level in building.Levels)
                if (level?.Plan != null && (NeedsSceneInstance(level.Plan) || !float.IsFinite(building.Data.Elevation(level)))) return;
            foreach (var level in building.Levels)
            {
                if (level?.Plan == null) continue;
                var pose = level.Plan.transform;
                var position = HouseVerticalLayout.PlanPosition(building, level);
                if (pose.localPosition == position && pose.localRotation == Quaternion.identity && pose.localScale == Vector3.one) continue;
                // Level elevations are slab centres. The plan lives on the finished
                // floor so its wall geometry and authored content share that surface.
                pose.localPosition = position;
                pose.localRotation = Quaternion.identity;
                pose.localScale = Vector3.one;
                EditorUtility.SetDirty(pose);
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            }
        }

        private static void RequestScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var building in root.GetComponentsInChildren<HouseBuilding>(true)) Request(building);
                foreach (var plan in root.GetComponentsInChildren<HousePlan>(true)) Request(plan);
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => RequestScene(scene);

        private static void OnSceneSaving(Scene scene, string path)
        {
            // A save must include recent edits even before the next editor update.
            RequestScene(scene);
            Flush();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AuditLoadedScenes();
                Flush();
            }
            else if (state == PlayModeStateChange.EnteredEditMode) RequestAudit();
        }
    }
}
