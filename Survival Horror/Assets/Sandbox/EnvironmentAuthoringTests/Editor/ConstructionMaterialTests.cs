using EnvironmentAuthoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ConstructionMaterialTests
{
    [Test]
    public void ConversionPreservesAssetIdentityAndSupportedSurfaceInputs()
    {
        string path = "Assets/Sandbox/WallFinish_" + System.Guid.NewGuid().ToString("N") + ".mat";
        var source = OpaqueWallMaterial();
        var texture = new Texture2D(2, 2);
        var normal = new Texture2D(2, 2);
        try
        {
            AssetDatabase.CreateAsset(source, path);
            AssetDatabase.AddObjectToAsset(texture, source);
            AssetDatabase.AddObjectToAsset(normal, source);
            source.SetTexture("_BaseMap", texture);
            source.SetTexture("_BumpMap", normal);
            source.EnableKeyword("_NORMALMAP");
            source.SetFloat("_BumpScale", .6f);
            source.SetFloat("_Smoothness", .25f);
            source.SetFloat("_Cull", 0);
            source.SetColor("_BaseColor", Color.gray);
            source.SetTextureScale("_BaseMap", new Vector2(2, 3));
            source.SetTextureOffset("_BaseMap", new Vector2(.2f, .3f));
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(ConstructionMaterialCatalog.CanConvert(source), Is.True, "Opaque nonmetal wall fixture must be convertible.");
            ConstructionMaterialCatalog.Convert(source);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).GetInstanceID(), Is.EqualTo(source.GetInstanceID()));
            Assert.That(source.shader.name, Is.EqualTo(ConstructionMaterialCatalog.ShaderName));
            Assert.That(source.GetTexture("_BaseMap"), Is.EqualTo(texture));
            Assert.That(source.GetTexture("_BumpMap"), Is.EqualTo(normal));
            Assert.That(source.GetColor("_BaseColor"), Is.EqualTo(Color.gray));
            Assert.That(source.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2, 3)));
            Assert.That(source.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(.2f, .3f)));
            Assert.That(source.GetFloat("_RoughnessScale"), Is.EqualTo(.75f));
            Assert.That(source.GetFloat("_NormalStrength"), Is.EqualTo(.6f));
            Assert.That(source.GetFloat("_UseNormalMap"), Is.EqualTo(1));
            Assert.That(source.GetFloat("_Cull"), Is.EqualTo(0));
            Assert.That(source.GetFloat("_Mapping"), Is.EqualTo(1));
        }
        finally
        {
            AssetDatabase.DeleteAsset(path);
            if (texture != null) Object.DestroyImmediate(texture, true);
            if (normal != null) Object.DestroyImmediate(normal, true);
        }
    }

    [TestCase("_Metallic", 1)]
    [TestCase("_AlphaClip", 1)]
    [TestCase("_Surface", 1)]
    public void UnsupportedSurfaceIsExcludedFromConversion(string property, float value)
    {
        var material = OpaqueWallMaterial();
        var texture = new Texture2D(2, 2);
        try
        {
            material.SetTexture("_BaseMap", texture);
            Assert.That(ConstructionMaterialCatalog.CanConvert(material), Is.True);
            material.SetFloat(property, value);
            Assert.That(ConstructionMaterialCatalog.CanConvert(material), Is.False);
        }
        finally { Object.DestroyImmediate(material); Object.DestroyImmediate(texture); }
    }

    private static Material OpaqueWallMaterial()
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Set the same explicit empty maps as imported URP wall materials.
        foreach (string property in new[] { "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap" })
            material.SetTexture(property, null);
        return material;
    }
}
