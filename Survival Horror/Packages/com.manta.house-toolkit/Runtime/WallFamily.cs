using UnityEngine;

namespace HouseToolkit
{
    // Authored through the Plain and Complex asset menus.
    public sealed class WallFamily : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("thickness"), Min(.01f)] float plainThickness = .15f;
        [HideInInspector] public bool complex;
        public bool IsComplex => complex || panel;
        public float thickness { get => panel && panel.mesh ? panel.mesh.bounds.size.z : plainThickness; set => plainThickness = value; }
        public PanelLayout defaultPanelLayout = PanelLayout.EvenSpacing;
        public Material surface;
        public Material glass;
        [Tooltip("Additional finishes offered in House Properties for this family.")]
        public Material[] surfaceFinishes = System.Array.Empty<Material>();
        public Material[] glassFinishes = System.Array.Empty<Material>();
        [Tooltip("Repeating wall panels, measured from their authored geometry. Even Spacing repeats the first panel.")]
        public WallSegment[] panels = System.Array.Empty<WallSegment>();
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("panel")]
        WallSegment legacyPanel;

        public WallSegment panel
        {
            get => panels != null && panels.Length != 0 ? panels[0] : null;
            set => panels = value ? new[] { value } : System.Array.Empty<WallSegment>();
        }

        public void OnBeforeSerialize() {}
        public void OnAfterDeserialize()
        {
            if ((panels == null || panels.Length == 0) && !ReferenceEquals(legacyPanel, null))
                panels = new[] { legacyPanel };
            legacyPanel = null;
        }
        [Tooltip("Optional opaque corner post for any wall angle. Center its footprint on X/Z, with the base at Y=0 and local X along the wall.")]
        public Mesh cornerPost;
        public WallSegment[] surrounds = System.Array.Empty<WallSegment>();
        public HouseInsert[] defaultInserts = System.Array.Empty<HouseInsert>();

        public WallSegment FindSurround(HouseInsert insert)
        {
            foreach (var segment in surrounds)
                if (segment && segment.kind == insert.kind &&
                    Mathf.Abs(segment.aperture.width - insert.opening.x) < 0.0005f &&
                    Mathf.Abs(segment.aperture.height - insert.opening.y) < 0.0005f)
                    return segment;
            return null;
        }
    }
}
