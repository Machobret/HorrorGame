using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TemporaryHouseDemo.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TemporaryHouseDemo.Tests
{
    public sealed class HouseWalkthroughTests
    {
        [UnityTest]
        public IEnumerator NativePlayerEnemyAndRoomCamerasWork()
        {
            EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            var originalInputSettings=InputSystem.settings;
            InputSystem.settings=Object.Instantiate(originalInputSettings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Component Find(string type) => (Component)Object.FindFirstObjectByType(NativeGameplaySetup.NativeType(type));
            Component player = null;
            for (int i=0;i<300 && player==null;i++) { yield return null; player=Find("HorrorEngine.PlayerActor"); }
            Assert.That(player,Is.Not.Null,"HECore / GameManager did not spawn the native player");
            Assert.That(Find("HorrorEngine.GameManager"),Is.Not.Null);
            Assert.That(Find("HorrorEngine.CameraSystem"),Is.Not.Null);
            // Batch runs have no Game View pointer. Disable legacy OnMouse raycasts in
            // the test harness; native input actions and camera trigger volumes remain active.
            foreach(var camera in Camera.allCameras) camera.eventMask=0;
            Assert.That(GameObject.Find("Demo Player - independent walkthrough"),Is.Null);
            var movement = player.GetComponent(NativeGameplaySetup.NativeType("HorrorEngine.PlayerMovement"));
            Assert.That(movement,Is.Not.Null);
            Assert.That(((Behaviour)movement).enabled,Is.True);
            var controller = player.GetComponent<CharacterController>();
            Assert.That(controller,Is.Not.Null);
            var enemy = GameObject.Find("House - Native Runner Enemy");
            Assert.That(enemy,Is.Not.Null);
            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.That(agent && agent.enabled && agent.isOnNavMesh,Is.True,"Native enemy must be on the baked NavMesh");
            enemy.SetActive(false); // Keep combat from interrupting input and room-transition checks.
            var transforms=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Vector3 Marker(string name)=>transforms.Single(t=>t.name==name).position;
            var origin=Marker("DemoOrigin");
            var x=Marker("DemoAxisX")-origin; var y=Marker("DemoAxisY")-origin; var z=Marker("DemoAxisZ")-origin;
            Vector3 Map(float a,float b,float c)=>origin+x*a+y*b+z*c;
            for(int i=0;i<15;i++) yield return new WaitForFixedUpdate();
            AssertCamera("Corridor South");
            for(int i=0;i<90;i++) yield return null;
            Capture("Corridor South");
            var listener=player.GetComponent(NativeGameplaySetup.NativeType("HorrorEngine.PlayerInputListener"));
            Assert.That(listener,Is.Not.Null);
            var actionsField=listener.GetType().GetField("m_Actions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<60 && actionsField.GetValue(listener)==null;i++) yield return null;
            Assert.That(actionsField.GetValue(listener),Is.Not.Null,"Native input actions never initialized");
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var before=player.transform.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                InputSystem.Update();
                yield return null;
                for(int i=0;i<20;i++) yield return new WaitForFixedUpdate();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.Update();
                Assert.That(Vector3.Distance(before,player.transform.position),Is.GreaterThan(.05f),"Native player did not respond to its existing movement bindings");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            foreach(var destination in new[] { ("Kitchen",2f,2f),("Bedroom",3f,6f),("Corridor North",5f,6f) })
            {
                var requested=Map(destination.Item2,destination.Item3,.04f);
                Assert.That(NavMesh.SamplePosition(requested,out var hit,.35f,NavMesh.AllAreas),Is.True);
                var path=new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(Map(5,2,.04f),hit.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete,Is.True,"Room disconnected on NavMesh: "+destination.Item1);
                controller.enabled=false;
                player.transform.position=hit.position+Vector3.up*.04f;
                controller.enabled=true;
                Physics.SyncTransforms();
                // Preserve the native two-second camera blend; inspect its completed view.
                for(int i=0;i<125;i++) yield return new WaitForFixedUpdate();
                AssertCamera(destination.Item1);
                Capture(destination.Item1);
            }
            InputSystem.settings=originalInputSettings;
            yield return new ExitPlayMode();
        }

        private static void AssertCamera(string room)
        {
            var system=(Component)Object.FindFirstObjectByType(NativeGameplaySetup.NativeType("HorrorEngine.CameraSystem"));
            var active=system.GetType().GetProperty("ActiveCamera").GetValue(system) as Component;
            Assert.That(active,Is.Not.Null,"No native camera active in "+room);
            var pov=active.GetComponentInParent(NativeGameplaySetup.NativeType("HorrorEngine.CameraPOV"));
            Assert.That(pov.gameObject.name,Is.EqualTo("House Camera - "+room));
            Assert.That(Camera.main,Is.Not.Null);
            Assert.That(float.IsNaN(Camera.main.transform.position.x) || float.IsNaN(Camera.main.fieldOfView),Is.False,"Native camera pose/lens must be finite");
            Assert.That(Camera.main.transform.IsChildOf(system.transform),Is.True,"Output must be the native CameraSystem camera");
            Assert.That(Vector3.Distance(Camera.main.transform.position,active.transform.position),Is.LessThan(.03f),"Native camera blend has not reached the room pose");
        }

        private static void Capture(string room)
        {
            string directory=Environment.GetEnvironmentVariable("TEMP_HOUSE_PREVIEW_DIR");
            if(string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            var camera=Camera.main;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
            var previous=RenderTexture.active;
            RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory,"unity_native_house_"+room.ToLowerInvariant().Replace(" ","_")+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;
            Object.DestroyImmediate(image);
            target.Release(); Object.DestroyImmediate(target);
        }
    }
}
