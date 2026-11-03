using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame.Characters.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace HorrorGame.Characters.Validation
{
    /// <summary>Repeatable deformation, mask, and actual CPU cloth Play Mode checks.</summary>
    [InitializeOnLoad]
    public static class SupermanValidation
    {
        const string Key = "SupermanValidation.Run";
        static SupermanShowcase showcase;
        static Cloth cloth;
        static SkinnedMeshRenderer cape, reference;
        static Mesh baked;
        static int startFrame, previousFrame, resetBaseline;
        static double started;
        static Report report;
        static Vector3[] previousPinned;
        static Vector3 previousRootPosition;
        static Quaternion previousRootRotation;

        [Serializable] sealed class Report
        {
            public string result = "RUNNING";
            public int animationClips, animationSamples, runtimeFrames, clothParticles, pinnedParticles, simulatedParticles;
            public float maxHandCenterError, maxAttachmentOffset, maxPinnedError, maxCurrentPoseQueryError, maxClothDeparture;
            public bool resetAfterTeleport, resetAfterReenable;
            public string failure;
        }
        [Serializable] sealed class ParticleSample
        {
            public Vector3[] particles, skin;
            public Vector2[] mask;
            public float[] maxDistances;
            public Vector3 rootPosition;
            public Quaternion rootRotation;
        }

        static SupermanValidation() { EditorApplication.playModeStateChanged += OnPlay; }

        public static void BuildAndRunBatch()
        {
            try { SupermanBuilder.Build(); RunBatch(); }
            catch (Exception ex) { Fail(ex); }
        }

        [MenuItem("Horror Game/Characters/Validate Superman Cloth")]
        public static void RunBatch()
        {
            try
            {
                Directory.CreateDirectory(SupermanBuilder.Output);
                report = ValidateAnimationPoses();
                WriteReport();
                EditorSceneManager.OpenScene(SupermanBuilder.Scene);
                SessionState.SetBool(Key, true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex) { Fail(ex); }
        }

        static Report ValidateAnimationPoses()
        {
            var metrics = new Report();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SupermanBuilder.Folder + "Superman_Visual.prefab");
            var root = Object.Instantiate(asset);
            var originalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SupermanBuilder.Skeleton);
            var originalSkin = originalAsset.GetComponentInChildren<SkinnedMeshRenderer>(true);
            try
            {
                var simulation = root.GetComponentInChildren<Cloth>();
                simulation.enabled = false;
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.sharedMesh.name == "Superman_BodyMesh");
                var attachment = root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "Superman_CapeAttachment");
                var bodyRest = body.sharedMesh.vertices;
                var attachmentRest = attachment.sharedMesh.vertices;
                var attachmentRole = attachment.sharedMesh.uv2;
                var bodyWeights = body.sharedMesh.boneWeights;
                var attachmentWeights = attachment.sharedMesh.boneWeights;
                var attachmentMap = attachmentRest.Select((p, j) => attachmentRole.Length > j && attachmentRole[j].x > .5f ? -1 : Enumerable.Range(0, bodyRest.Length)
                    .Where(i => SameWeights(bodyWeights[i], attachmentWeights[j]))
                    .OrderBy(i => (bodyRest[i] - p).sqrMagnitude).First()).ToArray();
                var transforms = root.GetComponentsInChildren<Transform>(true);
                var comparisonObject = new GameObject("Original skin comparison");
                comparisonObject.transform.SetParent(body.transform.parent, false);
                comparisonObject.transform.localPosition = body.transform.localPosition;
                comparisonObject.transform.localRotation = body.transform.localRotation;
                comparisonObject.transform.localScale = body.transform.localScale;
                var comparison = comparisonObject.AddComponent<SkinnedMeshRenderer>();
                comparison.sharedMesh = originalSkin.sharedMesh;
                comparison.bones = originalSkin.bones.Select(b => transforms.Single(t => t.name == b.name)).ToArray();
                comparison.rootBone = body.rootBone; comparison.enabled = false;
                var animator = root.GetComponentInChildren<Animator>();
                animator.fireEvents = false;
                var clips = animator.runtimeAnimatorController.animationClips.Concat(
                    AssetDatabase.FindAssets("t:AnimatorOverrideController").Select(g => AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AssetDatabase.GUIDToAssetPath(g)))
                        .Where(c => c).SelectMany(c => c.animationClips)).Where(c => c).Distinct().ToArray();
                var bodyBake = new Mesh(); var oldBake = new Mesh(); var attachmentBake = new Mesh();
                var bodyHandIds = new[] { HandIndices(body, "DEF-hand.L"), HandIndices(body, "DEF-hand.R") };
                var oldHandIds = new[] { HandIndices(comparison, "DEF-hand.L"), HandIndices(comparison, "DEF-hand.R") };
                try
                {
                    foreach (var clip in clips)
                    {
                        animator.Rebind(); animator.Update(0);
                        PlayableGraph graph;
                        AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
                        graph.Stop();
                        try
                        {
                            for (int f = 0; f < 5; f++)
                            {
                                graph.GetRootPlayable(0).SetTime(clip.length * f / 4);
                                graph.Evaluate(0);
                                body.BakeMesh(bodyBake); comparison.BakeMesh(oldBake);
                                var vertices = bodyBake.vertices;
                                attachment.BakeMesh(attachmentBake);
                                var attachmentPoints = attachmentBake.vertices;
                                for (int i = 0; i < attachmentPoints.Length; i++)
                                    if (attachmentMap[i] >= 0) metrics.maxAttachmentOffset = Mathf.Max(metrics.maxAttachmentOffset, Vector3.Distance(attachmentPoints[i], vertices[attachmentMap[i]]));
                                foreach (var v in vertices)
                                    SupermanBuilder.Require(Finite(v) && v.magnitude < 6, "Invalid body deformation: " + clip.name);
                                for (int hand = 0; hand < 2; hand++)
                                {
                                    var a = Center(vertices, bodyHandIds[hand]);
                                    var b = Center(oldBake.vertices, oldHandIds[hand]);
                                    metrics.maxHandCenterError = Mathf.Max(metrics.maxHandCenterError, Vector3.Distance(a, b));
                                }
                                metrics.animationSamples++;
                            }
                        }
                        finally { graph.Destroy(); }
                    }
                    metrics.animationClips = clips.Length;
                    SupermanBuilder.Require(metrics.maxHandCenterError < .005f, "Weapon hand-center error exceeds 5 mm: " + metrics.maxHandCenterError);
                    SupermanBuilder.Require(metrics.maxAttachmentOffset < .03f, "Shoulder attachment diverged from the body's skin: " + metrics.maxAttachmentOffset);
                }
                finally { Object.DestroyImmediate(bodyBake); Object.DestroyImmediate(oldBake); Object.DestroyImmediate(attachmentBake); }
                return metrics;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static int[] HandIndices(SkinnedMeshRenderer skin, string name)
        {
            int bone = Array.FindIndex(skin.bones, b => b.name == name);
            var seen = new HashSet<Vector3>();
            var vertices = skin.sharedMesh.vertices;
            return skin.sharedMesh.boneWeights.Select((w, i) => new { w, i }).Where(e =>
            {
                var w = e.w;
                float value = (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
                              (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
                var v = vertices[e.i];
                var key = new Vector3(Mathf.Round(v.x * 100000), Mathf.Round(v.y * 100000), Mathf.Round(v.z * 100000));
                return value > .5f && seen.Add(key);
            }).Select(e => e.i).ToArray();
        }

        static Vector3 Center(Vector3[] vertices, int[] indices)
        {
            SupermanBuilder.Require(indices.Length > 0, "No dominant hand vertices");
            return indices.Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / indices.Length;
        }

        static bool SameWeights(BoneWeight a, BoneWeight b)
        {
            var weights = new Dictionary<int, float>();
            void Add(int i, float w) { weights[i] = (weights.TryGetValue(i, out var old) ? old : 0) + w; }
            Add(a.boneIndex0, a.weight0); Add(a.boneIndex1, a.weight1); Add(a.boneIndex2, a.weight2); Add(a.boneIndex3, a.weight3);
            Add(b.boneIndex0, -b.weight0); Add(b.boneIndex1, -b.weight1); Add(b.boneIndex2, -b.weight2); Add(b.boneIndex3, -b.weight3);
            return weights.Values.All(w => Mathf.Abs(w) < .001f);
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            try
            {
                report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(SupermanBuilder.Output, "validation_report.json")));
                showcase = Object.FindFirstObjectByType<SupermanShowcase>();
                showcase.secondsPerClip = 2;
                cloth = showcase.character.GetComponentInChildren<Cloth>();
                cape = cloth.GetComponent<SkinnedMeshRenderer>();
                var referenceObject = new GameObject("Pure skin reference");
                referenceObject.transform.SetParent(cape.transform.parent, false);
                referenceObject.transform.localPosition = cape.transform.localPosition;
                referenceObject.transform.localRotation = cape.transform.localRotation;
                referenceObject.transform.localScale = cape.transform.localScale;
                reference = referenceObject.AddComponent<SkinnedMeshRenderer>();
                reference.sharedMesh = cape.sharedMesh; reference.bones = cape.bones; reference.rootBone = cape.rootBone; reference.enabled = false;
                baked = new Mesh();
                report.clothParticles = cloth.coefficients.Length;
                report.pinnedParticles = cloth.coefficients.Count(c => c.maxDistance == 0);
                report.simulatedParticles = cloth.coefficients.Count(c => c.maxDistance > 0);
                var build = JsonUtility.FromJson<SupermanBuilder.BuildReport>(File.ReadAllText(Path.Combine(SupermanBuilder.Output, "build_report.json")));
                SupermanBuilder.Require(report.pinnedParticles == build.pinnedParticles && report.simulatedParticles == build.simulatedParticles, "Author mask changed after prefab save");
                SupermanBuilder.Require(cloth.capsuleColliders.Length == 12 && cloth.capsuleColliders.All(c => c), "Missing cloth collision shapes");
                Time.captureFramerate = 60;
                Application.runInBackground = true;
                startFrame = Time.frameCount; previousFrame = -1;
                started = EditorApplication.timeSinceStartup;
                previousPinned = null;
                File.WriteAllText(Path.Combine(SupermanBuilder.Output, "pin_timing.csv"), "frame,clip,current,previousRootAndSkin,currentRootPreviousSkin,previousRootCurrentSkin\n");
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new TimeoutException("Cloth runtime validation timed out");
                if (!EditorApplication.isPlaying || Time.frameCount == previousFrame) return;
                previousFrame = Time.frameCount;
                int frame = Time.frameCount - startFrame;
                if (frame < 30) return;
                reference.BakeMesh(baked);
                var points = baked.vertices.Select(p => reference.transform.TransformPoint(p)).ToArray();
                var mask = cape.sharedMesh.uv2;
                var pinned = points.Where((p, i) => mask[i].x == 0).ToArray();
                var coefficients = cloth.coefficients;
                var vertices = cloth.vertices;
                if (frame == 90)
                    File.WriteAllText(Path.Combine(SupermanBuilder.Output, "particles.json"), JsonUtility.ToJson(new ParticleSample
                    {
                        particles = vertices, skin = points, mask = mask,
                        maxDistances = coefficients.Select(c => c.maxDistance).ToArray(),
                        rootPosition = cape.transform.position, rootRotation = cape.transform.rotation
                    }));
                float currentError = 0, previousError = 0, mixedA = 0, mixedB = 0;
                for (int i = 0; i < vertices.Length && (frame < 860 || frame > 865); i++)
                {
                    var world = SupermanBuilder.ClothToWorld(cape, vertices[i]);
                    SupermanBuilder.Require(Finite(world) && Vector3.Distance(world, showcase.character.position) < 5, "Non-finite or exploded cape simulation");
                    if (coefficients[i].maxDistance == 0)
                    {
                        float distance = Mathf.Sqrt(pinned.Min(p => (p - world).sqrMagnitude));
                        currentError = Mathf.Max(currentError, distance);
                        if (previousPinned != null)
                        {
                            var priorWorld = previousRootPosition + previousRootRotation * vertices[i];
                            previousError = Mathf.Max(previousError, Mathf.Sqrt(previousPinned.Min(p => (p - priorWorld).sqrMagnitude)));
                            mixedA = Mathf.Max(mixedA, Mathf.Sqrt(previousPinned.Min(p => (p - world).sqrMagnitude)));
                            mixedB = Mathf.Max(mixedB, Mathf.Sqrt(pinned.Min(p => (p - priorWorld).sqrMagnitude)));
                        }
                    }
                    else
                    {
                        float distance = Mathf.Sqrt(points.Min(p => (p - world).sqrMagnitude));
                        report.maxClothDeparture = Mathf.Max(report.maxClothDeparture, distance);
                    }
                }
                // A native query can precede the animation/physics synchronization.
                // Check pinned shape independently of that rigid-pose timing, and
                // retain the absolute current-pose query offset for diagnosis.
                report.maxCurrentPoseQueryError = Mathf.Max(report.maxCurrentPoseQueryError, currentError);
                report.maxPinnedError = Mathf.Max(report.maxPinnedError, currentError);
                if (frame < 860 || frame > 865)
                {
                    var particlePins = vertices.Where((p, i) => coefficients[i].maxDistance == 0)
                        .Select(p => SupermanBuilder.ClothToWorld(cape, p)).ToArray();
                    var targets = pinned.GroupBy(p => new Vector3(Mathf.Round(p.x * 10000), Mathf.Round(p.y * 10000), Mathf.Round(p.z * 10000)))
                        .Select(g => g.First()).ToArray();
                    SupermanBuilder.Require(targets.Length == particlePins.Length, "Pinned particle topology changed");
                }
                File.AppendAllText(Path.Combine(SupermanBuilder.Output, "pin_timing.csv"), frame + "," + showcase.CurrentClip + "," + currentError + "," + previousError + "," + mixedA + "," + mixedB + "\n");
                previousPinned = pinned;
                previousRootPosition = cape.transform.position;
                previousRootRotation = cape.transform.rotation;
                if (frame % 120 == 90)
                {
                    Capture("runtime_" + frame.ToString("D4") + "_" + showcase.CurrentClip);
                    WriteReport();
                }
                var lifecycle = cape.GetComponent<SupermanCape>();
                if (frame == 860)
                {
                    resetBaseline = lifecycle.ResetCount;
                    showcase.reviewOffset += Vector3.right * 5;
                }
                if (frame == 862) report.resetAfterTeleport = lifecycle.ResetCount > resetBaseline;
                if (frame == 870)
                {
                    int before = lifecycle.ResetCount;
                    cape.gameObject.SetActive(false); cape.gameObject.SetActive(true);
                    report.resetAfterReenable = lifecycle.ResetCount == before + 1;
                }
                if (frame < 900) return;
                report.runtimeFrames = frame;
                SupermanBuilder.Require(report.maxPinnedError < .002f, "Pinned cape row lost its skinned shape: " + report.maxPinnedError);
                SupermanBuilder.Require(report.maxCurrentPoseQueryError < .0001f, "Chest-bound pins moved from their attachment: " + report.maxCurrentPoseQueryError);
                SupermanBuilder.Require(report.maxClothDeparture > .10f, "Cape did not simulate independently of skin");
                SupermanBuilder.Require(report.resetAfterTeleport && report.resetAfterReenable, "Cloth lifecycle reset failed");
                report.result = "PASS"; WriteReport();
                Debug.Log("SUPERMAN_VALIDATION_PASS " + JsonUtility.ToJson(report));
                SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Capture(string name)
        {
            var camera = showcase.reviewCamera;
            var target = new RenderTexture(900, 1000, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var image = new Texture2D(900, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(SupermanBuilder.Output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            }
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                                         !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        static void WriteReport() => File.WriteAllText(Path.Combine(SupermanBuilder.Output, "validation_report.json"), JsonUtility.ToJson(report, true));

        static void Fail(Exception ex)
        {
            if (report == null) report = new Report();
            report.result = "FAIL"; report.failure = ex.ToString(); WriteReport();
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }
}
