using System;
using UnityEngine;

namespace EnvironmentAuthoring
{
    // A source library. Placed runs keep their explicit panel and post choices.
    [CreateAssetMenu(menuName = "Environment/Fence Family")]
    public sealed class FenceFamily : ScriptableObject
    {
        public TownModule[] Panels = Array.Empty<TownModule>();
        [Serializable] public sealed class Post
        {
            public Mesh Mesh;
            public Material[] Materials = Array.Empty<Material>();
        }
        public Post[] Posts = Array.Empty<Post>();
    }
}
