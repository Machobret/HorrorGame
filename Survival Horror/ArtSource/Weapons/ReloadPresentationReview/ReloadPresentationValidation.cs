using System;
using System.Collections.Generic;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HorrorGame.Weapons.Editor
{
    // Isolated editor-only validation. Copy to Assets/.../Editor; RunBatch captures
    // actual Play Mode reloads. No test components or changes enter the main scene.
    [InitializeOnLoad]
    public static class ReloadPresentationValidation
    {
        const string Key = "HorrorGame.ReloadPresentationValidation";
        public static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ReloadPresentationReview");
        static readonly string[] Names = { "FlareGun", "Revolver" };
        static int stage, index, sample, frame, lastCapturedFrame;
        static double started, phase;
        static float reloadStart, lastFrame, reserveBefore, loadedBefore;
        static bool checkedWalking, sawOpen;
        static MonoBehaviour presentation;
        static Vector3 startPosition;
        static Mouse mouse;
        static Keyboard keyboard;
        static ActorStateController states;
        static ReloadableWeaponData data;
        static InventoryEntry gun, ammo;
        static UIInventory ui;
        static Camera camera;
        static RenderTexture target;
        static Transform weapon;
        static Dictionary<Transform, Pose> initialParts;
        struct Pose { public Vector3 Position; public Quaternion Rotation; public Vector3 Scale; public Transform Parent; }
        static ReloadPresentationValidation() { EditorApplication.playModeStateChanged += OnState; }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        static Inventory Inv => GameManager.Instance.Inventory;
        static Actor Player => GameManager.Instance.Player;
        static void Next(int value) { stage = value; phase = EditorApplication.timeSinceStartup; }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(data?.name + ": " + message); }
        static void OnState(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup; index = stage = 0;
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "CaptureTimes.csv"), "capture,gameTime\n"); EditorApplication.update += Tick;
        }
        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup, dt = now - phase;
                if (now - started > 110) throw new Exception("Reload presentation timeout, stage=" + stage);
                if (stage == 0 && GameManager.Exists && Player && Time.timeSinceLevelLoad > 3)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 24;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
                    states = Player.GetComponentInChildren<ActorStateController>(); ui = UIManager.Get<UIInventory>();
                    // Keep the template's fixed inventory slot count; this test needs
                    // only the two guns and their two ammo stacks. Remove contents via
                    // the public API so equipment/UI notifications remain consistent.
                    var initialEntries = new List<InventoryEntry>(Inv.Items);
                    foreach (var entry in initialEntries)
                        if (entry.Item && entry.Count > 0) Inv.Remove(entry, entry.Count);
                    camera = new GameObject("Reload mechanism review camera").AddComponent<Camera>();
                    camera.enabled = false; camera.fieldOfView = 34; camera.nearClipPlane = .02f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .05f, .055f);
                    camera.cullingMask = ~(1 << 5); target = new RenderTexture(960, 720, 24); camera.targetTexture = target;
                    var light = new GameObject("Reload mechanism key").AddComponent<Light>();
                    light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, 35, 0);
                    Next(1);
                }
                else if (stage == 1 && dt > .3)
                {
                    data = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>("Assets/Game/Weapons/Data/" + Names[index] + ".asset");
                    if (!Inv.TryGet(data, out gun)) gun = Inv.Add(data, 1, 0);
                    gun.SecondaryCount = 0; Inv.Equip(gun); gun = Inv.GetEquippedWeapon();
                    if (!Inv.TryGet(data.AmmoItem, out ammo)) ammo = Inv.Add(data.AmmoItem, data.MaxAmmo * 4 + 4);
                    ammo.Count = data.MaxAmmo * 4 + 4; reserveBefore = ammo.Count;
                    InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(2);
                }
                else if (stage == 2 && dt > .5)
                {
                    weapon = Player.GetComponent<PlayerEquipment>().GetWeaponInstance(Inv.WeaponSlot).transform;
                    initialParts = Snapshot(weapon);
                    foreach (var component in weapon.GetComponents<MonoBehaviour>())
                        if (component.GetType().Name == data.name + "ReloadVisual") presentation = component;
                    Check(presentation, "weapon-specific reload presentation missing");
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); Next(3);
                }
                else if (stage == 3 && dt > .35)
                {
                    Check(states.m_CurrentState is PlayerStateAiming, "real aim input failed");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift)); Next(4);
                }
                else if (stage == 4 && states.m_CurrentState is PlayerStateReload)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift));
                    reloadStart = lastFrame = Time.time; startPosition = Player.transform.position; sample = frame = 0; lastCapturedFrame = -1; checkedWalking = sawOpen = false;
                    Check(Flag("IsReloading"), "presentation did not begin with the real reload state");
                    PrepareFramesDirectory(); Next(5);
                }
                else if (stage == 5)
                {
                    float elapsed = Time.time - reloadStart;
                    if (Number("OpenAmount") > .2f) sawOpen = true;
                    if (states.m_CurrentState is PlayerStateReload)
                    {
                        // Duration completion transfers ammo before the controller applies
                        // its requested next state on the following frame.
                        if (elapsed < data.ReloadDuration - .1f)
                            Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore, "ammo transferred before completion");
                        if (lastCapturedFrame != Time.frameCount)
                        { Capture(data.name + "_Frames/" + (frame++).ToString("D4")); lastCapturedFrame = Time.frameCount; }
                        if (sample < 8 && elapsed >= data.ReloadDuration * sample / 8f)
                        { Capture(data.name + "_Phase_" + sample++); }
                        if (!checkedWalking && elapsed > .4f)
                        {
                            var movement = Player.GetComponent<PlayerMovement>();
                            float distance = Vector3.ProjectOnPlane(Player.transform.position - startPosition, Vector3.up).magnitude;
                            float walk = new SerializedObject(movement).FindProperty("m_MovementSpeed").floatValue;
                            Check(movement.enabled && movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Running), "walk-only constraint absent");
                            Check(distance > .05f && distance <= walk * elapsed * 1.3f + .05f, "reload froze movement or allowed sprint");
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); checkedWalking = true;
                        }
                    }
                    else if (elapsed > .2f)
                    {
                        Check(checkedWalking, "walking sample missed");
                        Check(sawOpen && !Flag("IsReloading"), "mechanism never opened or presentation did not stop");
                        if (data.name == "Revolver")
                            Check(Number("LoadedRoundCount") == data.MaxAmmo, "full reload did not visibly insert every required round");
                        Check(gun.SecondaryCount == data.MaxAmmo && ammo.Count == reserveBefore - data.MaxAmmo, "manual completion count mismatch");
                        Check(states.m_CurrentState is PlayerStateMotion && !Player.GetComponent<IPlayerInput>().IsAimingHeld(), "held aim snapped back after reload");
                        InputSystem.QueueStateEvent(mouse, new MouseState()); Capture(data.name + "_Complete"); Next(6);
                    }
                }
                else if (stage == 6 && dt > .4)
                {
                    loadedBefore = Mathf.Max(0, data.MaxAmmo - 2); gun.SecondaryCount = loadedBefore; reserveBefore = ammo.Count;
                    ui.Show(); Check(PauseController.Instance.IsPaused, "inventory did not pause"); Inv.Combine(ammo, gun); Next(7);
                }
                else if (stage == 7 && dt > .3)
                {
                    Check(gun.SecondaryCount == loadedBefore && ammo.Count == reserveBefore, "inventory combination skipped animation");
                    Check(!(states.m_CurrentState is PlayerStateReload) && Inv.HasPendingEquippedWeaponReload(), "reload started while inventory open or was not queued");
                    ui.Hide(); Next(8);
                }
                else if (stage == 8 && states.m_CurrentState is PlayerStateReload) { reloadStart = Time.time; Next(9); }
                else if (stage == 9)
                {
                    if (states.m_CurrentState is PlayerStateReload)
                    {
                        if (Time.time - reloadStart < data.ReloadDuration - .1f)
                            Check(gun.SecondaryCount == loadedBefore && ammo.Count == reserveBefore, "partial reload transferred early");
                    }
                    else if (dt > .2)
                    {
                        Check(gun.SecondaryCount == data.MaxAmmo && ammo.Count == reserveBefore - (data.MaxAmmo - loadedBefore), "partial reload consumed wrong round count");
                        Check(!Inv.HasPendingEquippedWeaponReload(), "completed queue not cleared");
                        if (data.name == "Revolver")
                            Check(Number("LoadedRoundCount") == data.MaxAmmo - loadedBefore, "partial reload visibly inserted the wrong number of rounds");
                        gun.SecondaryCount = 0; reserveBefore = ammo.Count;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R)); Next(10);
                    }
                }
                else if (stage == 10 && states.m_CurrentState is PlayerStateReload)
                { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); reloadStart = Time.time; Next(11); }
                else if (stage == 11 && Time.time - reloadStart > data.ReloadDuration * .45f)
                {
                    Capture(data.name + "_Interrupted");
                    var motion = Player.GetComponentInChildren<PlayerStateMotion>(true);
                    states.SetState(motion, true);
                    Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore, "interruption granted ammo");
                    Check(!Flag("IsReloading") && Number("OpenAmount") < .001f, "interruption left mechanism active/open");
                    CheckReset(initialParts);
                    foreach (var part in weapon.GetComponentsInChildren<Transform>(true))
                        if (part.name == "RevolverReloadRounds" || part.name == "FlareReloadSpentCase" || part.name == "FlareReloadFreshCartridge")
                            Check(!part.gameObject.activeInHierarchy, "interruption left a transient cartridge visible: " + part.name);
                    Next(12);
                }
                else if (stage == 12 && dt > .25)
                {
                    Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore, "interrupted reload later granted ammo");
                    Check(!Player.GetComponent<PlayerMovement>().Constrain.HasFlag(PlayerMovement.MovementConstrain.Running), "interrupted reload leaked movement constraint");
                    Capture(data.name + "_Reset");
                    Debug.Log("RELOAD_PRESENTATION_OK " + data.name + " real manual input, walk-only, inventory-close partial reload, cancellation reset, frames=" + frame);
                    if (++index == Names.Length) { Debug.Log("ALL_RELOAD_PRESENTATION_CHECKS_OK"); Finish(0); } else Next(1);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void PrepareFramesDirectory()
        {
            string root = Path.GetFullPath(Output);
            string frames = Path.GetFullPath(Path.Combine(root, Names[index] + "_Frames"));
            Check(string.Equals(Path.GetDirectoryName(frames), root, StringComparison.OrdinalIgnoreCase), "capture cleanup escaped its output directory");
            Directory.CreateDirectory(frames);
            foreach (var oldFrame in Directory.GetFiles(frames, "*.png", SearchOption.TopDirectoryOnly))
                File.Delete(oldFrame);
        }
        static bool Flag(string property) => (bool)presentation.GetType().GetProperty(property).GetValue(presentation);
        static float Number(string property) => Convert.ToSingle(presentation.GetType().GetProperty(property).GetValue(presentation));
        static bool IsTransientPart(Transform part)
        {
            for (var node = part; node; node = node.parent)
            {
                if (node.name == "RevolverReloadRounds" || node.name == "FlareReloadSpentCase" || node.name == "FlareReloadFreshCartridge")
                    return true;
            }
            return false;
        }
        static Dictionary<Transform, Pose> Snapshot(Transform root)
        {
            var result = new Dictionary<Transform, Pose>();
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part.GetComponent<ParticleSystem>() || IsTransientPart(part)) continue;
                result[part] = new Pose { Position = part.localPosition, Rotation = part.localRotation, Scale = part.localScale, Parent = part.parent };
            }
            return result;
        }
        static void CheckReset(Dictionary<Transform, Pose> poses)
        {
            foreach (var pair in poses)
            {
                if (!pair.Key) continue;
                var part = pair.Key; var pose = pair.Value;
                Check(part.parent == pose.Parent && Vector3.Distance(part.localPosition, pose.Position) < .0001f &&
                    Quaternion.Angle(part.localRotation, pose.Rotation) < .1f && Vector3.Distance(part.localScale, pose.Scale) < .0001f,
                    "interrupted reload did not restore mesh/attachment: " + part.name);
            }
        }
        static void Capture(string name)
        {
            var focus = Player.transform.position + Vector3.up * 1.38f + Player.transform.forward * .35f;
            camera.transform.position = focus - Player.transform.right * 1.25f + Player.transform.forward * .4f + Vector3.up * .1f;
            camera.transform.LookAt(focus); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            File.AppendAllText(Path.Combine(Output, "CaptureTimes.csv"), name + "," + Time.time.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "\n");
            RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Time.captureFramerate = 0;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
