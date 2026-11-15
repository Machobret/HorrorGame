using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("")]
    public sealed class InsertAttachment : MonoBehaviour
    {
        public string openingId;
        public GameObject sourcePrefab;
        public Renderer finishRenderer;
        public Material lastFrame;
        public Material lastGlass;
    }
}
