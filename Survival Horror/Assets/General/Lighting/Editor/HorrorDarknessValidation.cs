using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorGame.Lighting.Editor
{
    // Batch-only checks in a disposable scene; never save or modify a game scene.
    public static class HorrorDarknessValidation
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate batch editor.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var manager = new GameObject("Validation Manager").AddComponent<HorrorDarknessVisibilityManager>();
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.AddComponent<HorrorEngine.PlayerActor>();
            var body = player.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            body.sharedMaterial = material;
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.transform.SetParent(player.transform, false);
            var gunRenderer = gun.GetComponent<Renderer>();
            var gunSource = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.mat");
            Require(gunSource != null, "Real gun material loaded");
            gunRenderer.sharedMaterial = gunSource;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(.65f, 1f, 0f);
            wall.transform.localScale = new Vector3(.2f, 2f, 3f);
            UnityEngine.Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
            wall.AddComponent<MeshCollider>().sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            Call(manager, "UpdateCloseGlowProximity");
            Require((float)Field("closeGlowTarget").GetValue(manager) > .1f, "Wall proximity with non-convex mesh");
            Field("closeGlowAmount").SetValue(manager, 1f);
            Call(manager, "RefreshTrackedObjects");
            Call(manager, "RefreshLights");
            Call(manager, "ApplyVisibility", true);
            Require(gunRenderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit",
                "Equipped unlit gun automatically receives a lit shader");
            Require(gunSource.shader.name == "HorrorEngine/UnlitAlphaCutout",
                "Gun source asset remains unchanged");
            Require(gunRenderer.sharedMaterial.GetTexture("_BaseMap") == gunSource.GetTexture("_MainTex"),
                "Gun texture survives conversion");
            Require(gunRenderer.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"),
                "Gun cutout transparency survives conversion");
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block, 0);
            Require(body.sharedMaterial.IsKeywordEnabled("_EMISSION"), "Player emission enabled on runtime copy");
            Require(body.sharedMaterial != material, "Shared player material preserved");
            Require(body.sharedMaterial.GetTexture("_EmissionMap") == Texture2D.whiteTexture, "Player texture retained");
            Require(block.GetColor("_EmissionColor").r > .001f, "Player faint fill in darkness");
            Require(block.GetColor("_BaseColor") == material.GetColor("_BaseColor"),
                "Player albedo preserved for ambient, baked and realtime lighting");
            Field("block").SetValue(manager, null);
            Call(manager, "LateUpdate");
            Require(Field("block").GetValue(manager) != null, "Missing material buffer recovered");
            wall.transform.position = new Vector3(20f, 1f, 0f);
            Physics.SyncTransforms();
            Call(manager, "UpdateCloseGlowProximity");
            Require((float)Field("closeGlowTarget").GetValue(manager) == 0f, "No proximity in open space");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -.2f, 0f);
            floor.transform.localScale = new Vector3(10f, .2f, 10f);
            Physics.SyncTransforms();
            Call(manager, "UpdateCloseGlowProximity");
            Require((float)Field("closeGlowTarget").GetValue(manager) == 0f, "Floor does not activate glow");
            var dust = new GameObject("Unnamed effect").AddComponent<ParticleSystem>();
            dust.transform.position = new Vector3(3f, 1f, 0f);
            Call(manager, "RefreshParticles");
            Field("closeGlowAmount").SetValue(manager, 0f);
            Call(manager, "ApplyVisibility", true);
            Require(dust.GetComponent<Renderer>().forceRenderingOff, "Unnamed particles hidden in darkness");
            body.GetPropertyBlock(block, 0);
            Require(block.GetColor("_EmissionColor").r == 0f, "Player fill off away from solids");
            var lamp = new GameObject("Validation Lamp").AddComponent<Light>();
            lamp.transform.position = new Vector3(3f, 1f, 2f);
            lamp.range = 5f;
            lamp.intensity = 10f;
            Call(manager, "RefreshLights");
            Call(manager, "ApplyVisibility", true);
            Require(!dust.GetComponent<Renderer>().forceRenderingOff, "Particles return under real light");
            wall.transform.position = new Vector3(3f, 1f, 1f);
            wall.transform.localScale = new Vector3(3f, 2f, .2f);
            Physics.SyncTransforms();
            Call(manager, "ApplyVisibility", true);
            Require(dust.GetComponent<Renderer>().forceRenderingOff, "Wall blocks light from revealing particles");
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            UnityEngine.Object.DestroyImmediate(material);
            Debug.Log("HORROR_DARKNESS_VALIDATION_PASSED");
        }

        private static FieldInfo Field(string name) => typeof(HorrorDarknessVisibilityManager)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("Darkness validation failed: " + message);
            Debug.Log("PASS: " + message);
        }
    }
}
