using System.Collections.Generic;
using EnvironmentAuthoring;
using EnvironmentAuthoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PrefabDoorMaterialTests
{
    private GameObject root;
    private HouseDoorAttachment attachment;
    private MeshRenderer visual;
    private readonly List<Material> materials = new List<Material>();

    [SetUp]
    public void SetUp()
    {
        var preset = AssetDatabase.LoadAssetAtPath<WallOpeningInsert>(
            "Assets/Game/Environment/Storefront/Sources/Insert_Door_01_W1000_H2800_ModelStorefront.asset");
        Assert.That(preset, Is.Not.Null);
        Assert.That(preset.Prefab, Is.Not.Null, "Run the storefront import before testing its prefab.");
        root = new GameObject("Door material fixture");
        attachment = root.AddComponent<HouseDoorAttachment>();
        attachment.Preset = preset;
        attachment.SourcePrefab = preset.Prefab;
        attachment.Instance = (GameObject)PrefabUtility.InstantiatePrefab(preset.Prefab);
        attachment.Instance.transform.SetParent(root.transform, false);
        visual = attachment.Instance.transform.Find("Visual").GetComponent<MeshRenderer>();
    }

    private Material NewMaterial()
    {
        var material = Object.Instantiate(visual.sharedMaterials[0]);
        materials.Add(material);
        return material;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        foreach (var material in materials) Object.DestroyImmediate(material);
        materials.Clear();
    }

    [Test]
    public void HostFinishesUpdateBothSlotsWithoutReplacingTheDoor()
    {
        var instance = attachment.Instance;
        var frame = NewMaterial(); var glass = NewMaterial();
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, frame, glass, false), Is.True);
        Assert.That(visual.sharedMaterials[0], Is.Not.SameAs(frame), "Freshness checks must not mutate renderers.");
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, frame, glass, true), Is.True);
        Assert.That(visual.sharedMaterials, Is.EqualTo(new[] { frame, glass }));
        Assert.That(attachment.Instance, Is.SameAs(instance));
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, frame, glass, false), Is.False);
    }

    [Test]
    public void ManualMaterialAndTransformOverridesSurviveHostChanges()
    {
        var frame = NewMaterial(); var glass = NewMaterial(); var custom = NewMaterial();
        HouseDoorObjects.RefreshMaterials(attachment, frame, glass, true);
        visual.sharedMaterials = new[] { custom, glass };
        visual.transform.localPosition += Vector3.up;
        var position = visual.transform.localPosition;
        var nextFrame = NewMaterial(); var nextGlass = NewMaterial();
        HouseDoorObjects.RefreshMaterials(attachment, nextFrame, nextGlass, true);
        Assert.That(visual.sharedMaterials, Is.EqualTo(new[] { custom, nextGlass }));
        Assert.That(visual.transform.localPosition, Is.EqualTo(position));
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, nextFrame, nextGlass, false), Is.False);
    }

    [Test]
    public void InheritedBindingsSurviveSerializationAndCanBeRefreshedAgain()
    {
        var frame = NewMaterial(); var glass = NewMaterial();
        HouseDoorObjects.RefreshMaterials(attachment, frame, glass, true);
        string serialized = EditorJsonUtility.ToJson(attachment);
        attachment.InheritedMaterials = System.Array.Empty<Material>();
        EditorJsonUtility.FromJsonOverwrite(serialized, attachment);
        var nextFrame = NewMaterial(); var nextGlass = NewMaterial();
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, nextFrame, nextGlass, true), Is.True);
        Assert.That(visual.sharedMaterials, Is.EqualTo(new[] { nextFrame, nextGlass }));
    }

    [Test]
    public void OrdinaryHostRetainsThePrefabDefaults()
    {
        var original = visual.sharedMaterials;
        Assert.That(HouseDoorObjects.RefreshMaterials(attachment, null, null, true), Is.False);
        Assert.That(visual.sharedMaterials, Is.EqualTo(original));
    }
}
