// Copy this helper into the isolated validation project's Editor folder to run.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HorrorGame.Weapons.Editor
{
    [InitializeOnLoad]
    public static class MP5IntegrationValidation
    {
        const string Root = "Assets/Game/Weapons/";
        const string Examples = "Assets/AssetStore/HorrorEngine/ExampleContent/";
        const string Key = "HorrorGame.MP5IntegrationValidation";
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSource/Weapons/MP5/Preview");
        static int stage, frame, captures, assertions;
        static float phase, reserveBefore, loadedBefore;
        static double started, wallPhase;
        static bool sawLock, sawRemoved, sawSeated;
        static Keyboard keyboard; static Mouse mouse;
        static ReloadableWeaponData data; static InventoryEntry gun, ammo;
        static ActorStateController states; static UIInventory inventoryUI;
        static Camera camera; static RenderTexture target;
        static Vector3 position, foot;
        static readonly Dictionary<ScriptableRendererFeature, bool> Features = new Dictionary<ScriptableRendererFeature, bool>();
        static MP5IntegrationValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        static void Check(bool condition, string message) { ++assertions; if (!condition) throw new Exception("MP5: " + message); }
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static Inventory Inventory => GameManager.Instance.Inventory;
        static Actor Player => GameManager.Instance.Player;

        public static void ValidateAssets()
        {
            data = Load<ReloadableWeaponData>(Root + "Data/MP5.asset");
            Check(data && data.MaxAmmo == 30 && Mathf.Abs(data.ReloadDuration - 3) < .01f, "30-round / 3-second weapon data missing");
            Check(data.AllowMovementDuringReload && data.AmmoItem == Load<AmmoData>(Root + "Data/SMGAmmo.asset"), "walking reload / separate SMG reserve wiring missing");
            Check(data.ShotSound && data.ReloadSound && data.Image && data.ExamineModel, "audio/icon/examination missing");
            Check(AssetDatabase.GetAssetPath(data.Image) == Root + "Models/MP5/MP5_Icon.png", "MP5 still uses placeholder inventory icon");
            var ammoData = data.AmmoItem as AmmoData; Check(ammoData && ammoData.MaxStackSize == 120, "SMG stack should be 120");
            var weapon = data.EquipPrefab.GetComponent<Weapon>(); Check(weapon && weapon.WeaponData == data, "equip prefab data mismatch");
            var rate = data.EquipPrefab.GetComponent<AttackRate>();
            Check(rate && rate.CanBeHeld && rate.MaxAttackCount == 0 && Mathf.Abs(rate.AttacksPerSecond - 800f / 60) < .01f, "automatic 800 rpm not configured");
            Check(!data.EquipPrefab.GetComponent<AttackCharge>() && !data.EquipPrefab.GetComponent<WeaponAmmoVisuals>() && !data.EquipPrefab.GetComponent<LongGunSupportPose>(), "competing charge/magazine/support components retained");
            Check(data.EquipPrefab.GetComponent<MP5ReloadVisual>() && data.EquipPrefab.GetComponent<MP5HandlingPose>(), "MP5 presentation components missing");
            var reload = data.EquipPrefab.GetComponent<MP5ReloadVisual>();
            Check(reload.Magazine && reload.ModelFrame, "magazine/model references missing");
            var montage = data.EquipPrefab.GetComponent<AttackMontage>();
            Check(montage && montage.Attack is ShotAttack && montage.CountConsumption == 1 && montage.AttackActivationDelay == 0, "hitscan attack / ammo cost wiring wrong");
            var database = Load<ItemDatabase>(Examples + "Databases/ItemDB.asset");
            foreach (var item in new ItemData[] { data, ammoData })
            {
                Check(database.Registers.Contains(item) && database.Registers.Where(x => x).Count(x => x.UniqueId == item.UniqueId) == 1, "item database registration or unique ID wrong: " + item.name);
                var pickup = Load<GameObject>(Root + "Prefabs/" + (item == data ? "MP5" : "SMGAmmo") + "_Pickup.prefab");
                Check(pickup && pickup.GetComponent<PickupItem>().Entry.Item == item, "normal pickup wrong: " + item.name);
                var interactive = pickup.GetComponentInChildren<Interactive>();
                Check(interactive && interactive.GetComponent<Collider>().isTrigger && interactive.OnInteract.GetPersistentEventCount() > 0, "pickup callback/trigger missing");
            }
            var rack = Load<GameObject>(Root + "Prefabs/Weapon_TestPickups.prefab");
            Check(rack.transform.Find("MP5_Pickup") && rack.transform.Find("SMGAmmo_Pickup"), "test rack missing additions");
            var ids = rack.GetComponentsInChildren<ObjectUniqueId>(true).Select(x => x.Id).ToArray();
            Check(ids.All(x => !string.IsNullOrEmpty(x)) && ids.Distinct().Count() == ids.Length, "duplicate / empty test-rack IDs");
            Debug.Log("MP5_ASSETS_OK assertions=" + assertions);
        }
        public static void RunBatch()
        {
            try { ValidateAssets(); EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity"); SessionState.SetBool(Key, true); EditorApplication.isPlaying = true; }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void OnPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            Directory.CreateDirectory(Output); stage = frame = captures = 0; started = EditorApplication.timeSinceStartup;
            Time.captureFramerate = 60; EditorApplication.update += Tick;
        }
        static void Next(int value) { stage = value; phase = Time.time; wallPhase = EditorApplication.timeSinceStartup; }
        static void Inputs(bool aim = false, bool fire = false, params UnityEngine.InputSystem.Key[] keys)
        {
            var state = new MouseState(); if (aim) state = state.WithButton(MouseButton.Right); if (fire) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("MP5 integration timed out at stage " + stage);
                float elapsed = Time.time - phase;
                if (stage == 0 && GameManager.Exists && Player && Time.timeSinceLevelLoad > 3)
                {
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
                    states = Player.GetComponentInChildren<ActorStateController>(); inventoryUI = UIManager.Get<UIInventory>();
                    data = Load<ReloadableWeaponData>(Root + "Data/MP5.asset");
                    if (!Inventory.TryGet(data, out gun)) gun = Inventory.Add(data, 1, 30); gun.SecondaryCount = 30; Inventory.Equip(gun); gun = Inventory.GetEquippedWeapon();
                    if (!Inventory.TryGet(data.AmmoItem, out ammo)) ammo = Inventory.Add(data.AmmoItem, 120); ammo.Count = 120;
                    camera = new GameObject("MP5 animation review").AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 35; camera.nearClipPlane = .02f;
                    camera.cullingMask = ~(1 << 5); target = new RenderTexture(960, 720, 24); camera.targetTexture = target;
                    var light = new GameObject("MP5 review key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(40, -35, 0);
                    Inputs(); Next(1);
                }
                else if (stage == 1 && elapsed > .6f) { Inputs(true); Next(2); }
                else if (stage == 2 && elapsed > .5f) { Check(states.m_CurrentState is PlayerStateAiming, "aim failed"); loadedBefore = gun.SecondaryCount; Inputs(true, true); Next(3); }
                else if (stage == 3 && elapsed > .65f)
                {
                    float fired = loadedBefore - gun.SecondaryCount; Check(fired >= 6 && fired <= 12, "held automatic shot count unreasonable: " + fired);
                    Inputs(true); loadedBefore = gun.SecondaryCount; Next(4);
                }
                else if (stage == 4 && elapsed > .5f) { Check(gun.SecondaryCount == loadedBefore, "continued firing after release"); gun.SecondaryCount = 2; reserveBefore = ammo.Count; Inputs(true, true); Next(5); }
                else if (stage == 5 && elapsed > 1)
                {
                    Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore && !(states.m_CurrentState is PlayerStateReload), "empty magazine auto-reloaded");
                    Inputs(true); Next(6);
                }
                else if (stage == 6 && elapsed > .4f) { Inputs(true, true); Next(7); }
                else if (stage == 7 && elapsed > .35f)
                {
                    Check(gun.SecondaryCount == 0 && !(states.m_CurrentState is PlayerStateReload), "empty trigger auto-reloaded");
                    Inputs(true, false, UnityEngine.InputSystem.Key.R, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift); Next(8);
                }
                else if (stage == 8 && states.m_CurrentState is PlayerStateReload)
                {
                    Check(gun.SecondaryCount == 0, "reload loaded before animation completed");
                    position = Player.transform.position; foot = Player.transform.InverseTransformPoint(Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                    Inputs(true, false, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift); sawLock = sawRemoved = sawSeated = false; Next(9);
                }
                else if (stage == 9)
                {
                    var visual = Player.GetComponentInChildren<MP5ReloadVisual>();
                    if (visual) { sawLock |= visual.HandleLocked; sawRemoved |= !visual.MagazineSeated; if (sawRemoved && visual.MagazineSeated) sawSeated = true; }
                    Capture("Aimed");
                    if (elapsed > .6f && elapsed < .7f)
                    {
                        var movement = Player.GetComponent<PlayerMovement>();
                        Check(movement.enabled && movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Running), "reload doesn't enforce walking");
                        Check(Vector3.Distance(position, Player.transform.position) > .1f, "cannot walk during reload");
                        Check(Vector3.Distance(foot, Player.transform.InverseTransformPoint(Player.MainAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position)) > .002f, "walking feet not animating");
                        Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore, "ammo granted early"); Inputs(true);
                    }
                    if (!(states.m_CurrentState is PlayerStateReload) && elapsed > .3f)
                    {
                        Check(gun.SecondaryCount == 30 && ammo.Count == reserveBefore - 30, "reload transfer incorrect");
                        Check(states.m_CurrentState is PlayerStateAiming && sawLock && sawRemoved && sawSeated, "aim return or MP5 mechanical sequence missing");
                        Inputs(); Next(10);
                    }
                }
                else if (stage == 10 && elapsed > .5f)
                {
                    Check(states.m_CurrentState is PlayerStateMotion, "cannot lower weapon"); gun.SecondaryCount = 0; reserveBefore = ammo.Count;
                    inventoryUI.Show(); Inventory.Combine(ammo, gun); Check(gun.SecondaryCount == 0 && Inventory.HasPendingEquippedWeaponReload(), "inventory combine skipped animation"); Next(11);
                }
                else if (stage == 11 && EditorApplication.timeSinceStartup - wallPhase > .2f) { inventoryUI.Hide(); Next(12); }
                else if (stage == 12 && states.m_CurrentState is PlayerStateReload) { Next(13); }
                else if (stage == 13)
                {
                    Capture("Lowered");
                    if (!(states.m_CurrentState is PlayerStateReload) && elapsed > .3f)
                    {
                        Check(states.m_CurrentState is PlayerStateMotion && gun.SecondaryCount == 30 && ammo.Count == reserveBefore - 30, "inventory queued lowered reload failed");
                        Check(!Inventory.HasPendingEquippedWeaponReload(), "inventory reload queue not cleared");
                        Debug.Log("MP5_INTEGRATION_OK assertions=" + assertions + " hold-fire, release, empty/manual-only, walk-only, animated feet, mechanical reload, held-aim return, inventory-close reload"); Finish(0);
                    }
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void Capture(string sequence)
        {
            if (!camera || frame == Time.frameCount) return; frame = Time.frameCount;
            var focus = Player.transform.position + Vector3.up * 1.28f + Player.transform.forward * .22f;
            camera.transform.position = focus + Player.transform.right * 1.45f + Player.transform.forward * .65f + Vector3.up * .13f; camera.transform.LookAt(focus);
            Save(Path.Combine(Output, sequence + "_" + (captures++).ToString("D5") + ".png"));
        }
        static void Save(string path)
        {
            camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = old; Object.DestroyImmediate(image); }
        }
        static void Finish(int code)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); Time.captureFramerate = 0;
            if (mouse != null) InputSystem.RemoveDevice(mouse); if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (camera) Object.DestroyImmediate(camera.gameObject); if (target) { target.Release(); Object.DestroyImmediate(target); }
            EditorApplication.Exit(code);
        }

        static int warmup;
        public static void RunIconBatch()
        {
            try
            {
                Directory.CreateDirectory(Output); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
                    foreach (var feature in Load<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)).rendererFeatures)
                        if (feature && feature.GetType().Name.Contains("FullScreen") && !Features.ContainsKey(feature)) { Features.Add(feature, feature.isActive); feature.SetActive(false); }
                RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.18f, .19f, .21f);
                var key = new GameObject("Warm horror key").AddComponent<Light>(); key.type = LightType.Directional; key.color = new Color(1, .65f, .35f); key.intensity = 2.2f; key.transform.rotation = Quaternion.Euler(35, -40, 0);
                var fill = new GameObject("Cool horror edge").AddComponent<Light>(); fill.type = LightType.Directional; fill.color = new Color(.49f, .64f, 1); fill.intensity = 1.35f; fill.transform.rotation = Quaternion.Euler(30, 140, 0);
                data = Load<ReloadableWeaponData>(Root + "Data/MP5.asset");
                var model = Object.Instantiate(data.ExamineModel); foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 0;
                // Runtime uses the established PSX unlit shader. Preview materials are
                // temporary lit copies so the inventory render receives horror lighting.
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        var texture = original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : original.GetTexture("_BaseMap");
                        // Smooth only the magazine in this UI render. The gameplay
                        // atlas keeps its established point-filtered PSX appearance.
                        if (renderer.name.Contains("Magazene") && texture)
                        {
                            var magazineTexture = Object.Instantiate(texture);
                            magazineTexture.name = "MP5 magazine icon preview";
                            magazineTexture.filterMode = FilterMode.Bilinear;
                            magazineTexture.anisoLevel = 4;
                            texture = magazineTexture;
                        }
                        lit.SetTexture("_BaseMap", texture);
                        lit.SetColor("_BaseColor", Color.white); lit.SetFloat("_Metallic", .12f); lit.SetFloat("_Smoothness", .16f); return lit;
                    }).ToArray();
                }
                camera = new GameObject("MP5 inventory camera").AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.nearClipPlane = .001f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                target = new RenderTexture(1024, 1024, 24); camera.targetTexture = target;
                var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                camera.transform.position = bounds.center + new Vector3(-2, .35f, -.9f) * bounds.size.magnitude * 3; camera.transform.LookAt(bounds.center); camera.transform.Rotate(0, 0, 35, Space.Self);
                float fit = 0;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                { var point = camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))); fit = Mathf.Max(fit, Mathf.Abs(point.x), Mathf.Abs(point.y)); }
                var faceLight = new GameObject("Neutral inventory face fill").AddComponent<Light>(); faceLight.type = LightType.Directional; faceLight.color = new Color(.92f, .95f, 1); faceLight.intensity = 1.0f; faceLight.transform.rotation = camera.transform.rotation;
                camera.orthographicSize = fit * 1.14f; warmup = 0; EditorApplication.update += CaptureIcon;
            }
            catch (Exception error) { Debug.LogException(error); FinishIcon(1); }
        }
        static void CaptureIcon()
        {
            if (++warmup < 8) return;
            try { Save(Root + "Models/MP5/MP5_Icon.png"); Save(Path.Combine(Output, "MP5_InventoryIcon.png")); AssetDatabase.ImportAsset(Root + "Models/MP5/MP5_Icon.png", ImportAssetOptions.ForceSynchronousImport); MP5AssetBuilder.RefreshIcon(); Debug.Log("MP5_ICON_OK"); FinishIcon(0); }
            catch (Exception error) { Debug.LogException(error); FinishIcon(1); }
        }
        static void FinishIcon(int code)
        {
            EditorApplication.update -= CaptureIcon; foreach (var item in Features) if (item.Key) item.Key.SetActive(item.Value); Features.Clear();
            if (camera) Object.DestroyImmediate(camera.gameObject); if (target) { target.Release(); Object.DestroyImmediate(target); } EditorApplication.Exit(code);
        }
    }
}
