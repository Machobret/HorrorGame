using UnityEngine;

namespace HouseToolkit
{
    public enum OpeningKind { Door, Window, Panel }

    [CreateAssetMenu(menuName = "House Toolkit/Insert")]
    public class HouseInsert : ScriptableObject
    {
        public OpeningKind kind;
        [Tooltip("For custom panels, an optional fixed-width frame. Its aperture defines the size and pivot of the optional insert prefab.")]
        [HideInInspector] public WallSegment panelSegment;
        public WallSegment PanelSegment => kind == OpeningKind.Panel ? panelSegment : null;
        public Vector2 OpeningSize => PanelSegment ? (panelSegment.aperture.size == Vector2.zero ? panelSegment.size : panelSegment.aperture.size) : opening;
        [Tooltip("Rough opening width and height in metres. Pivot at the bottom centre.")]
        public Vector2 opening = new Vector2(0.9f, 2.1f);
        public GameObject prefab;
        [Tooltip("Optional prefab renderer. Its first two slots can inherit the host frame and glass.")]
        public Renderer finishRenderer;
        [Min(0)] public float minimumThickness;
        [Tooltip("Zero accepts any maximum wall thickness.")]
        [Min(0)] public float maximumThickness;
    }
}
