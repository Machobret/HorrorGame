using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    /// <summary>Builds the standard fixed-stock MP5 without rebuilding any existing weapon.</summary>
    public static class MP5AssetBuilder
    {
        const string Root = "Assets/Game/Weapons/";
        const string Examples = "Assets/AssetStore/HorrorEngine/ExampleContent/";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static T Required<T>(string path) where T : Object => Load<T>(path) ? Load<T>(path) : throw new Exception("Missing MP5 input: " + path);

        static void Set(Object target, string path, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(path);
            if (property == null) throw new Exception(target.name + " missing " + path);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Float(Object target, string path, float value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(path);
            if (property == null) throw new Exception(target.name + " missing " + path);
            property.floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static T Copy<T>(T source, string path) where T : Object
        {
            var existing = Load<T>(path); if (existing) return existing;
            var result = Object.Instantiate(source); result.name = Path.GetFileNameWithoutExtension(path);
            if (result is Register register) { register.UniqueId = ""; register.GenerateId(); }
            AssetDatabase.CreateAsset(result, path); return result;
        }
        static void Name(ItemData item, string name, string description)
        {
            item.Name = new LocalizableText { IsLocalized = false, Unlocalized = name };
            item.Description = new LocalizableText { IsLocalized = false, Unlocalized = description };
            item.Name_DEPRECATED = item.Description_DEPRECATED = ""; EditorUtility.SetDirty(item);
        }

        [MenuItem("Horror Game/Weapons/Build Standard MP5")]
        public static void Build()
        {
            foreach (string folder in new[] { "Data", "Prefabs", "Materials", "Animation" }) Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureModel();
            var pistol = Required<ReloadableWeaponData>(Examples + "Weapons/Gun/Gun.asset");
            var sourceData = Required<ReloadableWeaponData>(Examples + "Weapons/Shotgun/Shotgun.asset");
            var ammo = Copy(Required<AmmoData>(Examples + "Items/GunAmmo/GunAmmo.asset"), Root + "Data/SMGAmmo.asset");
            Name(ammo, "SMG rounds", "9 mm standard rounds reserved for the MP5. Blue ammunition carton.");
            ammo.MaxStackSize = 120;
            var data = Copy(sourceData, Root + "Data/MP5.asset");
            Name(data, "MP5", "A standard fixed-stock 9 mm submachine gun. Thirty-round magazine, automatic fire.");
            data.MaxAmmo = 30; data.AmmoItem = ammo; data.ReloadDuration = 3; data.AllowMovementDuringReload = true;
            // The source pack has no audio. Explicitly use the existing game pistol report as a fallback.
            data.ShotSound = Load<AudioClip>(Root + "Audio/MP5_Shot.wav") ?? pistol.ShotSound;
            data.ReloadSound = Load<AudioClip>(Root + "Audio/MP5_Reload.wav") ?? pistol.ReloadSound;
            ConfigureOverride(sourceData, data);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(Examples + "Weapons/Shotgun/Shotgun.prefab"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "MP5";
                foreach (var component in root.GetComponents<MonoBehaviour>().Where(c => c &&
                    (c is LongGunSupportPose || c is WeaponAmmoVisuals || c is IWeaponReloadPresentation ||
                     c.GetType().Name == "RifleBoltVisual" || c.GetType().Name == "HandgunSupportPose" ||
                     c.GetType().Name == "MP5HandlingPose")).ToArray()) Object.DestroyImmediate(component);
                var weapon = root.GetComponent<Weapon>(); weapon.WeaponData = data;
                var sockets = root.GetComponentsInChildren<Socket>();
                var muzzle = sockets.First(s => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s.Handle)) == "d098dc4e8344d624c9a27847799359a5");
                // Retain the project's weapon-root coordinate convention; imported model is +Z forward.
                var orientation = Quaternion.LookRotation(root.transform.InverseTransformDirection(muzzle.transform.forward), root.transform.InverseTransformDirection(muzzle.transform.up));
                foreach (var socket in sockets) socket.transform.SetParent(root.transform, true);
                Object.DestroyImmediate(weapon.Visuals);
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(root.transform, false); visuals.transform.localRotation = orientation; visuals.transform.localPosition = orientation * new Vector3(0, 0, -.11f);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(Root + "Models/MP5/MP5.obj"), visuals.transform);
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); weapon.Visuals = visuals;
                foreach (var socket in sockets)
                {
                    socket.transform.SetParent(visuals.transform, false); socket.transform.localScale = Vector3.one;
                    socket.transform.localPosition = socket == muzzle ? new Vector3(0, .081496f, .382399f) : new Vector3(.02f, .07f, .14f);
                    socket.transform.localRotation = socket == muzzle ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                }
                var shot = root.GetComponent<ShotAttack>();
                var sourceAttack = (AttackType)new SerializedObject(shot).FindProperty("m_Attack").objectReferenceValue;
                var attack = Copy(sourceAttack, Root + "Data/Attack_MP5.asset");
                attack.Impacts = sourceAttack.Impacts.SelectMany(impact => impact.Damageable.Select(type => new AttackImpact
                { Damage = type && type.name.Contains("Weakpoint") ? 1.5f : 1f, Damageable = new[] { type }, Filters = impact.Filters,
                  PreDamageEffects = impact.PreDamageEffects, PostDamageEffects = impact.PostDamageEffects })).ToArray();
                EditorUtility.SetDirty(attack);
                Set(shot, "m_Attack", attack); Float(shot, "m_CastRadius", .025f); Float(shot, "m_MaxRange", 28);
                Float(shot, "m_Width", 0); Float(shot, "m_ForwardOffset", 0); Float(shot, "m_VerticalityFactor", 1);
                var shotSerialized = new SerializedObject(shot); shotSerialized.FindProperty("m_PenetrationHits").intValue = 1; shotSerialized.ApplyModifiedPropertiesWithoutUndo();
                // Replace the shotgun shell emitter with the pistol's brass emitter.
                var pistolRoot = Required<GameObject>(Examples + "Weapons/Gun/Gun.prefab");
                var pistolShot = pistolRoot.GetComponent<ShotAttack>();
                var pistolEmitter = (ObjectInstantiator)new SerializedObject(pistolShot).FindProperty("m_ShellKickInstantiator").objectReferenceValue;
                var emitter = (ObjectInstantiator)new SerializedObject(shot).FindProperty("m_ShellKickInstantiator").objectReferenceValue;
                if (emitter && pistolEmitter) EditorUtility.CopySerialized(pistolEmitter, emitter);
                var pistolMuzzle = (ObjectInstantiator)new SerializedObject(pistolShot).FindProperty("m_MuzzleInstantiator").objectReferenceValue;
                var muzzleEmitter = (ObjectInstantiator)new SerializedObject(shot).FindProperty("m_MuzzleInstantiator").objectReferenceValue;
                if (muzzleEmitter && pistolMuzzle) EditorUtility.CopySerialized(pistolMuzzle, muzzleEmitter);
                Float(shot, "m_ShellKickDelay", 0);
                var montage = root.GetComponent<AttackMontage>(); montage.Attack = shot; montage.CountConsumption = 1;
                montage.Duration = .12f; montage.MontageDelay = 0; montage.AttackActivationDelay = 0; montage.AttackDuration = 0;
                montage.AllowMovementDuringAttack = true; montage.Animation = Required<AnimatorStateHandle>(Root + "Animation/AnimState_MobileAttack.asset");
                montage.AnimationBlendTime = .045f;
                var rate = root.GetComponent<AttackRate>() ?? root.AddComponent<AttackRate>();
                rate.CanBeHeld = true; rate.MaxAttackCount = 0; rate.AttacksPerSecond = 800f / 60f;
                if (!root.GetComponent<WeaponShotAudio>()) root.AddComponent<WeaponShotAudio>();
                root.GetComponent<AudioSource>().spatialBlend = 0;
                AddPresentation(root, model);
                data.EquipPrefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/MP5.prefab");
                data.ExamineModel = Examination(model, "MP5");
                SaveModelMeasurements(model);
            }
            finally { Object.DestroyImmediate(root); }
            data.Image = Load<Sprite>(Root + "Models/MP5/MP5_Icon.png") ?? pistol.Image;
            var weaponPickup = Pickup(data, data.EquipPrefab.GetComponent<Weapon>().Visuals, "MP5", 1, 30);
            // These placeholders retain working interaction until the six-face packaging pipeline replaces them.
            var ammoSource = Required<GameObject>(Root + "Prefabs/PistolAmmo_Pickup.prefab");
            var ammoPickup = CloneAmmoPickup(ammoSource, ammo);
            if (!Load<GameObject>(Root + "Prefabs/SMGAmmo_Examination.prefab"))
            {
                var exam = Object.Instantiate(Required<GameObject>(Examples + "Items/GunAmmo/GunAmmoExamination.prefab"));
                try { exam.name = "SMGAmmo_Examination"; ammo.ExamineModel = PrefabUtility.SaveAsPrefabAsset(exam, Root + "Prefabs/SMGAmmo_Examination.prefab"); }
                finally { Object.DestroyImmediate(exam); }
            }
            else ammo.ExamineModel = Load<GameObject>(Root + "Prefabs/SMGAmmo_Examination.prefab");
            AppendRack(weaponPickup, ammoPickup);
            var database = Required<ItemDatabase>(Examples + "Databases/ItemDB.asset");
            foreach (var item in new ItemData[] { data, ammo }) { if (!database.Registers.Contains(item)) database.Registers.Add(item); EditorUtility.SetDirty(item); }
            EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
            Debug.Log("MP5_BUILD_OK standard fixed stock, 30 rounds, 800rpm automatic, manual 3-second walking reload; existing weapons preserved");
        }

        static void ConfigureModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "Models/MP5/MP5.obj");
            if (!importer) throw new Exception("Prepare standard MP5.obj first");
            importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false; importer.isReadable = true; importer.preserveHierarchy = true; importer.SaveAndReimport();
            string texturePath = Root + "Models/MP5/MP5_Albedo.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (!textureImporter) throw new Exception("Missing MP5 albedo");
            textureImporter.filterMode = FilterMode.Point; textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.maxTextureSize = 256; textureImporter.SaveAndReimport();
            string path = Root + "Materials/MP5.mat"; var material = Load<Material>(path);
            if (!material) { material = new Material(Shader.Find("HorrorEngine/UnlitAlphaCutout")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_MainTex", Required<Texture2D>(texturePath)); material.SetColor("_Color", Color.white); material.SetFloat("_Cull", 0);
            var names = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Material>().Select(m => m.name)
                .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct();
            foreach (var name in names) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            EditorUtility.SetDirty(material); importer.SaveAndReimport();
        }
        static void ConfigureOverride(ReloadableWeaponData source, ReloadableWeaponData destination)
        {
            var sourceOverride = (AnimatorOverrideController)new SerializedObject(source).FindProperty("AnimatorOverride.m_Value").objectReferenceValue;
            var controller = Copy(sourceOverride, Root + "Animation/MP5Override.overrideController");
            // The template's AssaultRifle reuses the shotgun's directional shooting
            // clips. Keep only their initial recoil, with recovery before the next
            // 800 rpm shot; never play the later long-gun loading/pumping motion.
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); controller.GetOverrides(pairs);
            var assault = Required<AnimatorOverrideController>(Examples + "Weapons/AssaultRifle/AssaultRifleOverride.overrideController");
            var assaultPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); assault.GetOverrides(assaultPairs);
            for (int i = 0; i < pairs.Count; ++i)
            {
                string key = pairs[i].Key.name;
                if (key == "WeaponAttack" || key == "WeaponAttackUp" || key == "WeaponAttackDown")
                {
                    var recoilSource = assaultPairs.FirstOrDefault(p => p.Key && p.Key.name == key).Value;
                    if (!recoilSource) throw new Exception("Missing directional template recoil: " + key);
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, RecoilClip(recoilSource, key));
                }
            }
            controller.ApplyOverrides(pairs);
            Set(destination, "AnimatorOverride.m_Value", controller); EditorUtility.SetDirty(controller);
        }
        static AnimationClip RecoilClip(AnimationClip source, string direction)
        {
            string path = Root + "Animation/MP5_" + direction + ".anim";
            var clip = Load<AnimationClip>(path);
            if (clip) EditorUtility.CopySerialized(source, clip);
            else { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            clip.name = "MP5_" + direction; clip.frameRate = 60;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var original = AnimationUtility.GetEditorCurve(source, binding);
                if (original == null || original.length == 0) continue;
                // Compact the initial 60ms of authored recoil into 33ms. Smoothly
                // recover to the original pose at 70ms, before the next 75ms shot.
                // Matching pose and zero tangents at both boundaries prevent a
                // visible jump when automatic fire restarts this clip.
                var keys = new List<Keyframe> { new Keyframe(0, original.Evaluate(0), 0, 0) };
                foreach (var key in original.keys)
                    if (key.time > .0001f && key.time < .0599f) keys.Add(new Keyframe(key.time * .55f, key.value, key.inTangent / .55f, key.outTangent / .55f));
                keys.Add(new Keyframe(.033f, original.Evaluate(.06f), 0, 0));
                keys.Add(new Keyframe(.070f, original.Evaluate(0), 0, 0));
                keys.Add(new Keyframe(.120f, original.Evaluate(0), 0, 0));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.startTime = 0; settings.stopTime = .12f; settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip); return clip;
        }
        static void AddPresentation(GameObject root, GameObject model)
        {
            var magazine = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "MP5Magazene" || t.name == "Magazine");
            if (!magazine) throw new Exception("Standard MP5 model is missing independently animated MP5Magazene. Imported hierarchy: " + string.Join(", ", model.GetComponentsInChildren<Transform>().Select(t => t.name)));
            AddRuntime(root, "MP5ReloadVisual", component => { Set(component, "ModelFrame", model.transform); Set(component, "Magazine", magazine); });
            AddRuntime(root, "MP5HandlingPose", component =>
            {
                Set(component, "ModelFrame", model.transform);
                var serialized = new SerializedObject(component);
                serialized.FindProperty("ForeEndWrist").vector3Value = new Vector3(-.055f, .005f, .175f);
                serialized.FindProperty("WristRotation").quaternionValue = new Quaternion(.09277141f, -.14839445f, -.81235290f, .55628824f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }
        static void AddRuntime(GameObject root, string name, Action<Component> configure)
        {
            var type = typeof(Weapon).Assembly.GetType("HorrorGame.Weapons." + name);
            if (type == null) throw new Exception("Compile " + name + " before building MP5");
            configure(root.AddComponent(type));
        }
        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) throw new Exception("MP5 missing visible mesh");
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static GameObject Examination(GameObject source, string name)
        {
            var root = new GameObject(name + "_Examination");
            try
            {
                var model = Object.Instantiate(source, root.transform); model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var bounds = BoundsOf(model); model.transform.position -= bounds.center;
                foreach (var transform in root.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 5;
                var utility = root.AddComponent<UIExamineItemUtility>(); utility.Visuals = model;
                var serialized = new SerializedObject(utility); serialized.FindProperty("m_ExpectedMask").intValue = 32; serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + "_Examination.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject Pickup(ItemData item, GameObject source, string name, int count, int loaded)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/PickupItem.prefab"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); root.name = name + "_Pickup";
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) Object.DestroyImmediate(renderer.gameObject);
                var visual = Object.Instantiate(source, root.transform); visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var socket in visual.GetComponentsInChildren<Socket>()) Object.DestroyImmediate(socket.gameObject);
                foreach (var transform in visual.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 0;
                var bounds = BoundsOf(visual); visual.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                var interactive = root.GetComponentInChildren<Interactive>(); interactive.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var collider = interactive.GetComponent<BoxCollider>(); collider.isTrigger = true; collider.size = Vector3.Max(bounds.size, new Vector3(.5f, 1, .5f)); collider.center = Vector3.up * collider.size.y * .5f;
                interactive.GetComponent<Lookable>().Offset = collider.center;
                root.GetComponent<PickupItem>().Entry = new InventoryEntry { Item = item, Count = count, SecondaryCount = loaded };
                root.GetComponent<ObjectUniqueId>().RegenerateId();
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + "_Pickup.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject CloneAmmoPickup(GameObject source, AmmoData ammo)
        {
            string path = Root + "Prefabs/SMGAmmo_Pickup.prefab";
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path) ?? source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); root.name = "SMGAmmo_Pickup";
                root.GetComponent<PickupItem>().Entry = new InventoryEntry { Item = ammo, Count = 30, SecondaryCount = 0 };
                root.GetComponent<ObjectUniqueId>().RegenerateId(); return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void AppendRack(params GameObject[] additions)
        {
            string path = Root + "Prefabs/Weapon_TestPickups.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = 0; i < additions.Length; ++i)
                {
                    var child = root.transform.Find(additions[i].name);
                    if (!child) { child = ((GameObject)PrefabUtility.InstantiatePrefab(additions[i], root.transform)).transform; child.localPosition = new Vector3(6.25f, 0, i * 1.25f); }
                }
                // Rack children are distinct instances, including their nested prefab source IDs.
                var seen = new HashSet<string>();
                foreach (var id in root.GetComponentsInChildren<ObjectUniqueId>(true))
                {
                    if (!seen.Add(id.Id) || !id.IsUniqueInstance) { id.RegenerateId(); seen.Add(id.Id); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [Serializable] sealed class ModelReport { public string Name; public Vector3 BoundsSize; public List<string> Meshes = new List<string>(); }
        static void SaveModelMeasurements(GameObject model)
        {
            var report = new ModelReport { Name = "Standard fixed-stock MP5", BoundsSize = BoundsOf(model).size };
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>()) report.Meshes.Add(filter.name + ": " + filter.sharedMesh.vertexCount + " vertices, local bounds " + filter.sharedMesh.bounds);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSource/Weapons/MP5"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "ModelMeasurements.json"), JsonUtility.ToJson(report, true));
        }
        public static void RefreshIcon()
        {
            var data = Required<ReloadableWeaponData>(Root + "Data/MP5.asset");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "Models/MP5/MP5_Icon.png");
            if (!importer) throw new Exception("Render MP5_Icon.png first");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.maxTextureSize = 1024; importer.filterMode = FilterMode.Bilinear; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            data.Image = Required<Sprite>(importer.assetPath); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        }
        public static void RunBatch() { try { Build(); EditorApplication.Exit(0); } catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); } }
    }
}
