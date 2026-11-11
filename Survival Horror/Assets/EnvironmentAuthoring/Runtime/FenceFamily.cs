using System;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // A source library. Placed runs keep their explicit panel and post choices.
    [CreateAssetMenu(menuName = "Environment/Fence Family")]
    public sealed class FenceFamily : WallGeometryFamily
    {
        public TownModule[] Panels = Array.Empty<TownModule>();
        [Serializable] public sealed class Post
        {
            public Mesh Mesh;
            public Material[] Materials = Array.Empty<Material>();
        }
        public Post[] Posts = Array.Empty<Post>();
        public TownModule Panel => Panels?.FirstOrDefault(panel => panel != null);
        public Post DefaultPost => Posts?.FirstOrDefault(post => post?.Mesh != null);

        public bool IsValid(out string reason)
        {
            try { TownModule.CheckCompatibility(Panel, TownModule.ModuleKind.WallPanel); }
            catch (ArgumentException error) { reason = error.Message; return false; }
            reason = "";
            return true;
        }
    }
}
