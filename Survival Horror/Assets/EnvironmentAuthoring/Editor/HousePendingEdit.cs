using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // The session retains one authored intent. Its copied candidate and rendering can
    // disappear on interruption without losing the user's values or original target.
    [Serializable]
    internal sealed class HousePendingEdit
    {
        internal enum Kind { None, LevelHeight, SlabThickness, Geometry }
        [SerializeField] private Kind kind;
        [SerializeField] private HousePlan plan;
        [SerializeField] private string inputs;
        [SerializeField] private float baselineHeight, baselineTotal, baselineFinish;
        [SerializeField] private float heightValue, total, finish;
        [SerializeField] private bool suspended;
        [SerializeField] private string error = "";
        [NonSerialized] private HouseEditPreview preview;
        [NonSerialized] private HouseSlabPreview slabPreview;
        [NonSerialized] private bool valid;
        [NonSerialized] private int displayRevision;
        internal event Action Changed;
        internal event Action Interrupted;

        internal Kind ActiveKind => kind;
        internal HousePlan Plan => plan;
        internal bool IsProperty => kind != Kind.None && kind != Kind.Geometry;
        internal bool IsPending => kind == Kind.Geometry ? preview != null : IsProperty && ValuesChanged;
        internal bool Suspended => suspended && IsPending;
        internal bool Conflicted => IsPending && (plan == null || inputs != HouseEditInputs.SourceInputs(plan));
        internal string Error => Conflicted ? "The original House or its kit changed. Your values are retained until you explicitly discard them." : error;
        internal bool Valid => valid && preview != null && !suspended;
        internal float HeightValue => heightValue;
        internal float Total => total;
        internal float Finish => finish;
        internal float BaselineHeight => baselineHeight;
        internal int DisplayRevision => displayRevision;
        internal HouseEditPreview Preview => preview;

        private bool ValuesChanged => kind == Kind.SlabThickness ? total != baselineTotal || finish != baselineFinish
            : !float.IsFinite(heightValue) || Mathf.Abs(heightValue - baselineHeight) > .0001f;

        internal static float CurrentHeight(HousePlan target)
        {
            // Unity's destroyed objects are not CLR null; do not use ?. on that target.
            if (target == null) return 2.8f;
            var building = HouseBuilding.For(target);
            return building != null ? building.WallHeight(building.Find(target))
                : target?.GetComponent<HouseWallBuilder>()?.Data.DefaultHeight ?? 2.8f;
        }

        private bool Begin(HousePlan target, Kind requested)
        {
            if (kind != Kind.None)
            {
                if (plan == target && kind == requested
                    && !Suspended && target != null) return EnsureCandidate();
                error = "Finish or discard the current edit before changing its target.";
                return false;
            }
            if (!HouseEditValidation.Source(target, out error)) return false;
            if (EditorUtility.IsPersistent(target) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { error = "Choose an editable scene House outside Play mode."; return false; }
            var building = HouseBuilding.For(target);
            if (building == null) { error = "Choose a House level before changing these properties."; return false; }
            if (HouseRebuildCoordinator.NeedsSceneInstance(target)) { error = "Unpack the House before changing its geometry."; return false; }
            plan = target; kind = requested;
            baselineTotal = building.SlabThickness; baselineFinish = building.FloorThickness;
            baselineHeight = CurrentHeight(target);
            heightValue = baselineHeight; total = baselineTotal; finish = baselineFinish;
            suspended = false;
            preview = new HouseEditPreview(target);
            // Retain the candidate's captured basis after interruption; initial capture
            // is synchronous and does not need a second source comparison.
            inputs = preview.OriginalInputs;
            return true;
        }

        internal bool SetHeight(HousePlan target, float value)
        {
            if (!Begin(target, Kind.LevelHeight)) { Notify(); return false; }
            heightValue = value;
            bool result = Prepare(true);
            Notify();
            return result;
        }
        internal bool SetSlab(HousePlan target, float slabTotal, float floorFinish)
        {
            if (!Begin(target, Kind.SlabThickness)) { Notify(); return false; }
            total = slabTotal; finish = floorFinish;
            bool result = Prepare(true);
            Notify();
            return result;
        }

        private bool EnsureCandidate()
        {
            if (plan == null || inputs != HouseEditInputs.SourceInputs(plan))
            { error = "The original House or its kit changed. Discard these values explicitly before starting another edit."; return false; }
            if (preview != null) return true;
            if (!HouseEditValidation.Source(plan, out error)) return false;
            preview = new HouseEditPreview(plan);
            return true;
        }

        private bool Prepare(bool validateGeometry)
        {
            valid = false;
            // Callers check the source once before changing intent or preparing its copy.
            // Nothing in this synchronous preparation writes accepted native data.
            bool prepared;
            if (kind == Kind.SlabThickness)
            {
                if (slabPreview == null)
                {
                    var building = preview.SourceBuilding;
                    if (!HouseSlabLayout.TryPatches(building.Data, level => level.Plan.Data, out var patches, out error)) return false;
                    slabPreview = new HouseSlabPreview(preview, patches);
                }
                prepared = HouseSlabCommands.PrepareSlabSettings(preview, finish, total - finish,
                    preview.SourceBuilding.CeilingMaterial, preview.SourceBuilding.CeilingTextureSize,
                    out error, validateGeometry);
            }
            else
            {
                preview.ResetBuildingSettings();
                prepared = preview.Building.TrySetLevelHeight(preview.Level, heightValue, out error);
                if (prepared && validateGeometry) prepared = HouseEditValidation.UniformHeights(preview, out error);
            }
            valid = prepared;
            if (valid) error = "";
            return valid;
        }

        internal HouseEditResult Apply()
        {
            if (!IsProperty || !IsPending)
            {
                if (kind != Kind.Geometry) Discard();
                return new HouseEditResult(HouseEditStatus.Unchanged);
            }
            // The original target is authoritative even if its view/context has closed.
            // Publication validates this exact candidate and source again, outside capture.
            if (!EnsureCandidate() || !Prepare(false)) { Notify(); return HouseEditResult.Reject(Error); }
            string action = kind == Kind.LevelHeight ? "Change level wall height" : "Change slab thickness";
            var result = HouseEditCommit.PublishBuildingGeometry(preview, action);
            if (result.Status != HouseEditStatus.Rejected) Discard();
            else { error = result.Message; Notify(); }
            return result;
        }

        internal bool TryBeginGeometry(HousePlan target, out string reason)
        {
            reason = "Apply or discard the current property edit before changing geometry.";
            if (kind != Kind.None && (kind != Kind.Geometry || plan != target)) return false;
            if (kind == Kind.Geometry && preview != null)
            { reason = preview.HasConflict ? "The House changed during this edit." : ""; return reason.Length == 0; }
            Clear();
            if (!HouseEditValidation.Source(target, out reason)) return false;
            plan = target; kind = Kind.Geometry;
            preview = new HouseEditPreview(target);
            inputs = preview.OriginalInputs;
            Notify();
            return true;
        }
        internal void ReleaseGeometry() { if (kind == Kind.Geometry) Discard(); }

        internal void CheckSource()
        {
            if (kind == Kind.None || suspended && preview == null) return;
            if (plan == null || inputs != HouseEditInputs.SourceInputs(plan)) Interrupt();
        }

        internal void Interrupt()
        {
            if (!IsProperty || !IsPending) Discard();
            else
            {
                ReleaseCandidate();
                suspended = true;
                Notify();
            }
            // Borrowing tools must release their native capture and proposal points
            // when the candidate disappears unexpectedly, not only on the next drag.
            Interrupted?.Invoke();
        }
        internal bool Resume(HousePlan target)
        {
            if (!IsPending) { if (kind != Kind.None) Discard(); return true; }
            if (!IsProperty || plan != target || !EnsureCandidate()) return false;
            suspended = false;
            Prepare(true); // Invalid values remain editable; resumption does not discard intent.
            Notify();
            return true;
        }
        internal void DrawSlabPreview(SceneView view)
        {
            if (kind == Kind.SlabThickness && Valid && !Conflicted) slabPreview?.Draw(view);
        }
        internal void Discard()
        {
            bool changed = kind != Kind.None || preview != null;
            Clear();
            if (changed) Notify();
        }
        private void Clear()
        {
            ReleaseCandidate();
            kind = Kind.None; plan = null; inputs = null;
            suspended = false; error = "";
            baselineHeight = heightValue = baselineTotal = total = baselineFinish = finish = 0;
        }
        private void ReleaseCandidate()
        {
            slabPreview?.Dispose(); slabPreview = null;
            preview?.Dispose(); preview = null;
            valid = false;
        }
        private void Notify() { displayRevision++; Changed?.Invoke(); }
    }
}
