using System;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // Native scene/Undo ownership shared by the three focused generators.
    public abstract class TownAuthoring : MonoBehaviour
    {
        [SerializeField, HideInInspector] private TownPath path = new TownPath();
        [NonSerialized] private TownOutput output;
        public TownPath Path => path;
        internal void SetPath(TownPath value) => path = value;
        // The generated root's Owner is the saved relationship. Do not store a
        // second derived reference in the authored object's Undo snapshots.
        internal TownOutput Output
        {
            get => output != null && output.Owner == this ? output
                : output = GetComponentsInChildren<TownOutput>(true).FirstOrDefault(item => item.Owner == this);
            set => output = value;
        }
        public bool HasSupportedTransform => Vector3.Dot(transform.up, Vector3.up) > .99999f && (transform.lossyScale - Vector3.one).sqrMagnitude < .000001f;
        // Commands and output read the same authored validity. Native editability,
        // crossing interpretation and derived geometry stay in their editor owners.
        internal bool TryValidatePath(TownPath candidate, out string reason)
        {
            if (!candidate.Valid(out reason)) return false;
            try
            {
                if (this is TownWallRun wall)
                {
                    if (candidate.Spans.Count > 0 && wall.Model != null) TownModule.CheckCompatibility(wall.Model, TownModule.ModuleKind.WallPanel);
                    foreach (var span in candidate.Spans.Where(span => span.Model != null))
                        TownModule.CheckCompatibility(span.Model, TownModule.ModuleKind.WallPanel);
                }
            }
            catch (ArgumentException error) { reason = error.Message; return false; }
            reason = ""; return true;
        }

        internal bool TryValidate(out string reason)
        {
            if (!TryValidatePath(path, out reason)) return false;
            bool Positive(float value) => float.IsFinite(value) && value >= .01f;
            reason = "Dimensions must be finite and at least 0.01 metres.";
            if (this is TownWallRun wall && (!Positive(wall.Height) || !Positive(wall.PostWidth) || !float.IsFinite(wall.PostHeight) || wall.PostHeight < 0)) return false;
            if (this is TownSidewalk walk && (!Positive(walk.Width) || !Positive(walk.Thickness) || !Positive(walk.MetresPerRepeat) || !Positive(walk.CurbHeight))) return false;
            if (this is TownRoad road && (!Positive(road.Width) || !Positive(road.Thickness) || !Positive(road.MetresPerRepeat) || !Positive(road.SidewalkWidth) || !Positive(road.SidewalkHeight) || !Positive(road.CurbHeight))) return false;
            reason = ""; return true;
        }

        public static event Action<TownAuthoring> Changed;
        protected virtual void OnEnable() => Changed?.Invoke(this);
        protected virtual void OnValidate() => Changed?.Invoke(this);
        protected virtual void OnDestroy() => Changed?.Invoke(this);
    }
}
