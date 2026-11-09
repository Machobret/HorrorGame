using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EnvironmentAuthoring.Editor
{
    public static class WallFamilyImporter
    {
        private sealed class Source
        {
            public string Path;
            public WallAssetName Name;
            public Mesh Mesh;
            public Material[] Materials;
            public Rect Opening;
        }

        [MenuItem("Assets/Environment/Import Wall Family")]
        private static void ImportSelected()
        {
            string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(folder)) { Debug.LogError("Select a folder containing complete wall modules or shared models."); return; }
            try { Selection.objects = ImportAll(folder); }
            catch (Exception error) { Debug.LogError(error.Message); }
        }

        public static WallFamily Import(string folder)
        {
            var sources = ReadSources(folder);
            if (sources.Where(s => !s.Name.Insert).Select(s => s.Name.Family).Distinct().Count() != 1)
                throw new InvalidOperationException("Import requires exactly one wall family. Use ImportAll for multiple families or model-only folders.");
            return Publish(folder, sources).OfType<WallFamily>().Single();
        }

        public static Object[] ImportAll(string folder) => Publish(folder, ReadSources(folder));

        private static List<Source> ReadSources(string folder)
        {
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException("Choose a family folder under Assets.");
            var sources = new List<Source>();
            foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                if (!WallAssetName.TryParse(path, out var metadata, out string reason)) throw new InvalidOperationException(path + ": " + reason);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 1 || filters[0].sharedMesh == null) throw new InvalidOperationException(path + ": export exactly one Body mesh.");
                var body = filters[0];
                // Baking transforms here would manufacture a fitted variant. Require metre-space sources instead.
                if (!MatrixIsIdentity(root.transform.worldToLocalMatrix * body.transform.localToWorldMatrix)
                    || root.transform.localScale != Vector3.one || root.transform.localRotation != Quaternion.identity || root.transform.localPosition != Vector3.zero)
                    throw new InvalidOperationException(path + ": apply transforms before export; Body must use family-local metre coordinates.");
                Rect opening = default;
                if (!metadata.Solid)
                {
                    var min = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "OpeningMin");
                    var max = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "OpeningMax");
                    if (min == null || max == null) throw new InvalidOperationException(path + ": add OpeningMin and OpeningMax markers in the common X/Y aperture.");
                    Vector3 a = root.transform.InverseTransformPoint(min.position), b = root.transform.InverseTransformPoint(max.position);
                    opening = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                    if (opening.width <= 0 || opening.height <= 0 || opening.xMin < 0 || opening.yMin < 0
                        || opening.xMax > metadata.Width || opening.yMax > metadata.Height || Mathf.Abs(a.z) > .0001f || Mathf.Abs(b.z) > .0001f)
                        throw new InvalidOperationException(path + ": aperture markers must lie inside the segment on Z=0.");
                }
                sources.Add(new Source { Path = path, Name = metadata, Mesh = body.sharedMesh, Opening = opening,
                    Materials = body.GetComponent<MeshRenderer>()?.sharedMaterials ?? Array.Empty<Material>() });
            }
            if (sources.Count == 0) throw new InvalidOperationException("No complete wall modules or shared models were found.");
            return sources;
        }

        private static bool MatrixIsIdentity(Matrix4x4 matrix)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(matrix[i] - Matrix4x4.identity[i]) > .00001f) return false;
            return true;
        }

        private static Object[] Publish(string folder, List<Source> sources)
        {
            var staged = new List<Object>();
            var paths = new Dictionary<Object, string>();
            T Stage<T>(string path) where T : ScriptableObject
            {
                var existing = AssetDatabase.LoadAssetAtPath<T>(path);
                var item = existing == null ? ScriptableObject.CreateInstance<T>() : Object.Instantiate(existing);
                item.name = Path.GetFileNameWithoutExtension(path); staged.Add(item); paths.Add(item, path); return item;
            }
            try
            {
                foreach (var group in sources.Where(s => !s.Name.Insert).GroupBy(s => s.Name.Family))
                {
                    string id = group.Key;
                    var family = Stage<WallFamily>(folder + "/Wall_" + id + ".asset");
                    family.Id = id;
                    var previousBlanks = family.Blanks().Where(d => d != null).ToArray();
                    var definitions = new List<(Source source, WallModuleDefinition definition)>();
                    foreach (var source in group)
                    {
                        var definition = Stage<WallModuleDefinition>(Path.ChangeExtension(source.Path, ".asset"));
                        var n = source.Name;
                        if (!definition.PreserveImportOverrides)
                        {
                            definition.FamilyId = id;
                            definition.Width = n.Width; definition.Height = n.Height; definition.Thickness = n.Thickness;
                            definition.AllowLengthStretch = n.Solid; definition.OpeningShape = n.Shape; definition.OpeningOutline = source.Opening;
                            definition.DisplayName = definition.name;
                        }
                        definition.Body = source.Mesh;
                        var errors = WallKitValidation.Check(definition).Where(i => i.Severity == MessageType.Error).Select(i => i.Message).ToArray();
                        if (errors.Length > 0) throw new InvalidOperationException(source.Path + ": " + string.Join(" ", errors));
                        definitions.Add((source, definition));
                    }
                    string PathOf(Object asset) => paths.TryGetValue(asset, out string path) ? path : AssetDatabase.GetAssetPath(asset);
                    var blanks = definitions.Where(d => d.source.Name.Solid).Select(d => d.definition).Concat(previousBlanks)
                        .GroupBy(d => PathOf(d)).Select(g => g.First()).OrderByDescending(d => d.Width).ToArray();
                    if (blanks.Length == 0) throw new InvalidOperationException(id + ": a family needs a solid piece.");
                    string previousPrimary = family.Blank != null ? PathOf(family.Blank) : "";
                    family.Blank = blanks.FirstOrDefault(d => PathOf(d) == previousPrimary) ?? blanks[0];
                    family.BlankVariants = blanks.Where(d => d != family.Blank).ToArray();
                    family.Openings = definitions.Where(d => !d.source.Name.Solid).Select(d => new WallFamily.Opening {
                        Kind = d.source.Name.Kind, Definition = d.definition }).Concat(family.Openings ?? Array.Empty<WallFamily.Opening>())
                        .Where(o => o?.Definition != null).GroupBy(o => o.Kind + ":" + PathOf(o.Definition)).Select(g => g.First()).ToArray();
                    if (!family.IsValid(out string reason)) throw new InvalidOperationException(reason);
                }
                foreach (var source in sources.Where(s => s.Name.Insert))
                {
                    var insert = Stage<WallOpeningInsert>(Path.ChangeExtension(source.Path, ".asset"));
                    insert.Shape = source.Name.Shape; insert.Kind = source.Name.Kind;
                    insert.SegmentWidth = source.Name.Width; insert.Opening = source.Opening; insert.Mesh = source.Mesh;
                    if (insert.Material == null) insert.Material = source.Materials.ElementAtOrDefault(0);
                    if (insert.GlassMaterial == null && insert.Mesh.subMeshCount == 2) insert.GlassMaterial = source.Materials.ElementAtOrDefault(1);
                    if (!insert.HasValidMaterials) throw new InvalidOperationException(source.Path + ": models need one or two submeshes and a main material; assign the second material for glass.");
                }
                // Validate the whole batch before touching persistent assets. Updating in place keeps references/GUIDs.
                var published = new Dictionary<Object, Object>();
                foreach (var item in staged)
                {
                    var destination = AssetDatabase.LoadAssetAtPath<Object>(paths[item]);
                    if (destination != null && destination.GetType() != item.GetType()) throw new InvalidOperationException("An incompatible asset already occupies " + paths[item]);
                }
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Import wall family");
                foreach (var item in staged)
                {
                    var destination = AssetDatabase.LoadAssetAtPath<Object>(paths[item]);
                    if (destination == null)
                    {
                        // Import is additive: new files remain on Undo; edits to existing assets are reversible.
                        // Allocate persistent identities first. Never serialize references to temporary staging objects.
                        destination = ScriptableObject.CreateInstance(item.GetType()); AssetDatabase.CreateAsset(destination, paths[item]);
                    }
                    else Undo.RecordObject(destination, "Import wall family");
                    published.Add(item, destination);
                }
                foreach (var item in staged)
                {
                    var serialized = new SerializedObject(item); var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null
                            && published.TryGetValue(property.objectReferenceValue, out var mapped)) property.objectReferenceValue = mapped;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var destination = published[item];
                    EditorUtility.CopySerialized(item, destination); EditorUtility.SetDirty(destination);
                }
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undo); AssetDatabase.SaveAssets();
                return published.Values.ToArray();
            }
            finally { foreach (var item in staged) Object.DestroyImmediate(item); }
        }
    }
}
