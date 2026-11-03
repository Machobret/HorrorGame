using System;
using System.IO;
using System.Linq;
using HorrorGame.Characters.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Characters.Validation
{
    /// <summary>Gravity-drape regression for a pitched upper body, including return to upright.</summary>
    [InitializeOnLoad]
    public static class SupermanLookDownValidation
    {
        const string Key = "Superman.LookDownValidation";
        static SupermanShowcase showcase;
        static Cloth cloth;
        static SkinnedMeshRenderer skin;
        static Quaternion uprightChest;
        static int[] hem;
        static int startFrame, lastFrame;
        static double started;
        static Result result;
        [Serializable] sealed class Result
        {
            public string status = "RUNNING", failure;
            public float uprightAngle, lookDownAngle, returnedUprightAngle, maxPinError, measuredChestBend;
            public int frames;
        }
        static string Output => Path.Combine(SupermanBuilder.Output, "LookDown");

        static SupermanLookDownValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        public static void RunBaseline() { Start("before"); }
        public static void RunBatch() { Start("after"); }
        public static void BuildAndRunBatch()
        {
            try { SupermanBuilder.Build(); RunBatch(); }
            catch (Exception ex) { Finish(ex); }
        }
        static void Start(string label)
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(SupermanBuilder.Scene);
            SessionState.SetString(Key, label);
            EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || string.IsNullOrEmpty(SessionState.GetString(Key, ""))) return;
            try
            {
                showcase = Object.FindFirstObjectByType<SupermanShowcase>();
                showcase.freezeMotion = true; showcase.HoldClip(0); showcase.lookDownDegrees = 0;
                cloth = showcase.character.GetComponentInChildren<Cloth>(); skin = cloth.GetComponent<SkinnedMeshRenderer>();
                var rest = skin.sharedMesh.vertices;
                var particles = cloth.vertices;
                float limit = skin.sharedMesh.bounds.min.y + skin.sharedMesh.bounds.size.y * .08f;
                hem = Enumerable.Range(0, particles.Length).Where(i =>
                {
                    var nearest = Enumerable.Range(0, rest.Length).OrderBy(j => (rest[j] - particles[i]).sqrMagnitude).First();
                    return rest[nearest].y <= limit;
                }).ToArray();
                SupermanBuilder.Require(hem.Length > 0, "No hem particles identified");
                result = new Result(); startFrame = Time.frameCount; lastFrame = -1;
                started = EditorApplication.timeSinceStartup;
                Time.captureFramerate = 60; Application.runInBackground = true;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Finish(ex); }
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new TimeoutException("Look-down validation timed out");
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                int frame = Time.frameCount - startFrame;
                showcase.lookDownDegrees = frame < 120 ? 0 : frame < 180 ? (frame - 120) * 55f / 60 : frame < 420 ? 55 : frame < 480 ? 55 - (frame - 420) * 55f / 60 : 0;
                var vertices = cloth.vertices;
                var coefficients = cloth.coefficients;
                var pins = vertices.Where((p, i) => coefficients[i].maxDistance == 0).Select(p => skin.transform.TransformPoint(p)).ToArray();
                var pinTargets = skin.sharedMesh.vertices.Where((p, i) => skin.sharedMesh.uv2[i].x == 0).ToArray();
                float current = 0;
                foreach (var p in vertices.Where((p, i) => coefficients[i].maxDistance == 0))
                {
                    current = Mathf.Max(current, Mathf.Sqrt(pinTargets.Min(t => (t - p).sqrMagnitude)));
                }
                if (frame > 30)
                {
                    result.maxPinError = Mathf.Max(result.maxPinError, current);
                }
                var anchor = pins.Aggregate(Vector3.zero, (a, b) => a + b) / pins.Length;
                var hemCenter = hem.Select(i => skin.transform.TransformPoint(vertices[i])).Aggregate(Vector3.zero, (a, b) => a + b) / hem.Length;
                float angle = Vector3.Angle(Vector3.down, hemCenter - anchor);
                var chest = showcase.character.GetComponentsInChildren<Transform>().Single(b => b.name == "DEF-spine.003");
                if (frame == 110) { result.uprightAngle = angle; uprightChest = chest.rotation; Capture("upright"); }
                if (frame == 410) { result.lookDownAngle = angle; result.measuredChestBend = Quaternion.Angle(uprightChest, chest.rotation); Capture("look_down_55"); }
                if (frame < 650) return;
                result.returnedUprightAngle = angle; result.frames = frame; Capture("returned_upright");
                if (SessionState.GetString(Key, "") == "after")
                {
                    SupermanBuilder.Require(result.measuredChestBend > 50, "Look-down pose was not applied to the animated chest");
                    SupermanBuilder.Require(result.lookDownAngle < 30, "Cape follows the torso pitch instead of gravity: " + result.lookDownAngle);
                    SupermanBuilder.Require(result.returnedUprightAngle < 25, "Cape failed to settle after looking up: " + result.returnedUprightAngle);
                    SupermanBuilder.Require(result.maxPinError < .002f, "Look-down movement displaced the attachment pins");
                }
                Finish(null);
            }
            catch (Exception ex) { Finish(ex); }
        }
        static void Capture(string name)
        {
            var camera = showcase.reviewCamera;
            camera.transform.position = new Vector3(3, 1.5f, -.3f);
            camera.transform.LookAt(showcase.character.position + Vector3.up * .95f);
            var target = new RenderTexture(900, 1000, 24); var active = RenderTexture.active; var old = camera.targetTexture;
            var image = new Texture2D(900, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Output, SessionState.GetString(Key, "") + "_" + name + ".png"), image.EncodeToPNG());
            }
            finally { camera.targetTexture = old; RenderTexture.active = active; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
        }
        static void Finish(Exception ex)
        {
            if (result == null) result = new Result();
            result.status = ex == null ? "PASS" : "FAIL"; result.failure = ex?.ToString();
            string label = SessionState.GetString(Key, "unknown");
            File.WriteAllText(Path.Combine(Output, label + "_report.json"), JsonUtility.ToJson(result, true));
            SessionState.SetString(Key, ""); EditorApplication.update -= Tick;
            if (ex != null) Debug.LogException(ex);
            EditorApplication.Exit(ex == null ? 0 : 1);
        }
    }
}
