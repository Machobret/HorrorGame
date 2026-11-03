using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HorrorGame.Characters.Editor
{
    /// <summary>Rebuild the Superman skin without editing the base player or vendor assets.</summary>
    public static class SupermanBuilder
    {
        public const string Folder = "Assets/Game/Characters/Superman/";
        public const string BasePlayer = "Assets/AssetStore/HorrorEngine/Prefabs/Character.prefab";
        public const string Skeleton = "Assets/AssetStore/HorrorEngine/ExampleContent/Character/CharacterPlaceholder.fbx";
        public const string Scene = "Assets/Sandbox/Superman/Superman_ClothReview.unity";
        public static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Exports/Reviews/Superman");

        [Serializable] public sealed class BuildReport
        {
            public string result;
            public int bodyVertices, attachmentVertices, capeRenderVertices, clothParticles, simulatedParticles, pinnedParticles, skeletonBones;
            public float restAlignmentError;
        }
        [Serializable] sealed class AuthoredMaterial { public float[] base_color; }
        [Serializable] sealed class AuthoredMaterials { public AuthoredMaterial Superman_Cape; }

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "build_failure.txt"), ex.ToString());
                Debug.LogException(ex); EditorApplication.Exit(1);
            }
        }

        [MenuItem("Horror Game/Characters/Rebuild Superman and Cloth Review")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            var failure = Path.Combine(Output, "build_failure.txt");
            if (File.Exists(failure)) File.Delete(failure);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "Superman_PlayerRig.fbx");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.meshOptimizationFlags = 0;
            importer.generateSecondaryUV = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "Superman_Albedo.png");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.mipmapEnabled = false;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();

            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayer);
            var oldSkin = playerAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Skeleton);
            var originalSkin = original.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Superman_PlayerRig.fbx"));
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset);
            GameObject visual = null;
            try
            {
                var bodySource = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Superman_Body");
                var capeSource = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Superman_Cape");
                var attachmentSource = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Superman_CapeAttachment");
                var report = new BuildReport();
                var body = ConvertMesh(bodySource, originalSkin, "Superman_BodyMesh", report);
                var cape = ConvertMesh(capeSource, originalSkin, "Superman_CapeSurface", report, bodySource, true);
                var attachment = ConvertMesh(attachmentSource, originalSkin, "Superman_CapeAttachmentMesh", report, bodySource);
                Require(cape.uv2.Length == cape.vertexCount, "Cape cloth mask UV channel missing");
                var bodyMaterial = new Material(oldSkin.sharedMaterial) { name = "Superman_Body" };
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Superman_Albedo.png");
                SetTexture(bodyMaterial, texture);
                SetColor(bodyMaterial, Color.white);
                bodyMaterial = Save(bodyMaterial, Folder + "Superman_Body.mat");
                var capeMaterial = new Material(bodyMaterial) { name = "Superman_Cape" };
                SetTexture(capeMaterial, null);
                var authored = JsonUtility.FromJson<AuthoredMaterials>(File.ReadAllText(
                    Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSource/Characters/Superman/materials.json")));
                var rgba = authored.Superman_Cape.base_color;
                var color = new Color(rgba[0], rgba[1], rgba[2], rgba[3]).gamma;
                SetColor(capeMaterial, color);
                if (capeMaterial.HasProperty("_Cull")) capeMaterial.SetFloat("_Cull", 0);
                capeMaterial.doubleSidedGI = true;
                capeMaterial = Save(capeMaterial, Folder + "Superman_Cape.mat");
                player.name = "Superman_Player";
                Configure(player, originalSkin, body, cape, attachment, bodyMaterial, capeMaterial, report);
                var beanie = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Beanie");
                if (beanie) beanie.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(player, Folder + "Superman_Player.prefab");

                visual = (GameObject)PrefabUtility.InstantiatePrefab(original);
                visual.name = "Superman_Visual";
                var animator = visual.GetComponentInChildren<Animator>();
                var playerAnimator = player.GetComponentInChildren<Animator>();
                animator.avatar = playerAnimator.avatar;
                animator.runtimeAnimatorController = playerAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Configure(visual, originalSkin, body, cape, attachment, bodyMaterial, capeMaterial, report);
                PrefabUtility.SaveAsPrefabAsset(visual, Folder + "Superman_Visual.prefab");
                report.bodyVertices = body.vertexCount;
                report.attachmentVertices = attachment.vertexCount;
                report.capeRenderVertices = cape.vertexCount;
                report.skeletonBones = originalSkin.bones.Length;
                report.result = "PASS";
                File.WriteAllText(Path.Combine(Output, "build_report.json"), JsonUtility.ToJson(report, true));
            }
            finally
            {
                Object.DestroyImmediate(source); Object.DestroyImmediate(player);
                if (visual) Object.DestroyImmediate(visual);
            }
            MakeScene();
            AssetDatabase.SaveAssets();
            // These meshes were generated by the initial cape prototype. The final
            // prefabs use the single-surface chest-bound mesh instead.
            foreach (var obsolete in new[] { "Superman_CapeMesh.asset", "Superman_CapeClothMesh.asset" })
                if (AssetDatabase.LoadAssetAtPath<Mesh>(Folder + obsolete)) AssetDatabase.DeleteAsset(Folder + obsolete);
            Debug.Log("SUPERMAN_BUILD_PASS " + File.ReadAllText(Path.Combine(Output, "build_report.json")));
        }

        static Matrix4x4 Frame(Vector3 hip, Vector3 head, Vector3 left)
        {
            var up = (head - hip).normalized;
            var right = Vector3.ProjectOnPlane(left - hip, up).normalized;
            var forward = Vector3.Cross(right, up).normalized;
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(right.x, right.y, right.z, 0));
            m.SetColumn(1, new Vector4(up.x, up.y, up.z, 0));
            m.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0));
            m.SetColumn(3, new Vector4(hip.x, hip.y, hip.z, 1));
            return m;
        }

        static Mesh ConvertMesh(SkinnedMeshRenderer source, SkinnedMeshRenderer original, string name, BuildReport report, SkinnedMeshRenderer alignmentSource = null, bool staticCloth = false)
        {
            var sm = source.sharedMesh;
            if (!alignmentSource) alignmentSource = source;
            int O(string n) => Array.FindIndex(original.bones, b => b.name == n);
            int S(string n) => Array.FindIndex(alignmentSource.bones, b => b.name == n);
            Vector3 OP(string n) => original.sharedMesh.bindposes[O(n)].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 SP(string n) => alignmentSource.sharedMesh.bindposes[S(n)].inverse.MultiplyPoint3x4(Vector3.zero);
            var oh = OP("DEF-spine"); var sh = SP("DEF-spine");
            float scale = Vector3.Distance(oh, OP("DEF-head")) / Vector3.Distance(sh, SP("DEF-head"));
            var conversion = Frame(oh, OP("DEF-head"), OP("DEF-upper_arm.L")) * Matrix4x4.Scale(Vector3.one * scale) *
                             Frame(sh, SP("DEF-head"), SP("DEF-upper_arm.L")).inverse;
            float error = 0;
            foreach (var bone in source.bones)
                if (O(bone.name) >= 0)
                    error = Mathf.Max(error, Vector3.Distance(OP(bone.name), conversion.MultiplyPoint3x4(SP(bone.name))));
            Require(error < .0001f, "Player skeleton rest alignment changed: " + error);
            report.restAlignmentError = Mathf.Max(report.restAlignmentError, error);
            var mesh = Object.Instantiate(sm);
            mesh.name = name;
            mesh.vertices = sm.vertices.Select(v => conversion.MultiplyPoint3x4(v)).ToArray();
            var normalMatrix = conversion.inverse.transpose;
            mesh.normals = sm.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.tangents = sm.tangents.Select(t =>
            {
                var v = conversion.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                return new Vector4(v.x, v.y, v.z, t.w);
            }).ToArray();
            var map = source.bones.Select(b => O(b.name)).ToArray();
            int Remap(int index, float weight)
            {
                if (weight <= 0) return 0;
                Require(map[index] >= 0, "Unmapped weighted bone: " + source.bones[index].name);
                return map[index];
            }
            var weights = sm.boneWeights;
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                Require(Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .001f, "Unnormalized skin weights");
                w.boneIndex0 = Remap(w.boneIndex0, w.weight0); w.boneIndex1 = Remap(w.boneIndex1, w.weight1);
                w.boneIndex2 = Remap(w.boneIndex2, w.weight2); w.boneIndex3 = Remap(w.boneIndex3, w.weight3);
                weights[i] = w;
            }
            mesh.boneWeights = staticCloth ? new BoneWeight[0] : weights;
            mesh.bindposes = staticCloth ? new Matrix4x4[0] : original.sharedMesh.bindposes;
            mesh.RecalculateBounds();
            var positions = mesh.vertices;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                Require(Vector3.Cross(positions[triangles[i+1]] - positions[triangles[i]], positions[triangles[i+2]] - positions[triangles[i]]).sqrMagnitude > 1e-14f,
                    "Degenerate triangle in " + name);
            return Save(mesh, Folder + name + ".asset");
        }

        static void Configure(GameObject root, SkinnedMeshRenderer original, Mesh body, Mesh cape, Mesh attachment, Material bodyMat, Material capeMat, BuildReport report)
        {
            var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var bones = original.bones.Select(b => transforms.Single(t => t.name == b.name)).ToArray();
            renderer.sharedMesh = body;
            renderer.sharedMaterial = bodyMat;
            renderer.bones = bones;
            renderer.rootBone = transforms.Single(t => t.name == original.rootBone.name);
            renderer.quality = SkinQuality.Bone4;
            renderer.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(3, 3, 3));
            renderer.updateWhenOffscreen = true;
            var attachmentObject = new GameObject("Superman_CapeAttachment") { layer = renderer.gameObject.layer };
            attachmentObject.transform.SetParent(renderer.transform.parent, false);
            attachmentObject.transform.localPosition = renderer.transform.localPosition;
            attachmentObject.transform.localRotation = renderer.transform.localRotation;
            attachmentObject.transform.localScale = renderer.transform.localScale;
            var attachmentSkin = attachmentObject.AddComponent<SkinnedMeshRenderer>();
            attachmentSkin.sharedMesh = attachment; attachmentSkin.sharedMaterial = capeMat;
            attachmentSkin.bones = bones; attachmentSkin.rootBone = renderer.rootBone;
            attachmentSkin.quality = SkinQuality.Bone4; attachmentSkin.updateWhenOffscreen = true;
            attachmentSkin.localBounds = renderer.localBounds;
            attachmentSkin.shadowCastingMode = ShadowCastingMode.TwoSided;
            var go = new GameObject("Superman_Cape") { layer = renderer.gameObject.layer };
            go.transform.SetParent(renderer.transform.parent, false);
            go.transform.localPosition = renderer.transform.localPosition;
            go.transform.localRotation = renderer.transform.localRotation;
            go.transform.localScale = renderer.transform.localScale;
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = cape; skin.sharedMaterial = capeMat;
            int chestIndex = Array.FindIndex(bones, b => b.name == "DEF-spine.003");
            var chestBind = original.sharedMesh.bindposes[chestIndex];
            go.transform.SetParent(bones[chestIndex], false);
            go.transform.localPosition = chestBind.GetColumn(3);
            go.transform.localRotation = chestBind.rotation;
            go.transform.localScale = chestBind.lossyScale;
            skin.bones = new Transform[0]; skin.rootBone = null;
            skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true;
            skin.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(4, 4, 4));
            skin.shadowCastingMode = ShadowCastingMode.TwoSided;
            var posedMesh = new Mesh();
            skin.BakeMesh(posedMesh);
            var posedPoints = posedMesh.vertices;
            Object.DestroyImmediate(posedMesh);
            var cloth = go.AddComponent<Cloth>();
            cloth.useGravity = true;
            cloth.stretchingStiffness = 1;
            cloth.bendingStiffness = 0;
            cloth.damping = .01f;
            cloth.friction = .2f;
            cloth.worldVelocityScale = 1f;
            cloth.worldAccelerationScale = 1f;
            cloth.clothSolverFrequency = 180;
            cloth.useTethers = true;
            cloth.enableContinuousCollision = true;
            cloth.useVirtualParticles = 1f;
            cloth.collisionMassScale = .5f;
            cloth.randomAcceleration = new Vector3(.08f, .02f, .08f);
            cloth.sleepThreshold = 0;
            var vertices = cloth.vertices;
            var points = cape.vertices;
            var mask = cape.uv2;
            Require(vertices.Length > 0, "Cloth has no particles");
            var coefficients = new ClothSkinningCoefficient[vertices.Length];
            var pinned = points.Where((p, i) => mask[i].x <= 0).ToArray();
            Require(pinned.Length > 0 && mask.Any(w => w.x > 0), "Empty cloth or pinned author mask");
            int freeCount = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                // UV/normal splits are welded by native Cloth. Match the generated
                // particle positions in the chest-bound object's local frame.
                var particle = skin.transform.InverseTransformPoint(ClothToWorld(skin, vertices[i]));
                float best = float.PositiveInfinity;
                float weight = -1;
                for (int j = 0; j < points.Length; j++)
                {
                    float distance = (posedPoints[j] - particle).sqrMagnitude;
                    if (distance < best - 1e-12f) { best = distance; weight = mask[j].x; }
                    else if (distance < 1e-10f && best < 1e-10f)
                        Require(Mathf.Abs(weight - mask[j].x) < .001f, "Conflicting author mask on coincident vertices");
                }
                Require(best < 1e-8f, "Cloth particle cannot be matched to author mask: " + best);
                // The reference sheet pitches with the chest. A close motion sphere
                // or backstop would hold fabric in that tilted pose. Structural
                // constraints and tethers retain its length; body proxies stop skin
                // penetration while this generous envelope allows gravity to drape it.
                coefficients[i].maxDistance = weight > 0 ? 3f : 0;
                // Unity uses MaxValue to disable this constraint. Zero still
                // enables a backstop on the rotating reference surface.
                coefficients[i].collisionSphereDistance = float.MaxValue;
                if (weight > 0) freeCount++;
            }
            cloth.coefficients = coefficients;
            report.clothParticles = vertices.Length;
            report.simulatedParticles = freeCount;
            report.pinnedParticles = vertices.Length - freeCount;
            var collisions = new List<CapsuleCollider>();
            Transform Bone(string n) => transforms.Single(t => t.name == n);
            void Capsule(string name, string a, string b, float radius)
            {
                var parent = Bone(a); var end = Bone(b).position;
                var colliderObject = new GameObject(name) { layer = 2 };
                colliderObject.transform.position = parent.position;
                colliderObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - parent.position);
                colliderObject.transform.SetParent(parent, true);
                var capsule = colliderObject.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                float length = Vector3.Distance(parent.position, end);
                capsule.height = length + radius * 2;
                capsule.center = Vector3.up * (length * .5f);
                // Trigger proxies on Ignore Raycast provide cloth collision without solid gameplay contacts.
                capsule.isTrigger = true;
                collisions.Add(capsule);
            }
            Capsule("CapeCollision_Chest", "DEF-spine.003", "DEF-neck", .19f);
            Capsule("CapeCollision_Pelvis", "DEF-spine", "DEF-spine.002", .19f);
            foreach (var side in new[] { "L", "R" })
            {
                Capsule("CapeCollision_Thigh_" + side, "DEF-thigh." + side, "DEF-shin." + side, .13f);
                Capsule("CapeCollision_Shin_" + side, "DEF-shin." + side, "DEF-foot." + side, .105f);
                Capsule("CapeCollision_Foot_" + side, "DEF-foot." + side, "DEF-toe." + side, .13f);
                Capsule("CapeCollision_UpperArm_" + side, "DEF-upper_arm." + side, "DEF-forearm." + side, .13f);
                Capsule("CapeCollision_Forearm_" + side, "DEF-forearm." + side, "DEF-hand." + side, .10f);
            }
            cloth.capsuleColliders = collisions.ToArray();
            go.AddComponent<SupermanCape>().SetMovementRoot(root.transform);
        }

        static void MakeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Superman_Visual.prefab"));
            var camera = new GameObject("Review Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(3, 1.8f, -3);
            camera.transform.LookAt(Vector3.up * 1.1f); camera.fieldOfView = 36;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .09f, .11f);
            camera.nearClipPlane = .05f;
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Review Floor"; floor.transform.localScale = Vector3.one * 2;
            var showcase = new GameObject("Cloth Review").AddComponent<SupermanShowcase>();
            showcase.animator = character.GetComponentInChildren<Animator>();
            showcase.character = character.transform; showcase.reviewCamera = camera;
            var clips = showcase.animator.runtimeAnimatorController.animationClips.Concat(
                AssetDatabase.FindAssets("t:AnimatorOverrideController").Select(g => AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(c => c).SelectMany(c => c.animationClips)).Distinct().Where(c => c && !AssetDatabase.GetAssetPath(c).Contains("/Empty/")).ToArray();
            var selected = new List<AnimationClip>();
            foreach (var term in new[] { "idle", "walk", "run", "crouch", "gunready", "shoot", "knife" })
            {
                var clip = clips.FirstOrDefault(c => c.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                if (clip && !selected.Contains(clip)) selected.Add(clip);
            }
            showcase.clips = selected.Count > 0 ? selected.ToArray() : clips.Take(5).ToArray();
            Require(showcase.clips.Length > 0, "No existing animation clips found");
            EditorSceneManager.SaveScene(scene, Scene);
        }

        static void SetTexture(Material material, Texture texture)
        {
            foreach (var key in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
                if (material.HasProperty(key)) material.SetTexture(key, texture);
        }

        static void SetColor(Material material, Color color)
        {
            foreach (var key in new[] { "_BaseColor", "_Color" })
                if (material.HasProperty(key)) material.SetColor(key, color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
        }

        static T Save<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing)
            {
                // Clear GPU buffers before replacing their serialized layout.
                // CopySerialized alone can leave the old draw buffers until reload.
                if (existing is Mesh oldMesh) oldMesh.Clear(false);
                EditorUtility.CopySerialized(value, existing);
                if (existing is Mesh mesh) mesh.UploadMeshData(false);
                Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing;
            }
            AssetDatabase.CreateAsset(value, path); return value;
        }

        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static Vector3 ClothToWorld(SkinnedMeshRenderer skin, Vector3 particle)
        {
            return skin.rootBone ? skin.rootBone.position + skin.rootBone.rotation * particle : skin.transform.TransformPoint(particle);
        }
    }
}
