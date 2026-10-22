using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TemporaryHouseDemo.Tests
{
    public sealed class HouseWalkthroughTests
    {
        [UnityTest]
        public IEnumerator PlayerCanWalkFromCorridorIntoBothRooms()
        {
            EditorSceneManager.OpenScene(Editor.DemoSceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var walker = Object.FindFirstObjectByType<DemoWalker>();
            Assert.That(walker, Is.Not.Null);
            walker.enabled = false; // Move deterministically without reading the user's input.
            var controller = walker.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Vector3 Marker(string n) => transforms.Single(t => t.name == n).position;
            var origin = Marker("DemoOrigin");
            var x = Marker("DemoAxisX") - origin;
            var y = Marker("DemoAxisY") - origin;
            var z = Marker("DemoAxisZ") - origin;
            Vector3 Map(float a, float b, float c) => origin + x*a + y*b + z*c;
            var waypoints = new[] { new Vector2(3,2),new Vector2(2,2),new Vector2(3,2),new Vector2(5,2),new Vector2(5,6),new Vector2(3,6) };
            foreach (var waypoint in waypoints)
            {
                var destination = Map(waypoint.x,waypoint.y,0);
                int frames = 0;
                while (frames++ < 600)
                {
                    var direction = destination - controller.transform.position;
                    direction.y = 0;
                    if (direction.magnitude < .12f) break;
                    controller.Move((direction.normalized * 2.2f + Vector3.down * 2) * .02f);
                    yield return null;
                }
                var remainder = destination - controller.transform.position;
                remainder.y = 0;
                Assert.That(remainder.magnitude, Is.LessThan(.15f), "Player blocked at room waypoint " + waypoint);
                Assert.That(controller.transform.position.y, Is.InRange(-.1f,.3f), "Player fell through floor");
            }
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.GetComponent<AudioListener>(), Is.Not.Null);
            yield return new ExitPlayMode();
        }
    }
}
