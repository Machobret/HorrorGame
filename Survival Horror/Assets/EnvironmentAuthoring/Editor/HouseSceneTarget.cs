namespace EnvironmentAuthoring.Editor
{
    // One hover winner per GUI event. Input and feedback use this same result;
    // persistent selection is separate and never disappears when another target wins.
    internal readonly struct HouseSceneTarget
    {
        internal readonly HouseSceneHit? Element;
        internal readonly HouseSceneQueries.SlabHit? Cut;
        internal readonly bool Ceiling;

        private HouseSceneTarget(HouseSceneHit? element, HouseSceneQueries.SlabHit? cut, bool ceiling)
        { Element = element; Cut = cut; Ceiling = ceiling; }

        internal static HouseSceneTarget ForElement(HouseSceneHit? hit) => new HouseSceneTarget(hit, null, false);
        internal static HouseSceneTarget ForCut(HouseSceneQueries.SlabHit? hit, bool ceiling) => new HouseSceneTarget(null, hit, ceiling);
    }
}
