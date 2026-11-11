using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Only meshes explicitly created as derived output belong to this owner.
    // Authored assets and unknown painted overrides must never be inferred from names.
    [InitializeOnLoad]
    internal static class GeneratedMeshResources
    {
        private static readonly HashSet<Mesh> owned = new HashSet<Mesh>();
        private static readonly Dictionary<string, Mesh> cached = new Dictionary<string, Mesh>();
        private static readonly HashSet<int> sceneMeshes = new HashSet<int>();
        private static bool dirty = true; // Reconcile serialized owners once after a reload.

        static GeneratedMeshResources()
        {
            // Undo can restore serialized owners before hierarchyChanged is delivered.
            // Restore disposal ownership before a same-frame edit releases output.
            Undo.undoRedoPerformed += RequestPrune;
            EditorApplication.hierarchyChanged += RequestPrune;
        }

        // Native hierarchy/scene changes can remove owners without going through a builder.
        // Queue collection for the end of a coordinator pass; never destroy from the callback.
        internal static void RequestPrune() => dirty = true;

        internal static Mesh Register(Mesh mesh)
        {
            if (mesh != null && !EditorUtility.IsPersistent(mesh) && owned.Add(mesh)) dirty = true;
            return mesh;
        }

        internal static Mesh FindCached(string name)
        {
            if (cached.TryGetValue(name, out var mesh) && mesh != null
                && owned.Contains(mesh) && !EditorUtility.IsPersistent(mesh)) return mesh;
            cached.Remove(name);
            mesh = owned.FirstOrDefault(m => m != null && m.name == name && !EditorUtility.IsPersistent(m));
            if (mesh == null)
            {
                // Painted clones can keep the original mesh name. Only serialized creation
                // records prove which loaded mesh is the reusable base variant after reload.
                mesh = Resources.FindObjectsOfTypeAll<GeneratedMeshOwnership>().SelectMany(m => m.Meshes)
                    .FirstOrDefault(m => m != null && m.name == name && !EditorUtility.IsPersistent(m));
                if (mesh != null) Register(mesh);
            }
            if (mesh != null) cached[name] = mesh;
            return mesh;
        }

        internal static string SourceKey(Mesh mesh)
        {
            if (owned.Contains(mesh) && cached.TryGetValue(mesh.name, out var retained) && retained == mesh)
                return mesh.name;
            return GeneratedMeshResources.SourceSignature(mesh);
        }

        internal static string SourceSignature(Mesh mesh)
        {
            if (mesh == null) return "no-mesh";
            // Imported files can contain several meshes. Include the sub-asset ID and
            // dependency hash so replacing or reimporting an authored source refreshes it.
            string path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path)) return "unsaved:" + MeshContentSignature(mesh);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
            return guid + ":" + localId + ":" + AssetDatabase.GetAssetDependencyHash(path);
        }

        private static Hash128 MeshContentSignature(Mesh mesh)
        {
            // Non-asset sources can change without changing identity. Hash their raw
            // streams, not a converted vertex format that could lose UV dimensions.
            // The editor snapshot also supports meshes with Read/Write disabled.
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
            var data = snapshot[0];
            var hash = Hash128.Compute("mesh-content-v1");
            hash.Append(data.vertexCount);
            hash.Append((int)data.indexFormat);
            hash.Append(mesh.GetVertexAttributes());
            hash.Append(data.vertexBufferCount);
            for (int stream = 0; stream < data.vertexBufferCount; stream++)
            {
                hash.Append(data.GetVertexBufferStride(stream));
                var bytes = data.GetVertexData<byte>(stream);
                hash.Append(bytes.Length);
                hash.Append(bytes);
            }
            hash.Append(data.GetIndexData<byte>());
            hash.Append(data.subMeshCount);
            for (int i = 0; i < data.subMeshCount; i++)
            {
                var submesh = data.GetSubMesh(i);
                hash.Append(ref submesh);
            }
            var bounds = mesh.bounds;
            hash.Append(ref bounds);
            return hash;
        }

        internal static bool IsModuleSource(GameObject part, Mesh mesh)
        {
            var townPart = part.GetComponent<TownOutputPart>();
            if (townPart != null && townPart.Source == mesh) return true;
            var module = part.GetComponentInParent<WallModule>(true);
            var definition = module != null ? module.Definition : null;
            return definition != null && mesh == definition.Body
                || module != null && module.Insert != null && mesh == module.Insert.Mesh;
        }

        internal static Mesh Cache(string name, Mesh mesh)
        {
            Register(mesh);
            cached[name] = mesh;
            return mesh;
        }

        internal static void RecordOwnedHierarchy(GameObject root, System.Func<GameObject, GeneratedMeshOwnership> createMarker = null)
        {
            if (root == null) return;
            // The marker follows the mesh-bearing part, not a long-lived output root.
            // Replacing a group must release its former variants instead of retaining history.
            var parts = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.gameObject)
                .Concat(root.GetComponentsInChildren<MeshCollider>(true).Select(c => c.gameObject)).Distinct();
            foreach (var part in parts)
            {
                var marker = part.GetComponent<GeneratedMeshOwnership>();
                if (marker != null) Restore(marker);
                var filter = part.GetComponent<MeshFilter>();
                var collider = part.GetComponent<MeshCollider>();
                // Unity can return a fake-null editor object for a missing component.
                // C# ?. bypasses Unity's null comparison and would still call sharedMesh.
                var cut = part.GetComponent<HouseBooleanMeshState>();
                var meshes = new[] { filter != null ? filter.sharedMesh : null, collider != null ? collider.sharedMesh : null,
                    cut != null ? cut.Source : null, cut != null ? cut.CollisionSource : null };
                // Saving a generated mesh as an asset transfers its disposal to the
                // AssetDatabase, but does not erase proof that this binding is generated.
                var current = meshes.Where(m => m != null && (owned.Contains(m) && !EditorUtility.IsPersistent(m)
                    || marker != null && marker.Meshes.Contains(m))).Distinct().ToList();
                if (current.Count > 0)
                {
                    if (marker == null)
                    {
                        marker = createMarker != null ? createMarker(part) : part.AddComponent<GeneratedMeshOwnership>();
                        marker.hideFlags |= HideFlags.HideInInspector;
                    }
                    if (!marker.Meshes.SequenceEqual(current))
                    {
                        marker.Meshes = current;
                        foreach (var mesh in current) sceneMeshes.Add(mesh.GetInstanceID());
                        EditorUtility.SetDirty(marker);
                        dirty = true;
                    }
                }
                else if (marker != null && marker.Meshes.Count > 0 && meshes.All(m => m == null
                    || IsModuleSource(part, m)))
                {
                    marker.Meshes.Clear();
                    EditorUtility.SetDirty(marker);
                    dirty = true;
                }
                // An unknown replacement, including a saved asset, may be painted. Preserve
                // the preceding ownership record rather than claiming or deleting it.
            }
        }

        private static void Restore(GeneratedMeshOwnership marker)
        {
            foreach (var mesh in marker.Meshes)
            {
                Register(mesh);
                if (mesh != null) sceneMeshes.Add(mesh.GetInstanceID());
            }
        }

        internal static void ReleaseOwnedRoot(GameObject root, System.Action<GameObject> destroy = null)
        {
            if (root == null) return;
            // Restore before deleting the marker: its serialized record survives reload.
            foreach (var marker in root.GetComponentsInChildren<GeneratedMeshOwnership>(true)) Restore(marker);
            if (destroy != null) destroy(root);
            else Object.DestroyImmediate(root);
            // Wait until the whole output pass has attached its replacement references.
            // This also permits the next placement to reuse an equivalent cached variant.
            dirty = true;
        }

        internal static void ReleaseUnused(Mesh mesh)
        {
            if (mesh == null || !owned.Contains(mesh)) return;
            ReleaseUnused(new[] { mesh }, LiveReferences());
        }

        internal static void Prune()
        {
            // Ordinary audits often have no generation work; avoid a global object scan then.
            if (!dirty) return;
            foreach (var marker in Resources.FindObjectsOfTypeAll<GeneratedMeshOwnership>()) Restore(marker);
            dirty = false;
            sceneMeshes.RemoveWhere(id => EditorUtility.EntityIdToObject(id) == null);
            var referenced = LiveReferences();
            ReleaseUnused(owned.ToArray(), referenced);
            // Retire unknown cache entries too, but do not destroy their mesh objects.
            foreach (var key in cached.Where(p => p.Value == null || !referenced.Contains(p.Value)).Select(p => p.Key).ToArray())
                cached.Remove(key);
        }

        private static void ReleaseUnused(IEnumerable<Mesh> candidates, HashSet<Mesh> referenced)
        {
            foreach (var mesh in candidates)
            {
                if (mesh == null) { owned.Remove(mesh); continue; }
                if (!owned.Contains(mesh)) continue;
                // Registering a formerly generated mesh as an asset transfers its ownership.
                if (EditorUtility.IsPersistent(mesh)) { owned.Remove(mesh); continue; }
                if (referenced.Contains(mesh)) continue;
                owned.Remove(mesh);
                foreach (var key in cached.Where(p => p.Value == mesh).Select(p => p.Key).ToArray()) cached.Remove(key);
                // Scene/native history can restore references absent from live output,
                // including after reload. Evict caches; Unity collects scene meshes
                // when no scene or history references remain. Unstaged work retires now.
                if (!sceneMeshes.Contains(mesh.GetInstanceID())) Object.DestroyImmediate(mesh);
            }
        }

        private static HashSet<Mesh> LiveReferences()
        {
            // Include inactive output, preview scenes, collider-only output and prefab objects.
            return new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<MeshFilter>().Select(f => f.sharedMesh)
                .Concat(Resources.FindObjectsOfTypeAll<MeshCollider>().Select(c => c.sharedMesh))
                .Concat(Resources.FindObjectsOfTypeAll<GeneratedMeshOwnership>().SelectMany(m => m.Meshes))
                .Where(m => m != null));
        }
    }
}
