using System;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    public static class WeaponAssetBuilder
    {
        const string Root = "Assets/Game/Weapons/";
        const string Examples = "Assets/AssetStore/HorrorEngine/ExampleContent/";
        static bool runPlayValidation;
        public static void BuildAndTest()
        {
            runPlayValidation = true;
            Build();
            WeaponPlayValidation.RunBatch();
        }
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        [MenuItem("Horror Game/Weapons/Build Revolver")]
        public static void BuildRevolver()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "Revolver", "RevolverAmmo" })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Models/" + name }))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    bool icon = importer.assetPath.Contains("_Icon");
                    importer.filterMode = icon ? FilterMode.Bilinear : FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = icon ? (name == "Revolver" ? 1024 : 512) : 256;
                    if (icon) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; }
                    importer.SaveAndReimport();
                }
                ConfigureModel(name);
            }
            var ammo = Copy(Load<AmmoData>(Examples + "Items/GunAmmo/GunAmmo.asset"), Root + "Data/RevolverAmmo.asset");
            ConfigureItem(ammo, ".357 Magnum rounds", "Rare ammunition for the six-shot revolver.", "RevolverAmmo"); ammo.MaxStackSize = 18;
            var gun = BuildWeapon("Revolver", "Gun", ammo, 6, 1.1f, 2.35f, 4f);
            var pickups = new[] { Pickup(gun, "Revolver", 1, 6), Pickup(ammo, "RevolverAmmo", 3, 0) };
            string rackPath = Root + "Prefabs/Weapon_TestPickups.prefab";
            var rack = PrefabUtility.LoadPrefabContents(rackPath);
            try
            {
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (rack.transform.Find(pickups[i].name)) continue;
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(pickups[i], rack.transform);
                    child.transform.localPosition = new Vector3(5, 0, i * 1.25f);
                }
                PrefabUtility.SaveAsPrefabAsset(rack, rackPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rack); }
            var db = Load<ItemDatabase>(Examples + "Databases/ItemDB.asset");
            foreach (var item in new ItemData[] { gun, ammo }) if (!db.Registers.Contains(item)) db.Registers.Add(item);
            EditorUtility.SetDirty(db);
            var flareRoot = PrefabUtility.LoadPrefabContents(Root + "Prefabs/FlareGun.prefab");
            flareRoot.GetComponent<FlareGunAttack>().LaunchSpeed = 11;
            PrefabUtility.SaveAsPrefabAsset(flareRoot, Root + "Prefabs/FlareGun.prefab"); PrefabUtility.UnloadPrefabContents(flareRoot);
            Projectile(Load<AttackType>(Root + "Data/Attack_FlareGun.asset"));
            AssetDatabase.SaveAssets(); Validate(); Debug.Log("REVOLVER_BUILD_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        static void Set(Object obj, string property, object value)
        {
            var so = new SerializedObject(obj); var p = so.FindProperty(property);
            if (p == null) throw new Exception(obj.name + " missing " + property);
            if (value is Object reference) p.objectReferenceValue = reference;
            else if (value == null) p.objectReferenceValue = null;
            else if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i;
            else if (value is string s) p.stringValue = s;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static T Copy<T>(T source, string path) where T : Object
        {
            var existing = Load<T>(path);
            if (existing) return existing;
            var copy = Object.Instantiate(source); copy.name = Path.GetFileNameWithoutExtension(path);
            if (copy is Register register) { register.UniqueId = ""; register.GenerateId(); }
            AssetDatabase.CreateAsset(copy, path); return copy;
        }
        [MenuItem("Horror Game/Weapons/Build Rifle and Flare Prototype")]
        public static void Build()
        {
            foreach (string folder in new[] { "Data", "Prefabs", "Materials", "Effects" }) Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = path.Contains("HuntingRifleText") ? 512 : 256;
                if (path.Contains("_Icon")) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear; }
                if (path.Contains("_Icon")) importer.maxTextureSize = path.Contains("Ammo") ? 512 : 1024;
                importer.SaveAndReimport();
            }
            ConfigureModel("HuntingRifle"); ConfigureModel("FlareGun"); ConfigureModel("FlareAmmo"); ConfigureModel("RifleAmmo");
            var ammoTemplate = Load<AmmoData>(Examples + "Items/GunAmmo/GunAmmo.asset");
            var rifleAmmo = Copy(ammoTemplate, Root + "Data/HuntingRifleAmmo.asset");
            ConfigureItem(rifleAmmo, ".300 Win Mag rounds", "Bottleneck rifle cartridges for the hunting rifle.", "RifleAmmo"); rifleAmmo.MaxStackSize = 30;
            var flareAmmo = Copy(ammoTemplate, Root + "Data/FlareAmmo.asset");
            ConfigureItem(flareAmmo, "Red signal flares", "Red aerial signal cartridges for the flare gun.", "FlareAmmo"); flareAmmo.MaxStackSize = 10;
            var rifle = BuildWeapon("HuntingRifle", "Shotgun", rifleAmmo, 5, 1.4f, 1.3f, 2);
            var flare = BuildWeapon("FlareGun", "Gun", flareAmmo, 1, .8f, 2.3f, .25f);
            var riflePickup = Pickup(rifle, "HuntingRifle", 1, 5);
            var flarePickup = Pickup(flare, "FlareGun", 1, 1);
            var roundsPickup = Pickup(rifleAmmo, "RifleAmmo", 5, 0);
            var flareRoundsPickup = Pickup(flareAmmo, "FlareAmmo", 3, 0);
            var pistolPickup = ExistingWeaponPickup("Gun", "Pistol");
            var shotgunPickup = ExistingWeaponPickup("Shotgun", "Shotgun");
            var pistolAmmoPickup = ExistingAmmoPickup("PistolAmmo", "PickupGunAmmo", 15);
            var shotgunAmmoPickup = ExistingAmmoPickup("ShotgunAmmo", "PickupShotgunShells", 4);
            var rack = new GameObject("Weapon_TestPickups");
            var prefabs = new[] { pistolPickup, shotgunPickup, riflePickup, flarePickup, pistolAmmoPickup, shotgunAmmoPickup, roundsPickup, flareRoundsPickup };
            for (int i = 0; i < prefabs.Length; ++i)
            { var child = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], rack.transform); child.transform.localPosition = i < 4 ? new Vector3(i * 1.25f, 0, 0) : new Vector3((i - 4) * 1.25f, 0, 1.25f); }
            PrefabUtility.SaveAsPrefabAsset(rack, Root + "Prefabs/Weapon_TestPickups.prefab"); Object.DestroyImmediate(rack);
            var db = Load<ItemDatabase>(Examples + "Databases/ItemDB.asset");
            foreach (var item in new ItemData[] { rifle, flare, rifleAmmo, flareAmmo })
            { if (!db.Registers.Contains(item)) db.Registers.Add(item); EditorUtility.SetDirty(item); }
            EditorUtility.SetDirty(db); AssetDatabase.SaveAssets();
            Validate(); Debug.Log("WEAPONS_BUILD_OK");
            if (Application.isBatchMode && !runPlayValidation) EditorApplication.Exit(0);
        }
        static void ConfigureModel(string name)
        {
            string folder = Root + "Models/" + name + "/";
            var model = (ModelImporter)AssetImporter.GetAtPath(folder + name + ".fbx");
            model.globalScale = 1; model.useFileScale = true; model.importAnimation = false; model.isReadable = true;
            model.SaveAndReimport();
            var assets = AssetDatabase.LoadAllAssetsAtPath(folder + name + ".fbx");
            var materialNames = assets.OfType<Material>().Select(m => m.name)
                .Concat(model.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct().ToArray();
            foreach (var materialName in materialNames)
            {
                string texture = name == "Revolver" ? (materialName.Contains("034") ? "cylinderRefV2.jpg" : "ColtPython.png") : name == "RevolverAmmo" ? "RevolverAmmo_Albedo.png" : name == "HuntingRifle" ? (materialName.Contains("067") ? "ScopeText.jpg" : "HuntingRifleText.png") :
                    name == "FlareAmmo" ? "FlareAmmo_Albedo.png" : name == "FlareGun" ? "Flaregun_Text_240.png" : "RifleAmmoText_240.png";
                string path = Root + "Materials/" + name + "_" + materialName.Replace('.', '_') + ".mat";
                var mat = Load<Material>(path);
                var shader = Shader.Find(name == "RifleAmmo" ? "Universal Render Pipeline/Lit" : "HorrorEngine/UnlitAlphaCutout");
                if (!shader) throw new Exception("Missing weapon shader for " + name);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                var albedo = Load<Texture2D>(folder + texture);
                if (!albedo) throw new Exception("Missing weapon texture: " + folder + texture);
                mat.SetTexture("_MainTex", albedo); mat.SetColor("_Color", Color.white);
                if (name == "RevolverAmmo" && materialName == "MagnumCarton")
                {
                    mat.SetTexture("_MainTex", Load<Texture2D>(folder + "RevolverAmmo_Carton.png"));
                    mat.SetFloat("_Cutoff", 0);
                    mat.SetColor("_Color", new Color(.44f, .30f, .21f));
                }
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .05f);
                if (name != "RifleAmmo") mat.SetFloat("_Cull", 0);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
                model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName), mat);
            }
            model.SaveAndReimport();
        }
        static void ConfigureItem(ItemData item, string name, string description, string model)
        {
            item.Name = new LocalizableText { IsLocalized = false, Unlocalized = name };
            item.Description = new LocalizableText { IsLocalized = false, Unlocalized = description };
            item.Name_DEPRECATED = item.Description_DEPRECATED = "";
            item.Image = Load<Sprite>(Root + "Models/" + model + "/" + model + (model == "RevolverAmmo" ? "_PickupIcon.png" : "_Icon.png"));
            if (item is AmmoData)
            {
                string ammoKey = model == "RifleAmmo" ? "HuntingRifleAmmo" : model;
                string revisedPath = ammoKey == "FlareAmmo" ? Root + "Models/FlareAmmo/FlareAmmo_Icon.png" : Root + "Models/AmmoSides/" + ammoKey + "_PickupIcon.png";
                var revisedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(revisedPath);
                if (revisedIcon) item.Image = revisedIcon;
                var revisedDrop = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + ammoKey + "_Drop.prefab");
                if (revisedDrop) item.DropPrefab = revisedDrop.GetComponent<SpawnableSavable>();
            }
            item.ExamineModel = Examination(item.name, model);
            EditorUtility.SetDirty(item);
        }
        static GameObject Model(string name, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root + "Models/" + name + "/" + name + ".fbx"), parent);
            // FBX import can put its centimetre-to-metre correction on the
            // root transform (100 for the ammo models). Retain that scale.
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (!AmmoPackagingBuilder.ApplyToModel(go, name)) AmmoBoxSideTextureBuilder.ApplyToModel(go, name); return go;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static GameObject Examination(string name, string model)
        {
            var root = new GameObject(name + "_Examination");
            var visual = Model(model, root.transform); var b = BoundsOf(visual);
            visual.transform.position -= b.center;
            if (model == "RevolverAmmo") visual.transform.localScale *= 8;
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 5;
            var utility = root.AddComponent<UIExamineItemUtility>(); utility.Visuals = visual; Set(utility, "m_ExpectedMask", 32);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + "_Examination.prefab");
            Object.DestroyImmediate(root); return saved;
        }
        static AttackType Attack(AttackType source, string name, float damage)
        {
            var result = Copy(source, Root + "Data/Attack_" + name + ".asset");
            result.Impacts = source.Impacts.SelectMany(impact => impact.Damageable.Select(type => new AttackImpact
            {
                Damage = damage * (type && type.name.Contains("Weakpoint") ? 1.5f : 1),
                Damageable = new[] { type }, Filters = impact.Filters,
                PreDamageEffects = name == "FlareGun" ? new AttackEffectEntry[0] : impact.PreDamageEffects,
                PostDamageEffects = name == "FlareGun" ? new AttackEffectEntry[0] : impact.PostDamageEffects
            })).ToArray();
            EditorUtility.SetDirty(result); return result;
        }
        static ReloadableWeaponData BuildWeapon(string name, string template, AmmoData ammo, int magazine, float duration, float reload, float damage)
        {
            var data = Copy(Load<ReloadableWeaponData>(Examples + "Weapons/" + template + "/" + template + ".asset"), Root + "Data/" + name + ".asset");
            ConfigureItem(data, name == "Revolver" ? "Magnum revolver" : name == "HuntingRifle" ? "Hunting rifle" : "Flare gun", name == "Revolver" ? "A powerful six-shot .357 revolver. Fires on a trigger press." : name == "HuntingRifle" ? "A slow, powerful hunting rifle. Five-round magazine." : "A single-shot signal gun. Flares illuminate surfaces and burn enemies on contact.", name);
            data.MaxAmmo = magazine; data.AmmoItem = ammo; data.ReloadDuration = reload;
            data.AllowMovementDuringReload = name != "HuntingRifle";
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Examples + "Weapons/" + template + "/" + template + ".prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); root.name = name;
            var weapon = root.GetComponent<Weapon>(); weapon.WeaponData = data;
            var sockets = root.GetComponentsInChildren<Socket>();
            var muzzle = sockets.First(s => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s.Handle)) == "d098dc4e8344d624c9a27847799359a5");
            Quaternion orientation = Quaternion.LookRotation(root.transform.InverseTransformDirection(muzzle.transform.forward), root.transform.InverseTransformDirection(muzzle.transform.up));
            foreach (var socket in sockets) socket.transform.SetParent(root.transform, true);
            Object.DestroyImmediate(weapon.Visuals);
            var holder = new GameObject("Visuals"); holder.transform.SetParent(root.transform, false); holder.transform.localRotation = orientation;
            var model = Model(name, holder.transform); weapon.Visuals = holder;
            foreach (var socket in sockets)
            {
                socket.transform.SetParent(holder.transform, false); socket.transform.localScale = Vector3.one;
                socket.transform.localPosition = socket == muzzle ? (name == "Revolver" ? new Vector3(0, .073f, .279f) : name == "HuntingRifle" ? new Vector3(0, .047f, .77f) : new Vector3(0, .094f, .242f)) : new Vector3(.02f,.05f,.08f);
                socket.transform.localRotation = socket == muzzle ? Quaternion.identity : Quaternion.Euler(0,90,0);
            }
            var shot = root.GetComponent<ShotAttack>();
            var type = Attack((AttackType)new SerializedObject(shot).FindProperty("m_Attack").objectReferenceValue, name, damage);
            var montage = root.GetComponent<AttackMontage>(); montage.AllowMovementDuringAttack = name == "HuntingRifle";
            if (montage.AllowMovementDuringAttack) montage.Animation = Load<AnimatorStateHandle>(Root + "Animation/AnimState_MobileAttack.asset");
            montage.Duration = duration; montage.AttackActivationDelay = 0; montage.CountConsumption = 1;
            var rate = root.GetComponent<AttackRate>(); if (!rate) rate = root.AddComponent<AttackRate>(); rate.CanBeHeld = false; rate.MaxAttackCount = 1; rate.AttacksPerSecond = 0;
            if (name == "HuntingRifle")
            {
                Set(shot,"m_Attack",type); Set(shot,"m_CastRadius",.04f); Set(shot,"m_MaxRange",40f); Set(shot,"m_Width",0f); Set(shot,"m_ForwardOffset",0f); Set(shot,"m_PenetrationHits",1);
                Set(shot,"m_VerticalityFactor",1f);
                Set(shot,"m_ShellKickInstantiator",null);
                Set(data,"AnimatorOverride.m_Value",Load<AnimatorOverrideController>(Root + "Animation/RifleOverride.overrideController"));
                var visual = root.AddComponent<RifleBoltVisual>(); visual.Bolt = model.GetComponentsInChildren<Transform>().First(t => t.name == "BARifleBolt");
                visual.CasingMaterial = Load<Material>(Root + "Materials/RifleSpentBrass.mat");
            }
            else if (name == "Revolver")
            {
                Set(shot,"m_Attack",type); Set(shot,"m_CastRadius",.025f); Set(shot,"m_MaxRange",22f); Set(shot,"m_Width",0f); Set(shot,"m_ForwardOffset",0f); Set(shot,"m_PenetrationHits",1); Set(shot,"m_VerticalityFactor",1f);
                var shellKick = new SerializedObject(shot).FindProperty("m_ShellKickInstantiator").objectReferenceValue;
                Set(shot, "m_ShellKickInstantiator", null);
                if (shellKick) Object.DestroyImmediate(shellKick);
            }
            else
            {
                Object.DestroyImmediate(shot);
                foreach (var inst in root.GetComponentsInChildren<ObjectInstantiator>()) Object.DestroyImmediate(inst);
                var attack = root.AddComponent<FlareGunAttack>(); attack.Muzzle = muzzle.transform; attack.Projectile = Projectile(type); montage.Attack = attack;
            }
            // Normalized meshes are in metres. Compensate for the character bone's inherited scale.
            var character = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/Character.prefab"));
            var hand = character.GetComponent<SocketController>().GetSocket(data.CharacterAttachment.Socket).transform;
            var scale = hand.lossyScale;
            data.CharacterAttachment.Scale = new Vector3(1 / Mathf.Abs(scale.x),1 / Mathf.Abs(scale.y),1 / Mathf.Abs(scale.z)) * (name == "FlareGun" ? .74f : 1f);
            Object.DestroyImmediate(character);
            // Seat each grip in the palm; source models have different grip pivots.
            if (name == "FlareGun") data.CharacterAttachment.Position = new Vector3(.000195746f, .000131942f, .000108326f);
            else if (name == "Revolver") data.CharacterAttachment.Position = new Vector3(.000198405f, .000237005f, .000202957f);
            if (name == "FlareGun") data.CharacterAttachment.Rotation = new Vector3(-1.45f, -90f, -87f);
            else if (name == "Revolver") data.CharacterAttachment.Rotation = new Vector3(-1.45f, -90f, -87f);
            if (name == "FlareGun" || name == "Revolver")
                if (!root.GetComponent<HandgunSupportPose>()) root.AddComponent<HandgunSupportPose>();
            if (name == "HuntingRifle")
            {
                var support = root.GetComponent<LongGunSupportPose>();
                if (!support) support = root.AddComponent<LongGunSupportPose>();
                support.ForeEndWrist = new Vector3(-.075f, .040f, .190f);
                support.WristRotation = new Quaternion(.09277141f, -.14839445f, -.81235290f, .55628824f);
                support.GripSeatMetres = .005f;
            }
            var shotAudio = root.GetComponent<WeaponShotAudio>();
            if (!shotAudio) shotAudio = root.AddComponent<WeaponShotAudio>();
            shotAudio.Volume = name == "FlareGun" ? 1.25f : 1;
            root.GetComponent<AudioSource>().spatialBlend = 0;
            if (name == "FlareGun")
            {
                if (!root.GetComponent<FlareGunReloadVisual>()) root.AddComponent<FlareGunReloadVisual>();
                Set(data,"AnimatorOverride.m_Value",Load<AnimatorOverrideController>(Root + "Animation/FlareGunOverride.overrideController"));
            }
            else if (name == "Revolver")
            {
                if (!root.GetComponent<RevolverReloadVisual>()) root.AddComponent<RevolverReloadVisual>();
                Set(data,"AnimatorOverride.m_Value",Load<AnimatorOverrideController>(Root + "Animation/RevolverOverride.overrideController"));
            }
            data.EquipPrefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(root); EditorUtility.SetDirty(data); return data;
        }
        static SignalFlare Projectile(AttackType impact)
        {
            var root = new GameObject("SignalFlare");
            var flare = root.AddComponent<SignalFlare>(); Set(flare,"m_Attack",impact);
            var mat = Load<Material>(Root + "Materials/FlareGlow.mat");
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, Root + "Materials/FlareGlow.mat"); }
            mat.SetColor("_BaseColor",new Color(8, .025f, .004f)); EditorUtility.SetDirty(mat);
            flare.GlowMaterial = Load<Material>(Root + "Materials/FlareHalo.mat");
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere); core.name = "Glowing flare"; core.transform.SetParent(root.transform,false); core.transform.localScale = Vector3.one * .12f;
            Object.DestroyImmediate(core.GetComponent<Collider>()); core.GetComponent<Renderer>().sharedMaterial = mat;
            var lightObj = new GameObject("Flare light"); lightObj.transform.SetParent(root.transform,false); var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1,.008f,.005f); light.range = flare.LightRange; light.intensity = flare.LightIntensity; light.shadows = LightShadows.Hard; flare.FlareLight = light;
            var sparks = new GameObject("Sparks"); sparks.transform.SetParent(root.transform,false); var ps = sparks.AddComponent<ParticleSystem>();
            var main=ps.main; main.startLifetime=.45f; main.startSpeed=.4f; main.startSize=.025f; main.startColor=new Color(1,.02f,.005f); main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission; emission.rateOverTime=35; var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.03f;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;
            var trail = root.AddComponent<TrailRenderer>();trail.time=.25f;trail.startWidth=.035f;trail.endWidth=0;trail.sharedMaterial=mat;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Root + "Effects/SignalFlare.prefab").GetComponent<SignalFlare>(); Object.DestroyImmediate(root); return saved;
        }
        static GameObject ExistingWeaponPickup(string template, string name)
        {
            var data = Load<ReloadableWeaponData>(Examples + "Weapons/" + template + "/" + template + ".asset");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/PickupItem.prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = name + "_Pickup";
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) Object.DestroyImmediate(renderer.gameObject);
            var visual = Object.Instantiate(data.EquipPrefab.GetComponent<Weapon>().Visuals, root.transform);
            visual.transform.localPosition = Vector3.zero;
            foreach (var socket in visual.GetComponentsInChildren<Socket>()) Object.DestroyImmediate(socket.gameObject);
            foreach (var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = 0;
            var bounds = BoundsOf(visual);
            visual.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            var interactive = root.GetComponentInChildren<Interactive>();
            var collider = interactive.GetComponent<BoxCollider>();
            collider.center = new Vector3(0, bounds.size.y * .5f, 0); collider.size = Vector3.Max(bounds.size, Vector3.one * .2f);
            interactive.GetComponent<Lookable>().Offset = collider.center;
            root.GetComponent<PickupItem>().Entry = new InventoryEntry { Item = data, Count = 1, SecondaryCount = data.MaxAmmo };
            root.GetComponent<ObjectUniqueId>().RegenerateId();
            var result = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + "_Pickup.prefab");
            Object.DestroyImmediate(root); return result;
        }
        static GameObject ExistingAmmoPickup(string name, string template, float count)
        {
            string path = Root + "Prefabs/" + name + "_Pickup.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Examples + "Pickups/" + template + ".prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = name + "_Pickup";
            var entry = root.GetComponent<PickupItem>();
            entry.Entry = new InventoryEntry { Item = entry.Entry.Item, Count = count, SecondaryCount = 0 };
            var visualRoots = root.GetComponentsInChildren<Renderer>().Select(r => {
                var t = r.transform;
                while (t.parent != root.transform) t = t.parent;
                return t;
            }).Distinct().ToArray();
            var bounds = BoundsOf(root);
            float scale = .15f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            foreach (var t in visualRoots) { t.localPosition *= scale; t.localScale *= scale; }
            bounds = BoundsOf(root);
            foreach (var t in visualRoots) t.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            var interactive = root.GetComponentInChildren<Interactive>();
            interactive.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var collider = interactive.GetComponent<BoxCollider>();
            collider.isTrigger = true; collider.size = new Vector3(.5f, 1f, .5f); collider.center = new Vector3(0, .5f, 0);
            interactive.GetComponent<Lookable>().Offset = collider.center;
            root.GetComponent<ObjectUniqueId>().RegenerateId();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }
        static GameObject Pickup(ItemData item, string model, float count, float loaded)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/AssetStore/HorrorEngine/Prefabs/PickupItem.prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);root.name=item.name+"_Pickup";
            foreach(var renderer in root.GetComponentsInChildren<Renderer>()) Object.DestroyImmediate(renderer.gameObject);
            GameObject visual;
            if(model!=null) visual=Model(model,root.transform);
            else
            {
                visual=new GameObject("Flare rounds");visual.transform.SetParent(root.transform,false);
                for(int i=0;i<3;i++)
                {var tube=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tube.transform.SetParent(visual.transform,false);tube.transform.localScale=new Vector3(.025f,.055f,.025f);tube.transform.localPosition=new Vector3((i-1)*.035f,0,0);Object.DestroyImmediate(tube.GetComponent<Collider>());tube.GetComponent<Renderer>().sharedMaterial=Load<Material>(Root+"Materials/FlareGlow.mat");}
            }
            var b=BoundsOf(visual);visual.transform.position+=new Vector3(-b.center.x,-b.min.y,-b.center.z);
            var interactive=root.GetComponentInChildren<Interactive>();
            var collider=interactive.GetComponent<BoxCollider>();if(!collider)collider=interactive.gameObject.AddComponent<BoxCollider>();
            interactive.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            collider.isTrigger=true;collider.center=new Vector3(0,b.size.y*.5f,0);collider.size=Vector3.Max(b.size,Vector3.one*.2f);
            if (item is AmmoData)
            {
                // Match PickupDropped's generous interaction volume, anchored at the floor.
                collider.size = Vector3.Max(b.size, new Vector3(.5f, 1f, .5f));
                collider.center = new Vector3(0, collider.size.y * .5f, 0);
            }
            var lookable=interactive.GetComponent<Lookable>();if(lookable)lookable.Offset=collider.center;
            var pickup=root.GetComponent<PickupItem>();pickup.Entry=new InventoryEntry{Item=item,Count=count,SecondaryCount=loaded};
            root.GetComponent<ObjectUniqueId>().RegenerateId();
            var saved=PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/"+root.name+".prefab");Object.DestroyImmediate(root);return saved;
        }
        [MenuItem("Horror Game/Weapons/Validate Weapon Assets")]
        public static void Validate()
        {
            var db=Load<ItemDatabase>(Examples+"Databases/ItemDB.asset");
            foreach(string name in new[]{"HuntingRifle","FlareGun","Revolver"})
            {
                var data=Load<ReloadableWeaponData>(Root+"Data/"+name+".asset");
                if(!data||!data.EquipPrefab||!data.AmmoItem||!data.Image||!data.ExamineModel||!db.Registers.Contains(data)||!db.Registers.Contains(data.AmmoItem)) throw new Exception("Incomplete weapon "+name);
                if(db.Registers.Where(i=>i).Count(i=>i.UniqueId==data.UniqueId)!=1)throw new Exception("Duplicate item ID "+name);
                var weapon=data.EquipPrefab.GetComponent<Weapon>();if(weapon.WeaponData!=data)throw new Exception("Wrong weapon data "+name);
                var montage=data.EquipPrefab.GetComponent<AttackMontage>();if(!montage.Attack||montage.CountConsumption!=1)throw new Exception("Missing attack/ammo wiring "+name);
                var visual=Object.Instantiate(weapon.Visuals);visual.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);visual.transform.localScale=Vector3.one;
                var b=BoundsOf(visual);Debug.Log(name+" normalized mesh bounds: "+b.size+" attachment scale: "+data.CharacterAttachment.Scale);
                if(b.size.magnitude<.1f||b.size.magnitude>1.6f)throw new Exception("Unexpected model scale "+name);Object.DestroyImmediate(visual);
            }
            Debug.Log("WEAPONS_ASSET_VALIDATION_OK");
            foreach(string name in new[]{"Pistol","Shotgun","HuntingRifle","FlareGun","PistolAmmo","ShotgunAmmo","HuntingRifleAmmo","FlareAmmo","Revolver","RevolverAmmo"})
            {
                var prefab=Load<GameObject>(Root+"Prefabs/"+name+"_Pickup.prefab");
                var pickup=prefab.GetComponent<PickupItem>();var interactive=prefab.GetComponentInChildren<Interactive>();
                if(!pickup.Entry.Item||!interactive||!interactive.GetComponent<Collider>()||!interactive.GetComponent<Collider>().isTrigger)throw new Exception("Invalid pickup interaction "+name);
                if(interactive.OnInteract.GetPersistentEventCount()==0||interactive.OnInteract.GetPersistentTarget(0)!=pickup)throw new Exception("Pickup callback not wired "+name);
                var instance = Object.Instantiate(prefab);
                var bounds = BoundsOf(instance);
                Object.DestroyImmediate(instance);
                if (bounds.size.magnitude < .05f || bounds.size.magnitude > 1.6f)
                    throw new Exception("Pickup visual has invalid physical size " + name + " " + bounds.size);
                if (Mathf.Abs(bounds.min.y) > .005f)
                    throw new Exception("Pickup visual is not resting on the floor " + name);
            }
            Debug.Log("WEAPONS_PICKUP_VALIDATION_OK");
        }
    }
}
