using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    public sealed partial class HouseStack
    {
        public enum BooleanShape { Box = 0, Icosphere = 2 }

        [Serializable]
        public sealed class BooleanVolume
        {
            [SerializeField] private string id;
            [SerializeField] private BooleanShape shape;
            [SerializeField] private Vector3 center, size;
            [SerializeField] private Quaternion rotation;
            public string Id => id;
            public BooleanShape Shape => shape;
            public Vector3 Center => center;
            public Vector3 Size => size;
            public Quaternion Rotation => rotation;
            public Matrix4x4 Matrix => Matrix4x4.TRS(center, rotation, size);

            internal BooleanVolume(BooleanShape shape, Vector3 center, Quaternion rotation, Vector3 size, string id = null)
            { this.id = id ?? Guid.NewGuid().ToString("N"); this.shape = shape; this.center = center; this.rotation = rotation; this.size = size; }
            internal BooleanVolume Copy() => new BooleanVolume(shape, center, rotation, size, id);

            internal bool IsValid(out string reason)
            {
                bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
                reason = "Use a supported Boolean shape with finite position, rotation and positive dimensions of at least 0.01 m.";
                if (string.IsNullOrEmpty(id) || !Enum.IsDefined(typeof(BooleanShape), Shape)
                    || !Finite(center) || !Finite(size) || size.x < .01f || size.y < .01f || size.z < .01f
                    || !float.IsFinite(rotation.x) || !float.IsFinite(rotation.y) || !float.IsFinite(rotation.z) || !float.IsFinite(rotation.w)
                    || Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1) > .0001f) return false;
                reason = "";
                return true;
            }
        }

        // House-local volumes affect derived geometry only, never room topology.
        [SerializeField] private List<BooleanVolume> booleanVolumes = new List<BooleanVolume>();
        [SerializeField] private Material booleanCutMaterial;
        public Material BooleanCutMaterial => booleanCutMaterial;
        internal void SetBooleanCutMaterial(Material material) => booleanCutMaterial = material;
        internal void CopyBooleanSettings(HouseStack source)
        {
            booleanCutMaterial = source.booleanCutMaterial;
            booleanVolumes = source.booleanVolumes.Select(volume => volume.Copy()).ToList();
        }
        private ReadOnlyModelList<BooleanVolume> booleanView;
        public IReadOnlyList<BooleanVolume> BooleanVolumes => ReadOnlyModelList<BooleanVolume>.For(booleanVolumes, ref booleanView);

        internal bool TrySetBooleanVolume(string id, BooleanShape shape, Vector3 center, Quaternion rotation, Vector3 size, out string reason)
        {
            var volume = new BooleanVolume(shape, center, rotation, size, id);
            if (!volume.IsValid(out reason)) return false;
            int index = id == null ? -1 : booleanVolumes.FindIndex(item => item.Id == id);
            if (id != null && index < 0) { reason = "The Boolean shape no longer exists."; return false; }
            if (index < 0) booleanVolumes.Add(volume);
            else booleanVolumes[index] = volume;
            return true;
        }

        internal bool RemoveBooleanVolume(string id) => booleanVolumes.RemoveAll(volume => volume.Id == id) != 0;
        private bool BooleanVolumesValid(out string reason)
        {
            reason = "Boolean shapes need distinct identities.";
            if (booleanVolumes == null || booleanVolumes.Any(volume => volume == null)
                || booleanVolumes.Select(volume => volume.Id).Distinct().Count() != booleanVolumes.Count) return false;
            foreach (var volume in booleanVolumes) if (!volume.IsValid(out reason)) return false;
            reason = "";
            return true;
        }
    }
}
