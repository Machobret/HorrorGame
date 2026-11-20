using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ImportNeonSigns
{
    const string Root = "Assets/Game/Environment/NeonSigns";
    const float MetresPerPixel = 0.02f;
    static readonly string[] Designs =
    {
        "BusinessHours", "PizzaSlice", "SodaCup", "Fries", "BirdLogo", "SolenWordmark", "CocktailGlass",
        "PixelGlyphs", "LoopedScript", "AngularMonogram", "CursiveGlyphs", "UnderlinedWordmark",
        "ItalicWordmark", "RoundScriptOutline", "RoundScriptBadge", "SlantedGlyphs", "VerticalGlyphs",
        "PmxWordmark", "LinearGlyphs", "Burger"
    };

    public static void RunBatch()
    {
        string source = Environment.GetEnvironmentVariable("NEON_SIGN_PACK");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Set NEON_SIGN_PACK to the extracted sign pack.");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/NeonSign.shader");
        if (!shader) throw new InvalidOperationException("The neon sign shader must be present in the Unity project.");

        string[][] variants = Designs.Select((_, index) =>
            Directory.GetDirectories(Path.Combine(source, $"Sign {index + 1}"))).ToArray();
        if (variants.Length != 20 || variants.Any(group => group.Length < 2) || variants.Sum(group => group.Length) != 43)
            throw new InvalidDataException("Expected 20 designs and 43 variants in the extracted sign pack.");

        var meshSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            Mesh quad = meshSource.GetComponent<MeshFilter>().sharedMesh;
            for (int index = 0; index < variants.Length; index++)
                foreach (string variantPath in variants[index].OrderBy(path => path, StringComparer.Ordinal))
                    ImportVariant(Designs[index], variantPath, shader, quad);
        }
        finally
        {
            Object.DestroyImmediate(meshSource);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Imported 43 animated neon sign prefabs.");
    }

    static void ImportVariant(string design, string variantPath, Shader shader, Mesh quad)
    {
        string variant = string.Concat(Path.GetFileName(variantPath).Split(' ')
            .Select(word => word.Substring(0, 1).ToUpperInvariant() + word.Substring(1).ToLowerInvariant()));
        string name = $"Neon{design}_{variant}";
        string folder = $"{Root}/{design}";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, design);
        string stem = $"{folder}/{name}";

        var files = Directory.GetFiles(variantPath, "*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (files.Length < 2 || files.Where((file, i) =>
                !Regex.IsMatch(Path.GetFileName(file), $"_{i:000}\\.png$", RegexOptions.IgnoreCase)).Any())
            throw new InvalidDataException($"Expected consecutive numbered animation frames in {variantPath}.");

        Vector4 layout = WriteAtlas(files, stem + "_Frames.png");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_Frames.png");
        string materialPath = stem + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.mainTexture = texture;
        material.SetVector("_Layout", layout);
        EditorUtility.SetDirty(material);

        var sign = new GameObject(name);
        try
        {
            var face = new GameObject("Sign Face", typeof(MeshFilter), typeof(MeshRenderer));
            face.transform.SetParent(sign.transform, false);
            face.transform.localScale = new Vector3(layout.x * MetresPerPixel, layout.y * MetresPerPixel, 1);
            face.GetComponent<MeshFilter>().sharedMesh = quad;
            var renderer = face.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            string prefabPath = stem + ".prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                PrefabUtility.SaveAsPrefabAsset(sign, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(sign);
        }
    }

    static Vector4 WriteAtlas(string[] files, string path)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var frames = new Color32[files.Length][];
        int width = 0, height = 0, brightest = 0;
        long brightestScore = -1;
        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (!image.LoadImage(File.ReadAllBytes(files[i]))) throw new InvalidDataException(files[i]);
                if (i == 0) (width, height) = (image.width, image.height);
                if (image.width != width || image.height != height) throw new InvalidDataException($"Frame size changed in {files[i]}");

                // Begin at a legible frame for previews, retaining the full cyclic animation order.
                var pixels = frames[i] = image.GetPixels32();
                long score = pixels.Sum(pixel => (long)Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) * pixel.a);
                if (score > brightestScore) (brightest, brightestScore) = (i, score);
            }
        }
        finally
        {
            Object.DestroyImmediate(image);
        }

        int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(files.Length * (float)height / width)), 1, files.Length);
        int rows = (files.Length + columns - 1) / columns;
        var atlas = new Texture2D(Mathf.NextPowerOfTwo(columns * width), Mathf.NextPowerOfTwo(rows * height), TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color32[atlas.width * atlas.height];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                var source = frames[(frame + brightest) % frames.Length];
                int x = frame % columns * width;
                int y = frame / columns * height;
                for (int row = 0; row < height; row++)
                    Array.Copy(source, row * width, pixels, (y + row) * atlas.width + x, width);
            }
            atlas.SetPixels32(pixels);
            atlas.Apply();
            File.WriteAllBytes(path, atlas.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(atlas);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return new Vector4(width, height, columns, frames.Length);
    }
}
