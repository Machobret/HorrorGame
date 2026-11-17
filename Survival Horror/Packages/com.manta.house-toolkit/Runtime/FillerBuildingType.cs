using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName="Procedural Environment/Filler Building Type")]
    public sealed class FillerBuildingType : ScriptableObject
    {
        public Material facade;
        [Tooltip("Optional solid ground-floor finish. The supplied facade atlas already contains a blank wall.")]
        public Material solidFacade;
        [Tooltip("Storefront materials offered in the wall selection dropdown. Any filler building can use them.")]
        public Material[] storefronts=System.Array.Empty<Material>();
        [Tooltip("Optional material above the ground floor. Uses the same atlas row.")] public Material upperFacade;
        [Range(0,4)] public int atlasRow;
        [Min(.1f)] public float preferredBayWidth=3;
        [Tooltip("Ground-floor clear height in metres. Zero uses the building's clear height.")]
        [Min(0)] public float groundFloorHeight;
        [Tooltip("Start with a painted entrance on the longest wall wide enough for one full bay.")]
        public bool placeEntrance;
    }
}
