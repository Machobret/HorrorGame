using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    // Leases keep transient meshes alive across houses while any renderer still uses them.
    internal sealed class SharedGeometry
    {
        static readonly Dictionary<Hash128, SharedGeometry> meshes = new Dictionary<Hash128, SharedGeometry>();
        readonly Hash128 key;
        internal readonly Mesh mesh;
        int references;

        SharedGeometry(Hash128 key, Mesh mesh)
        {
            this.key = key;
            this.mesh = mesh;
            mesh.hideFlags = HideFlags.DontSave;
        }

        internal static Mesh Use(ref SharedGeometry current, Hash128 key, Func<Mesh> build)
        {
            if (current != null && current.key == key) return current.mesh;
            Release(ref current);
            if (!meshes.TryGetValue(key, out current))
            {
                current = new SharedGeometry(key, build());
                meshes.Add(key, current);
            }
            current.references++;
            return current.mesh;
        }

        internal static void Release(ref SharedGeometry current)
        {
            if (current == null) return;
            if (--current.references == 0)
            {
                meshes.Remove(current.key);
                if (Application.isPlaying) UnityEngine.Object.Destroy(current.mesh);
                else UnityEngine.Object.DestroyImmediate(current.mesh);
            }
            current = null;
        }
    }
}
