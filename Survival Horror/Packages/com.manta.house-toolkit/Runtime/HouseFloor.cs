using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [Serializable]
    public struct FloorCorner
    {
        public int id;
        public Vector2 point;
        public FloorCorner(int id, Vector2 point) { this.id = id; this.point = point; }
    }

    [Serializable]
    public sealed class SlabOpening
    {
        [HideInInspector] public string id;
        public Vector2[] points;
        public SlabOpening(Vector2[] points){id=Guid.NewGuid().ToString("N");this.points=points;}
        public static Vector2[] Rectangle(Rect r)=>new[]{r.min,new Vector2(r.xMax,r.yMin),r.max,new Vector2(r.xMin,r.yMax)};
    }

    public sealed class HouseFloor : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Min(0.01f)] public float height = 2.8f;
        public WallFamily defaultFamily, interiorFamily;
        public Material exteriorFinish, interiorFinish, floorFinish, ceilingFinish;
        [SerializeField,HideInInspector,UnityEngine.Serialization.FormerlySerializedAs("stairOpenings")]
        List<Rect> legacyRectangles;
        [HideInInspector] public List<SlabOpening> floorOpenings=new List<SlabOpening>();
        [HideInInspector] public List<SlabOpening> roofOpenings=new List<SlabOpening>();
        public void OnBeforeSerialize(){}
        public void OnAfterDeserialize()
        {
            floorOpenings ??= new List<SlabOpening>();
            roofOpenings ??= new List<SlabOpening>();
            if (legacyRectangles != null)
            {
                foreach (var rect in legacyRectangles) floorOpenings.Add(new SlabOpening(SlabOpening.Rectangle(rect)));
                legacyRectangles.Clear();
            }
            // Older scenes stored only the polygon. Its identity must survive subsequent Undo deserialization.
            foreach (var opening in floorOpenings) EnsureId(opening);
            foreach (var opening in roofOpenings) EnsureId(opening);

            static void EnsureId(SlabOpening opening)
            {
                if (opening != null && string.IsNullOrEmpty(opening.id)) opening.id = Guid.NewGuid().ToString("N");
            }
        }
        public Material exteriorGlass, interiorGlass;
        public TrimFamily trim;
        [HideInInspector] public string floorId;
        [HideInInspector] public List<FloorCorner> corners = new List<FloorCorner>();
        [HideInInspector] public int nextCorner = 1;
        public House House => GetComponentInParent<House>();
        public Vector2 Position(int id) => corners.Find(c => c.id == id).point;
        public Vector3 World(Vector2 p) => transform.TransformPoint(new Vector3(p.x, 0, p.y));
        public Vector2 Local(Vector3 p) { var v = transform.InverseTransformPoint(p); return new Vector2(v.x, v.z); }
        public WallFamily FamilyFor(bool interior)
        {
            var house = House;
            if (interior && interiorFamily) return interiorFamily;
            if (interior && house && house.interiorFamily) return house.interiorFamily;
            if (defaultFamily) return defaultFamily;
            return house ? house.exteriorFamily : null;
        }
        public Material GlassFor(bool interior)
        {
            var local = interior ? interiorGlass : exteriorGlass;
            if (local || !House) return local;
            return interior ? House.interiorGlass : House.exteriorGlass;
        }
        public Material FinishFor(bool interior)
        {
            var local = interior ? interiorFinish : exteriorFinish;
            if (local || !House) return local;
            return interior ? House.interiorFinish : House.exteriorFinish;
        }
    }
}
