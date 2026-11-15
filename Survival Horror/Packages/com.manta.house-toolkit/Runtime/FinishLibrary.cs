using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "House Toolkit/Finish Library")]
    public sealed class FinishLibrary : ScriptableObject
    {
        public Material[] materials = System.Array.Empty<Material>();
    }
}
