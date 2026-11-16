using UnityEngine;

namespace HouseToolkit
{
    [CreateAssetMenu(menuName = "House Toolkit/Garage Door Insert")]
    public sealed class GarageDoorInsert : HouseInsert
    {
        public const float Jamb = .08f, Header = .16f;
        public bool operable;
        public Texture2D atlas;
        public Vector2 ClearSize(Vector2 size) => size.Equals(Vector2.zero)
            ? opening - new Vector2(2 * Jamb, Header) : size;
        public Vector2 RoughSize(Vector2 size) => ClearSize(size) + new Vector2(2 * Jamb, Header);
        public bool ValidSize(Vector2 size)
        {
            var clear = ClearSize(size);
            return float.IsFinite(clear.x) && clear.x > 0 && float.IsFinite(clear.y) && clear.y > 0;
        }
    }
}
