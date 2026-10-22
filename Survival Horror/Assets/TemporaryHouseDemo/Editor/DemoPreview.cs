using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TemporaryHouseDemo.Editor
{
    public static class DemoPreview
    {
        public static void Capture()
        {
            EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath);
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Vector3 Marker(string n) => transforms.Single(t => t.name == n).position;
            var origin = Marker("DemoOrigin");
            var x = Marker("DemoAxisX") - origin;
            var y = Marker("DemoAxisY") - origin;
            var z = Marker("DemoAxisZ") - origin;
            Vector3 Map(float a, float b, float c) => origin + x*a + y*b + z*c;
            var camera = Camera.main;
            var output = Environment.GetEnvironmentVariable("TEMP_HOUSE_PREVIEW_DIR");
            if (string.IsNullOrEmpty(output)) output = "Temp/HousePreview";
            Directory.CreateDirectory(output);
            void Shot(string name, Vector3 position, Vector3 target)
            {
                camera.transform.position = position;
                camera.transform.rotation = Quaternion.LookRotation(target-position,Vector3.up);
                var rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
                rt.Create();
                var request = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(camera, request);
                var prior = RenderTexture.active;
                RenderTexture.active = rt;
                var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
                RenderTexture.active = prior;
                UnityEngine.Object.DestroyImmediate(image);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            Shot("unity_house_kitchen", Map(3.66f,1.99f,1.75f),Map(.55f,1.8f,1.35f));
            Shot("unity_house_bedroom", Map(3.63f,5.95f,1.75f),Map(1.1f,6.5f,1.2f));
            Shot("unity_house_corridor", Map(5,.58f,1.75f),Map(4.95f,6.9f,1.65f));
            Debug.Log("TEMP_HOUSE_PREVIEW_PASS " + output);
        }
    }
}
