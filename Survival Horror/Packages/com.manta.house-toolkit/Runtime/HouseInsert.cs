using UnityEngine;

namespace HouseToolkit
{
    public enum OpeningKind { Door, Window }

    [CreateAssetMenu(menuName = "House Toolkit/Insert")]
    public class HouseInsert : ScriptableObject
    {
        public OpeningKind kind;
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
