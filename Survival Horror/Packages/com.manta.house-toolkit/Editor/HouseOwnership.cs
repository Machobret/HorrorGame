using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    // Membership belongs to the generated object. Children added by a level designer
    // do not acquire it just because their parent is a piece of architecture.
    internal static class HouseOwnership
    {
        internal static bool IsArchitecture(GameObject item)
        {
            if (item.TryGetComponent<House>(out _) || item.TryGetComponent<HouseFloor>(out _) ||
                item.TryGetComponent<HouseRoom>(out _) || item.TryGetComponent<HouseWall>(out _) ||
                item.TryGetComponent<HouseSlab>(out _) || item.TryGetComponent<CornerJoint>(out _) ||
                item.TryGetComponent<WallOutput>(out _) || item.TryGetComponent<BooleanCutResult>(out _))
                return true;

            var parent = item.transform.parent;
            if (!parent || !parent.TryGetComponent<WallOutput>(out var output) ||
                !item.TryGetComponent<MeshRenderer>(out var renderer)) return false;
            if (output.authored.Contains(renderer) || output.surfaces.Contains(renderer) ||
                output.panes.Contains(renderer) || output.trims.Contains(renderer)) return true;

            // Older builders did not record trims. Match their profile and dimensions
            // conservatively so an unrelated decoration beneath Wall geometry stays editable.
            if (output.recordsTrims || PrefabUtility.IsAnyPrefabInstanceRoot(item)) return false;
            var wall = parent.parent ? parent.parent.GetComponent<HouseWall>() : null;
            var trim = wall ? wall.Trim : null;
            if (!trim || trim.name != item.name || !item.TryGetComponent<MeshFilter>(out var filter)) return false;
            var profile = trim.profile ? trim.profile : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return filter.sharedMesh == profile &&
                Mathf.Approximately(profile.bounds.size.y * item.transform.localScale.y, trim.height) &&
                Mathf.Approximately(profile.bounds.size.z * item.transform.localScale.z, trim.depth);
        }

        // An opening only owns the model supplied by its insert. Use the source
        // prefab to distinguish separately added scene objects, including nested ones.
        internal static GameObject InsertModel(GameObject hit, InsertAttachment insert)
        {
            if (!insert) return null;
            if (hit == insert.gameObject) return hit;
            if (hit.TryGetComponent<BooleanCutResult>(out _)) hit = hit.transform.parent.gameObject;
            if (!insert.sourcePrefab) return null;
            var model = hit.transform;
            while (model && model.parent != insert.transform) model = model.parent;
            if (!model) return null;
            var path = AssetDatabase.GetAssetPath(insert.sourcePrefab);
            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(model.gameObject, path) != insert.sourcePrefab)
                return null;
            return PrefabUtility.GetCorrespondingObjectFromSourceAtPath(hit, path) ? model.gameObject : null;
        }

        internal static bool IsFixturePart(GameObject hit, FixtureAttachment fixture)
        {
            if (!fixture) return false;
            if (hit == fixture.gameObject) return true;
            var model = hit.transform;
            while (model && model.parent != fixture.transform) model = model.parent;
            if (!model) return false;
            if (model == hit.transform && model.name == "Light" && model.TryGetComponent<Light>(out _)) return true;
            // The fixture builder creates its model as its first child.
            if (model.GetSiblingIndex() != 0) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject);
            return !string.IsNullOrEmpty(path) && PrefabUtility.GetCorrespondingObjectFromSourceAtPath(hit, path);
        }

        internal static bool IsAttachmentPart(GameObject hit)
        {
            return InsertModel(hit, hit.GetComponentInParent<InsertAttachment>(true)) ||
                IsFixturePart(hit, hit.GetComponentInParent<FixtureAttachment>(true));
        }

        internal static GameObject PropRoot(GameObject hit)
        {
            var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(hit);
            if (!prefab || prefab == hit) return hit;
            // A building can itself be a prefab. Never climb past architecture to
            // reach that prefab when the clicked object is ordinary scenery.
            for (var t = hit.transform.parent; t; t = t.parent)
            {
                if (IsArchitecture(t.gameObject) || t.TryGetComponent<RoomContents>(out _)) return hit;
                if (t.gameObject == prefab)
                {
                    var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);
                    return PrefabUtility.GetCorrespondingObjectFromSourceAtPath(hit, path) ? prefab : hit;
                }
            }
            return hit;
        }

        internal static void DestroyGeometry(Component generated, Transform survivingParent, string action)
        {
            PreserveChildren(generated.transform, survivingParent, action);
            Undo.DestroyObjectImmediate(generated.gameObject);
        }

        static void PreserveChildren(Transform parent, Transform survivingParent, string action)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (IsArchitecture(child.gameObject) || IsAttachmentPart(child.gameObject))
                    PreserveChildren(child, survivingParent, action);
                else
                    Undo.SetTransformParent(child, survivingParent, action);
            }
        }
    }
}
