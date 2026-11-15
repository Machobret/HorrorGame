using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseToolkit.Editor
{
    internal static class BooleanOutput
    {
        const string Action = "Edit Boolean cut";
        const int GeometryVersion = 2;

        internal static bool Change(House house, BooleanCut value, string remove, out string issue)
        {
            if (value != null && !value.IsValid(out issue)) return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Action);
            try
            {
                Undo.RecordObject(house, Action);
                if (value == null) house.booleanCuts.RemoveAll(c => c.id == remove);
                else
                {
                    int index = house.booleanCuts.FindIndex(c => c.id == value.id);
                    if (index < 0) house.booleanCuts.Add(value); else house.booleanCuts[index] = value;
                }
                Undo.FlushUndoRecordObjects();
                Rebuild(house);
                Undo.CollapseUndoOperations(group);
                EditorUtility.SetDirty(house);
                issue = null;
                return true;
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                issue = error.Message;
                return false;
            }
        }

        internal static void Upgrade(House house)
        {
            if (house.booleanCuts.Count == 0 || house.booleanGeometryVersion >= GeometryVersion) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                Undo.RegisterCompleteObjectUndo(house, Action);
                Rebuild(house);
                Undo.CollapseUndoOperations(group);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogWarning($"Could not update Boolean cuts on {house.name}. {error.Message}", house);
            }
        }

        internal static void Rebuild(House house)
        {
            foreach (var output in house.GetComponentsInChildren<WallOutput>(true)) Apply(house, output);
            foreach (var wall in house.GetComponentsInChildren<HouseWall>(true)) ApplyWindows(house, wall);
            foreach (var slab in house.GetComponentsInChildren<HouseSlab>(true)) Apply(house, slab);
            foreach (var joint in house.GetComponentsInChildren<CornerJoint>(true)) Apply(house, joint);
            house.booleanGeometryVersion = GeometryVersion;
        }

        internal static void ApplyWindows(House house, HouseWall wall)
        {
            foreach (var binding in wall.GetComponentsInChildren<InsertAttachment>(true))
                if (wall.openings.Exists(o => o.id == binding.openingId && o.insert && o.insert.kind == OpeningKind.Window))
                    Apply(house, binding);
        }

        internal static void Apply(House house, Component root)
        {
            if (!house || !root) return;
            house.InvalidateProjection();
            var owners = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.transform)
                .Concat(root.GetComponentsInChildren<Collider>(true).Where(Supported).Select(c => c.transform))
                .Distinct().Where(t => !t.GetComponentInParent<BooleanCutResult>(true)).ToArray();
            foreach (var owner in owners) ApplySource(house, root, owner);
        }

        static bool Supported(Collider c) => !c.isTrigger && (c is BoxCollider || c is MeshCollider);

        static Bounds LocalBounds(Collider c)
        {
            if (c is BoxCollider box) return new Bounds(box.center, box.size);
            return ((MeshCollider)c).sharedMesh.bounds;
        }

        static void ApplySource(House house, Component root, Transform owner)
        {
            var state = owner.Cast<Transform>().Select(t => t.GetComponent<BooleanCutResult>()).FirstOrDefault(s => s);
            var renderer = owner.GetComponent<MeshRenderer>();
            var filter = owner.GetComponent<MeshFilter>();
            var mesh = renderer && filter && (renderer.enabled || (state && state.GetComponent<MeshRenderer>()))
                ? filter.sharedMesh : null;
            var colliders = owner.GetComponents<Collider>().Where(c => Supported(c) &&
                (c.enabled || (state && state.originalColliders.Contains(c))) &&
                (!(c is MeshCollider model) || model.sharedMesh)).ToArray();
            var matrix = owner.worldToLocalMatrix * house.transform.localToWorldMatrix;
            var cuts = house.booleanCuts.Where(c =>
            {
                var bounds = HouseBooleanMesh.PrimitiveBounds(matrix * c.Matrix);
                return (mesh && mesh.bounds.Intersects(bounds)) || colliders.Any(col => LocalBounds(col).Intersects(bounds));
            }).ToArray();
            if (cuts.Length == 0)
            {
                if (state) Restore(renderer, state);
                return;
            }

            var key = Hash128.Compute(ref matrix);
            key.Append(GeometryVersion);
            foreach (var c in cuts)
            {
                var placement = c.Matrix;
                key.Append(ref placement);
                key.Append((int)c.shape);
            }
            foreach (var collider in colliders)
            {
                var bounds = LocalBounds(collider);
                key.Append(collider.GetInstanceID());
                key.Append(ref bounds);
                if (collider is MeshCollider model) key.Append(model.sharedMesh.GetInstanceID());
            }
            if (state && state.lastSource == mesh && state.signature == key)
            {
                if (state.TryGetComponent<MeshRenderer>(out var visible)) SetMaterials(house, renderer, visible);
                return;
            }

            var panel = owner.GetComponent<AuthoredPanel>();
            bool splitGlass = mesh && mesh.subMeshCount == 2 && (panel ||
                (root is InsertAttachment insert && insert.finishRenderer == renderer));
            var result = mesh ? HouseBooleanMesh.SubtractAll(mesh, cuts, matrix, owner.GetComponent<HouseSlab>(), splitGlass, panel) : null;
            if (state) Restore(renderer, state);
            var clippedColliders = colliders.Where(col => cuts.Any(c =>
                LocalBounds(col).Intersects(HouseBooleanMesh.PrimitiveBounds(matrix * c.Matrix)))).ToArray();
            if (result == mesh && clippedColliders.Length == 0) return;

            var go = new GameObject("Boolean result");
            go.transform.SetParent(owner, false);
            go.layer = owner.gameObject.layer;
            state = go.AddComponent<BooleanCutResult>();
            state.originalColliders = clippedColliders;
            state.lastSource = mesh;
            state.signature = key;
            if (result != mesh)
            {
                Undo.RegisterCreatedObjectUndo(result, Action);
                go.AddComponent<MeshFilter>().sharedMesh = result;
                SetMaterials(house, renderer, go.AddComponent<MeshRenderer>());
                Undo.RecordObject(renderer, Action);
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (var collider in clippedColliders)
            {
                Undo.RecordObject(collider, Action);
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            try { AddCollision(go, clippedColliders, mesh, result, cuts, matrix); }
            finally { Undo.RegisterCreatedObjectUndo(go, Action); }
        }

        static void AddCollision(GameObject go, Collider[] colliders, Mesh source, Mesh result, BooleanCut[] cuts, Matrix4x4 matrix)
        {
            foreach (var original in colliders)
            {
                if (original is BoxCollider box)
                {
                    // Legacy glazing uses thin solid boxes for collision, even where the visual is a single sheet.
                    var boxMesh = HouseBooleanMesh.ColliderBox(box);
                    Mesh collision;
                    try { collision = HouseBooleanMesh.SubtractAll(boxMesh, cuts, matrix); }
                    catch { Object.DestroyImmediate(boxMesh); throw; }
                    if (boxMesh != collision) Object.DestroyImmediate(boxMesh);
                    if (collision.vertexCount == 0) { Object.DestroyImmediate(collision); continue; }
                    Undo.RegisterCreatedObjectUndo(collision, Action);
                    CreateCollider(go, original, collision);
                }
                else if (original is MeshCollider model && model.sharedMesh)
                {
                    var collision = model.sharedMesh == source ? result : HouseBooleanMesh.SubtractAll(model.sharedMesh, cuts, matrix);
                    if (collision.vertexCount == 0)
                    {
                        if (collision != result && collision != model.sharedMesh) Object.DestroyImmediate(collision);
                        continue;
                    }
                    if (collision != result && collision != model.sharedMesh) Undo.RegisterCreatedObjectUndo(collision, Action);
                    CreateCollider(go, original, collision);
                }
            }
        }

        static void CreateCollider(GameObject go, Collider original, Mesh collision)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = collision;
            collider.sharedMaterial = original.sharedMaterial;
            collider.contactOffset = original.contactOffset;
            collider.includeLayers = original.includeLayers;
            collider.excludeLayers = original.excludeLayers;
            collider.layerOverridePriority = original.layerOverridePriority;
        }

        static void Restore(MeshRenderer source, BooleanCutResult state)
        {
            var result = state.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (source && state.GetComponent<MeshRenderer>())
            {
                Undo.RecordObject(source, Action);
                source.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            }
            foreach (var c in state.originalColliders)
                if (c) { Undo.RecordObject(c, Action); c.enabled = true; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
            var originals = state.originalColliders.OfType<MeshCollider>().Select(c => c.sharedMesh).ToArray();
            var generated = state.GetComponents<MeshCollider>().Select(c => c.sharedMesh).Append(result).Except(originals).ToArray();
            Undo.DestroyObjectImmediate(state.gameObject);
            foreach (var mesh in generated)
                if (mesh && !EditorUtility.IsPersistent(mesh)) Undo.DestroyObjectImmediate(mesh);
        }

        internal static void RefreshMaterials(House house, Component root)
        {
            foreach (var result in root.GetComponentsInChildren<BooleanCutResult>(true))
                if (result.TryGetComponent<MeshRenderer>(out var display))
                    SetMaterials(house, result.transform.parent.GetComponent<MeshRenderer>(), display);
        }

        static void SetMaterials(House house, MeshRenderer source, MeshRenderer display)
        {
            var slots = source.sharedMaterials;
            var finish = house.cutMaterial ? house.cutMaterial : source.sharedMaterial;
            var materials = slots.Append(finish).ToArray();
            if (!display.sharedMaterials.SequenceEqual(materials)) display.sharedMaterials = materials;
        }
    }
}
