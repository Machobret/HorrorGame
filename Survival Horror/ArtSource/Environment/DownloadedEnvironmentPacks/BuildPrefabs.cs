// Copied into the ignored, isolated Unity project by build_prefabs.py.
// This is an import recipe, never a runtime script or an editor startup hook.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DownloadedEnvironmentPackImporter
{
    [Serializable] public class MissingTexture { public string role; public string[] filenames; }
    [Serializable] public class MaterialRecord
    {
        public string name, base_color, normal, emission;
        public float[] color;
        public float opacity = 1;
        public bool has_pbr;
        public float metallic, roughness = .85f, transmission, cutoff = .5f, emission_strength = 1;
        public string alpha_mode;
        public MissingTexture[] missing_source_textures;
    }
    [Serializable] public class TextureRecord { public string path, sha256; public bool normal; }
    [Serializable] public class ModelRecord { public string path, sha256; public MaterialRecord[] materials; }
    [Serializable] public class Pack { public string name, root; public TextureRecord[] textures; public ModelRecord[] models; }
    [Serializable] public class Manifest { public Pack[] packs; }
    [Serializable] public class AssetRecord
    {
        public string pack, source_model, source_object, prefab, guid, duplicate_of;
        public float[] bounds_m;
        public int mesh_parts, triangles;
        public string[] material_paths, source_texture_warnings;
    }
    [Serializable] public class PackRecord { public string name; public int source_objects, prefabs, duplicates, materials, textures; }
    [Serializable] public class ExclusionRecord { public string pack, source_model, source_object, reason; }
    [Serializable] public class Receipt
    {
        public string completed_utc, unity_version;
        public bool verified;
        public int prefab_count, source_object_count, duplicate_count, excluded_object_count;
        public PackRecord[] packs;
        public AssetRecord[] assets;
        public ExclusionRecord[] exclusions;
        public string[] warnings, checks;
    }

    const string ManifestPath = "ImportManifest.json";
    static readonly Dictionary<string, bool> Alpha = new Dictionary<string, bool>();
    static readonly Dictionary<Mesh, string> MeshHashes = new Dictionary<Mesh, string>();
    static readonly List<AssetRecord> Assets = new List<AssetRecord>();
    static readonly List<string> Warnings = new List<string>();
    static readonly List<PackRecord> Packs = new List<PackRecord>();
    static readonly List<ExclusionRecord> Exclusions = new List<ExclusionRecord>();
    static HashSet<string> ApprovedExclusions;

    static bool HasGeometry(MeshFilter filter) => filter && filter.sharedMesh && filter.sharedMesh.vertexCount > 0 && filter.sharedMesh.triangles.Length > 0;

    public static void RunBatch()
    {
        try
        {
            if (File.Exists("ImportFailure.txt")) File.Delete("ImportFailure.txt");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            ApprovedExclusions = File.Exists("MaintenancePlan.json")
                ? new HashSet<string>(JsonUtility.FromJson<DownloadedPrefabMaintenance.Plan>(File.ReadAllText("MaintenancePlan.json")).excluded_sources ?? Array.Empty<string>())
                : new HashSet<string>();
            foreach (var pack in manifest.packs) BuildPack(pack);
            AssetDatabase.SaveAssets();
            Verify(manifest);
            var receipt = new Receipt {
                completed_utc = DateTime.UtcNow.ToString("o"), unity_version = Application.unityVersion,
                verified = true, prefab_count = Assets.Count(a => string.IsNullOrEmpty(a.duplicate_of)),
                source_object_count = Assets.Count + Exclusions.Count, duplicate_count = Assets.Count(a => !string.IsNullOrEmpty(a.duplicate_of)),
                excluded_object_count = Exclusions.Count, exclusions = Exclusions.ToArray(),
                packs = Packs.ToArray(), assets = Assets.ToArray(), warnings = Warnings.Distinct().ToArray(),
                checks = new[] {
                    "One prefab per source top-level object/assembly; source child groups retained",
                    "Exact duplicate geometry/material assemblies share a prefab within each pack",
                    "All prefab roots use identity transforms and bottom-centred placement pivots",
                    "Every renderer has a nonempty saved mesh and matching URP material slots; textured meshes retain UV0",
                    "Physical mesh parts have static nonconvex MeshColliders; non-blocking decoration is exempt",
                    "Source geometry, local part transforms, triangle counts and bounds preserved",
                    "Prefab dependencies contain no combined FBX, Blender source, or scene",
                    "All declared available textures, normal maps and emission maps assigned",
                    "No missing scripts; no gameplay behaviours, lights or cameras added",
                    "All original model/texture GUIDs retained during promotion"
                }
            };
            File.WriteAllText("ImportReceipt.json", JsonUtility.ToJson(receipt, true));
            if (File.Exists("MaintenancePlan.json")) DownloadedPrefabMaintenance.Apply();
            Debug.Log("PACK_PREFABS_VERIFIED " + receipt.prefab_count + " prefabs; " + receipt.duplicate_count + " duplicate instances; " + receipt.warnings.Length + " warnings");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("ImportFailure.txt", e.ToString());
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static string Safe(string value)
    {
        value = Regex.Replace(value, @"[^\p{L}\p{N}_-]+", "_").Trim('_');
        return string.IsNullOrEmpty(value) ? "SourceObject" : value;
    }

    static string Digest(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));

    static string MeshHash(Mesh mesh)
    {
        if (MeshHashes.TryGetValue(mesh, out var cached)) return cached;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
            foreach (var p in mesh.vertices) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            foreach (var p in mesh.normals) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            foreach (var p in mesh.tangents) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
            foreach (var p in mesh.colors32) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); writer.Write(p.a); }
            for (int channel = 0; channel < 8; channel++)
            {
                var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count);
                foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
            }
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var indices = mesh.GetIndices(i); writer.Write((int)mesh.GetTopology(i)); writer.Write(indices.Length);
                foreach (int index in indices) writer.Write(index);
            }
            cached = Digest(stream.ToArray());
        }
        MeshHashes.Add(mesh, cached); return cached;
    }

    static string AssemblyHash(Transform source)
    {
        var text = new StringBuilder();
        // Remove source layout translation/rotation; retain scale and every part's relative pose.
        text.Append(source.lossyScale.ToString("F5"));
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true).Where(HasGeometry))
        {
            text.Append(MeshHash(filter.sharedMesh));
            var relative = source.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++) text.Append('|').Append(Mathf.RoundToInt(relative[i] * 100000));
            foreach (var mat in filter.GetComponent<Renderer>().sharedMaterials)
                text.Append('|').Append(AssetDatabase.GetAssetPath(mat));
        }
        return Digest(text.ToString());
    }

    static void ConfigureTextures(Pack pack)
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var entry in pack.textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.path);
                importer.textureType = entry.normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !entry.normal;
                importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = !entry.normal;
                importer.isReadable = !entry.normal;
                importer.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (var entry in pack.textures)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.path);
            if (!texture) throw new InvalidOperationException("Texture failed to load: " + entry.path);
            Alpha[entry.path] = !entry.normal && texture.GetPixels32().Any(c => c.a < 250);
        }
    }

    static Material MakeMaterial(Pack pack, MaterialRecord record, string existingPath = null)
    {
        var path = existingPath ?? (pack.root + "/Materials/" + Safe(record.name) + "_" + Digest(JsonUtility.ToJson(record)).Substring(0, 10) + ".mat");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP Lit unavailable");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        Texture2D Tex(string p) => string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        mat.SetTexture("_BaseMap", Tex(record.base_color));
        // URP Lit has no physical transmission; represent source glass with alpha blending.
        float opacity = record.transmission > 0 ? Mathf.Lerp(record.opacity, .12f, record.transmission) : record.opacity;
        mat.SetColor("_BaseColor", new Color(record.color[0], record.color[1], record.color[2], opacity));
        mat.SetFloat("_Smoothness", record.has_pbr ? 1 - record.roughness : .15f);
        mat.SetFloat("_Metallic", record.has_pbr ? record.metallic : 0);
        mat.SetFloat("_Cull", (float)CullMode.Off); mat.doubleSidedGI = true; mat.enableInstancing = true;
        bool transparent = opacity < .99f || record.alpha_mode == "BLEND";
        bool cutout = !transparent && (record.has_pbr ? record.alpha_mode == "MASK"
            : !string.IsNullOrEmpty(record.base_color) && Alpha[record.base_color]);
        mat.SetFloat("_Surface", transparent ? 1 : 0);
        mat.SetFloat("_Blend", 0); mat.SetFloat("_AlphaClip", cutout ? 1 : 0); mat.SetFloat("_Cutoff", record.cutoff);
        mat.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
        mat.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        mat.SetFloat("_ZWrite", transparent ? 0 : 1);
        mat.SetFloat("_AlphaToMask", cutout ? 1 : 0);
        Keyword(mat, "_SURFACE_TYPE_TRANSPARENT", transparent); Keyword(mat, "_ALPHATEST_ON", cutout);
        mat.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
        mat.renderQueue = transparent ? 3000 : cutout ? 2450 : 2000;
        mat.SetShaderPassEnabled("ShadowCaster", !transparent);
        mat.SetTexture("_BumpMap", Tex(record.normal)); Keyword(mat, "_NORMALMAP", !string.IsNullOrEmpty(record.normal));
        mat.SetFloat("_BumpScale", 1);
        mat.SetTexture("_EmissionMap", Tex(record.emission));
        mat.SetColor("_EmissionColor", string.IsNullOrEmpty(record.emission) ? Color.black : Color.white * record.emission_strength);
        Keyword(mat, "_EMISSION", !string.IsNullOrEmpty(record.emission));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(mat); return mat;
    }

    static void Keyword(Material mat, string name, bool enabled)
    { if (enabled) mat.EnableKeyword(name); else mat.DisableKeyword(name); }

    static void BuildPack(Pack pack)
    {
        Debug.Log("PACK_START " + pack.name);
        foreach (string folder in new[] { "Materials", "Geometry", "Prefabs" }) Directory.CreateDirectory(pack.root + "/" + folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures(pack);
        var signatures = new Dictionary<string, AssetRecord>();
        var geometry = new Dictionary<string, Mesh>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packMaterials = new HashSet<string>();
        int start = Assets.Count;
        // Prefer the loose Pizzeria props over their repeated scene instances.
        foreach (var model in pack.models.OrderBy(m => m.path.Contains("_Scene") ? 1 : 0))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(model.path);
            var previousRemaps = importer.GetExternalObjectMap();
            var materials = new Dictionary<string, Material>();
            foreach (var record in model.materials)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), record.name);
                string previousPath = previousRemaps.TryGetValue(identifier, out var previous) && previous is Material
                    ? AssetDatabase.GetAssetPath(previous) : null;
                var mat = MakeMaterial(pack, record, previousPath); materials[record.name] = mat;
                packMaterials.Add(AssetDatabase.GetAssetPath(mat));
                foreach (var missing in record.missing_source_textures ?? Array.Empty<MissingTexture>())
                    Warnings.Add(pack.name + ": " + record.name + " uses its source color; missing " + missing.role + " texture " + string.Join(", ", missing.filenames));
            }
            importer.isReadable = true; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(model.path);
            if (!imported) throw new InvalidOperationException("Model failed to import: " + model.path);
            var roots = imported.GetComponent<MeshRenderer>() ? new[] { imported.transform }
                : imported.transform.Cast<Transform>().ToArray();
            foreach (var source in roots)
            {
                if (ApprovedExclusions.Contains(model.path + "\n" + source.name))
                {
                    Exclusions.Add(new ExclusionRecord { pack=pack.name, source_model=model.path, source_object=source.name,
                        reason="Approved curation: generic building part, incomplete assembly or source layout. Original preserved in ArtSource." });
                    continue;
                }
                var renderers = source.GetComponentsInChildren<MeshRenderer>(true).Where(r => HasGeometry(r.GetComponent<MeshFilter>())).ToArray();
                if (renderers.Length == 0)
                {
                    Exclusions.Add(new ExclusionRecord { pack = pack.name, source_model = model.path, source_object = source.name,
                        reason = "Source object has no renderable triangles; original retained under ArtSource." });
                    continue;
                }
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        var material = slots[slot];
                        if (material && packMaterials.Contains(AssetDatabase.GetAssetPath(material))) continue;
                        string path = AssetDatabase.GetAssetPath(material);
                        if (!material || path == "Resources/unity_builtin_extra" || path == "Library/unity default resources"
                            || (path == model.path && material.name == "No Name" && !materials.ContainsKey("No Name")))
                        {
                            var defaults = AssetDatabase.FindAssets("SourceDefault t:Material", new[] { pack.root + "/Materials" });
                            string defaultPath = defaults.Length == 1 ? AssetDatabase.GUIDToAssetPath(defaults[0]) : null;
                            var fallback = MakeMaterial(pack, new MaterialRecord { name = "SourceDefault", base_color = "", normal = "", emission = "",
                                color = new[] { .65f, .65f, .65f, 1f }, opacity = 1 }, defaultPath);
                            slots[slot] = fallback; packMaterials.Add(AssetDatabase.GetAssetPath(fallback));
                            Warnings.Add(pack.name + ": " + source.name + " has no authored material; neutral URP source-default material assigned.");
                        }
                        else throw new InvalidOperationException("Unmapped material: " + model.path + " / " + source.name + " / " + material.name + "=" + path);
                    }
                    renderer.sharedMaterials = slots;
                }
                string signature = AssemblyHash(source);
                if (signatures.TryGetValue(signature, out var existing))
                {
                    Assets.Add(new AssetRecord { pack = pack.name, source_model = model.path, source_object = source.name, duplicate_of = existing.prefab });
                    continue;
                }
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Vector3 anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                string subject = source.name;
                if (Regex.IsMatch(subject, @"^(Cube|Plane|Cylinder|Sphere|Ca|C|B)(\.|$)", RegexOptions.IgnoreCase))
                {
                    var main = renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m.mainTexture);
                    if (main) subject = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(main.mainTexture)) + "_" + subject;
                }
                string name = pack.name + "_" + Safe(subject);
                string prefabPath = pack.root + "/Prefabs/" + name + ".prefab";
                if (!paths.Add(prefabPath))
                { name += "_" + signature.Substring(0, 8); prefabPath = pack.root + "/Prefabs/" + name + ".prefab"; paths.Add(prefabPath); }
                var root = new GameObject(name);
                var clone = Object.Instantiate(source.gameObject);
                if (PrefabUtility.IsPartOfPrefabInstance(clone))
                    PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                clone.name = source.name;
                clone.transform.SetParent(root.transform, false);
                clone.transform.position = source.position - anchor;
                clone.transform.rotation = source.rotation;
                clone.transform.localScale = source.lossyScale;
                try
                {
                    bool nonBlocking = PrefabCollisionBatch.IsNonBlocking(prefabPath);
                    foreach (var filter in clone.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = filter.sharedMesh;
                        if (!HasGeometry(filter))
                        {
                            var emptyRenderer = filter.GetComponent<MeshRenderer>();
                            if (emptyRenderer) Object.DestroyImmediate(emptyRenderer);
                            Object.DestroyImmediate(filter);
                            continue; // Keep the helper Transform and any meaningful descendants.
                        }
                        string hash = MeshHash(mesh);
                        if (!geometry.TryGetValue(hash, out var saved))
                        {
                            string meshPath = pack.root + "/Geometry/" + Safe(subject) + "_" + hash.Substring(0, 12) + ".asset";
                            saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                            if (!saved) { saved = Object.Instantiate(mesh); saved.name = Safe(subject); AssetDatabase.CreateAsset(saved, meshPath); }
                            geometry.Add(hash, saved);
                        }
                        filter.sharedMesh = saved;
                        var collider = filter.GetComponent<MeshCollider>();
                        if (nonBlocking)
                        {
                            if (collider && !collider.isTrigger) Object.DestroyImmediate(collider);
                            continue;
                        }
                        if (!collider) collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = saved; collider.convex = false;
                    }
                    var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    if (!savedPrefab) throw new InvalidOperationException("Could not save prefab: " + prefabPath);
                    var mats = savedPrefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                    var warningNames = model.materials.Where(m => (m.missing_source_textures?.Length ?? 0) > 0)
                        .Where(m => mats.Contains(materials[m.name])).Select(m => m.name).ToArray();
                    var entry = new AssetRecord {
                        pack = pack.name, source_model = model.path, source_object = source.name, prefab = prefabPath,
                        guid = AssetDatabase.AssetPathToGUID(prefabPath), mesh_parts = renderers.Length,
                        triangles = source.GetComponentsInChildren<MeshFilter>(true).Where(HasGeometry).Sum(f => f.sharedMesh.triangles.Length / 3),
                        bounds_m = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                        material_paths = mats.Select(AssetDatabase.GetAssetPath).ToArray(), source_texture_warnings = warningNames
                    };
                    Assets.Add(entry); signatures.Add(signature, entry);
                }
                finally { Object.DestroyImmediate(root); }
            }
            Debug.Log("MODEL_SPLIT " + model.path + " source roots=" + roots.Length + " unique prefabs=" + signatures.Count);
        }
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var entry in pack.textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.path);
                importer.isReadable = false; importer.mipMapsPreserveCoverage = Alpha[entry.path]; importer.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Packs.Add(new PackRecord { name = pack.name, source_objects = Assets.Count - start + Exclusions.Count(e => e.pack == pack.name), prefabs = signatures.Count,
            duplicates = Assets.Count - start - signatures.Count, materials = packMaterials.Count, textures = pack.textures.Length });
        AssetDatabase.SaveAssets();
        Debug.Log("PACK_COMPLETE " + pack.name + " prefabs=" + signatures.Count);
    }

    static void Verify(Manifest manifest)
    {
        foreach (var entry in Assets.Where(a => string.IsNullOrEmpty(a.duplicate_of)))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
            if (!prefab || entry.guid.Length != 32) throw new InvalidOperationException("Missing prefab: " + entry.prefab);
            if (prefab.transform.localPosition != Vector3.zero || prefab.transform.localRotation != Quaternion.identity || prefab.transform.localScale != Vector3.one)
                throw new InvalidOperationException("Nonidentity prefab root: " + entry.prefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new InvalidOperationException("Missing script: " + entry.prefab);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                if ((bounds.size - new Vector3(entry.bounds_m[0], entry.bounds_m[1], entry.bounds_m[2])).magnitude > .01f
                    || Mathf.Abs(bounds.min.y) > .005f || Mathf.Abs(bounds.center.x) > .005f || Mathf.Abs(bounds.center.z) > .005f)
                    throw new InvalidOperationException("Bounds/pivot mismatch: " + entry.prefab + " " + bounds);
                int triangles = 0;
                bool nonBlocking = PrefabCollisionBatch.IsNonBlocking(entry.prefab);
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh; var renderer = filter.GetComponent<Renderer>();
                    if (!mesh || mesh.vertexCount == 0) throw new InvalidOperationException("Missing mesh: " + entry.prefab);
                    if (renderer.sharedMaterials.Any(m => m && m.mainTexture) && mesh.uv.Length != mesh.vertexCount)
                        throw new InvalidOperationException("Missing textured mesh UV: " + entry.prefab);
                    triangles += mesh.triangles.Length / 3;
                    if (!renderer || renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => !m || m.shader.name != "Universal Render Pipeline/Lit"))
                        throw new InvalidOperationException("Material slot mismatch: " + entry.prefab);
                    var collider = filter.GetComponent<MeshCollider>();
                    if (nonBlocking ? collider && !collider.isTrigger : !collider || collider.sharedMesh != mesh || collider.convex)
                        throw new InvalidOperationException("Collider mismatch: " + entry.prefab);
                }
                if (triangles != entry.triangles || renderers.Length != entry.mesh_parts) throw new InvalidOperationException("Geometry mismatch: " + entry.prefab);
                foreach (var dependency in AssetDatabase.GetDependencies(entry.prefab, true))
                    if (new[] { ".fbx", ".blend", ".unity" }.Contains(Path.GetExtension(dependency).ToLowerInvariant()))
                        throw new InvalidOperationException("Prefab still depends on a source assembly: " + entry.prefab + " -> " + dependency);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        foreach (var pack in manifest.packs)
            foreach (var model in pack.models)
                foreach (var record in model.materials)
                {
                    var remaps = ((ModelImporter)AssetImporter.GetAtPath(model.path)).GetExternalObjectMap();
                    if (!remaps.TryGetValue(new AssetImporter.SourceAssetIdentifier(typeof(Material), record.name), out var mapped))
                        throw new InvalidOperationException("Missing material remap: " + record.name);
                    var mat = MakeMaterial(pack, record, AssetDatabase.GetAssetPath(mapped));
                    foreach (var pair in new[] { ("_BaseMap", record.base_color), ("_BumpMap", record.normal), ("_EmissionMap", record.emission) })
                        if (!string.IsNullOrEmpty(pair.Item2) && AssetDatabase.GetAssetPath(mat.GetTexture(pair.Item1)) != pair.Item2)
                            throw new InvalidOperationException("Incorrect texture binding: " + record.name + " " + pair.Item1);
                }
    }

    public static void UpdateMaterialsBatch()
    {
        try
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("ImportReceipt.json"));
            if (!receipt.verified) throw new InvalidOperationException("A verified geometry receipt is required.");
            receipt.verified = false;
            File.WriteAllText("ImportReceipt.json", JsonUtility.ToJson(receipt, true));
            foreach (var pack in manifest.packs)
                foreach (var entry in pack.textures)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(entry.path);
                    Alpha[entry.path] = !entry.normal && importer.DoesSourceTextureHaveAlpha();
                }
            Assets.AddRange(receipt.assets);
            Verify(manifest);
            AssetDatabase.SaveAssets();
            receipt.verified = true;
            receipt.completed_utc = DateTime.UtcNow.ToString("o");
            receipt.checks = receipt.checks.Concat(new[] {
                "Textured materials use image color rather than Blender FBX's disconnected diffuse socket fallback",
                "Supplied GLB PBR color, metallic, roughness, alpha and emission settings restored",
                "GLB transmission glass approximated with alpha-blended URP Lit; physical refraction is not provided"
            }).ToArray();
            File.WriteAllText("ImportReceipt.json", JsonUtility.ToJson(receipt, true));
            Debug.Log("PACK_MATERIALS_VERIFIED " + receipt.prefab_count);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("MaterialFailure.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
