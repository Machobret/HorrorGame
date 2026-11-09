using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    // Native output lifetimes only. Commands retain ownership of authored edits/groups.
    internal static class GeneratedOutputUndo
    {
        private static HashSet<Object> original;
        private static readonly HashSet<Object> retired = new HashSet<Object>();

        internal static bool IsPublishing => original != null;

        internal static void Run(GameObject owner, Func<GameObject[]> roots, Action build, string action)
        {
            if (original != null) throw new InvalidOperationException("Generated output publication cannot nest.");
            var before = Components(roots());

            original = new HashSet<Object>(before.Cast<Object>().Concat(before.Select(c => (Object)c.gameObject)).Concat(owner.GetComponentsInChildren<Transform>(true).Select(t => (Object)t.gameObject)));
            try
            {
                // Unity owns snapshots of existing output. Source objects are outside
                // these roots and already have their command's authored record.
                foreach (var root in roots()) Undo.RegisterFullObjectHierarchyUndo(root, action);
                build();
            }
            finally
            {
                try
                {
                    // Even a failed builder may have created children. Publish their
                    // native lifetimes before propagating the exception, so Undo can
                    // restore the authored action without dangling objects.
                    var after = Components(roots());
                    var created = new HashSet<GameObject>(after.Select(component => component.gameObject).Where(go => !original.Contains(go)));
                    foreach (var go in created)
                        if (go.transform.parent == null || !created.Contains(go.transform.parent.gameObject))
                            Undo.RegisterCreatedObjectUndo(go, action);
                    Undo.FlushUndoRecordObjects();
                    foreach (var target in retired)
                        if (target != null) Undo.DestroyObjectImmediate(target);
                    Undo.SetCurrentGroupName(action);
                    GeneratedMeshResources.RequestPrune();
                    GeneratedMeshResources.Prune();
                }
                finally { retired.Clear(); original = null; }
            }
        }

        internal static void Destroy(Object target)
        {
            if (original != null && original.Contains(target)) retired.Add(target);
            else Object.DestroyImmediate(target);
        }

        internal static void Remove(GameObject root) => GeneratedMeshResources.ReleaseOwnedRoot(root, Destroy);

        internal static T AddComponent<T>(GameObject target) where T : Component
        {
            if (original == null || !original.Contains(target)) return target.AddComponent<T>();
            // The component exists in native history before its immutable mesh/material
            // references are initialized. Capture that initialization as one record.
            var component = Undo.AddComponent<T>(target);
            Undo.RegisterCompleteObjectUndo(component, "Initialize generated component");
            return component;
        }

        private static Component[] Components(GameObject[] roots) => roots
            .Where(root => root != null).SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .Where(component => component != null).Distinct().ToArray();
    }
}
