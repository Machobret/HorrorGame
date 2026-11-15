using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DoorExitPrefabTests
{
    static IEnumerable<string> Doors()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Game/Doors/Prefabs"}))
            yield return AssetDatabase.GUIDToAssetPath(guid);
        yield return "Assets/AssetStore/HorrorEngine/Prefabs/Door.prefab";
        yield return "Assets/AssetStore/HorrorEngine/Prefabs/CrossSceneDoor.prefab";
    }

    [TestCaseSource(nameof(Doors))]
    public void WiredDestinationsSitCloseToTheDoor(string path)
    {
        var door = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(door, path);
        foreach (var component in door.GetComponents<MonoBehaviour>())
        {
            if (!component) continue;
            using var data = new SerializedObject(component);
            var first = data.FindProperty("ExitPoint");
            if (first == null) continue;
            var other = data.FindProperty("OtherExitPoint");
            Check(first, other == null ? 1 : -1);
            if (other != null) Check(other, 1);
            return;
        }
        Assert.Fail("Door has no wired exit: " + path);

        void Check(SerializedProperty property, int side)
        {
            var exit = property.objectReferenceValue as Transform;
            Assert.IsNotNull(exit, property.name);
            var expected = door.transform.TransformPoint(Vector3.forward * (side * .65f));
            Assert.That(Vector3.Distance(exit.position, expected), Is.LessThan(.001f), path);
            Assert.That(Vector3.Dot(exit.forward, door.transform.forward * side), Is.GreaterThan(.999f), path);
        }
    }
}
