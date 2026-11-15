using System;
using UnityEngine;

namespace HouseToolkit
{
    public enum CutShape { Box, Icosphere }

    [Serializable]
    public sealed class BooleanCut
    {
        public string id = Guid.NewGuid().ToString("N");
        public CutShape shape;
        public Vector3 center;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 size = Vector3.one;
        public CutShape Shape => shape;
        public Matrix4x4 Matrix => Matrix4x4.TRS(center, rotation, size);
        public BooleanCut Copy() => (BooleanCut)MemberwiseClone();
        public bool IsValid(out string issue)
        {
            bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
            issue = "Choose a box or sphere with a finite position, rotation and dimensions of at least 1 cm.";
            if (string.IsNullOrEmpty(id) || !Enum.IsDefined(typeof(CutShape), shape) || !Finite(center) || !Finite(size) ||
                size.x < .01f || size.y < .01f || size.z < .01f ||
                !float.IsFinite(Quaternion.Dot(rotation, rotation)) || Mathf.Abs(Quaternion.Dot(rotation, rotation)-1) > .0001f) return false;
            issue = null;
            return true;
        }
    }
}
