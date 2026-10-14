using System;
using System.Linq;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace HorrorGame.Weapons.Editor
{
    [InitializeOnLoad]
    public static class RevolverPresentationValidation
    {
        const string SessionKey = "HorrorGame.RevolverPresentationValidation";
        static int stage, pressFrame, shots;
        static double started, phaseStart;
        static float pressTime, shotTime;
        static Mouse mouse;
        static ReloadableWeaponData gun;
        static ItemData ammo;
        static string errors = "";
        static RevolverPresentationValidation() { EditorApplication.playModeStateChanged += OnState; }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");
            SessionState.SetBool(SessionKey, true); EditorApplication.isPlaying = true;
        }
        static void OnState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
            started = phaseStart = EditorApplication.timeSinceStartup; stage = 0; shots = 0;
            EditorApplication.update += Tick; Application.logMessageReceived += OnLog;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += message + "\n";
        }
        static void Check(bool success, string message) { if (!success) throw new Exception(message); }
        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - started > 45) throw new Exception("Revolver/UI validation timeout\n" + errors);
                if (stage == 0 && GameManager.Exists && GameManager.Instance.Player && Time.timeSinceLevelLoad > 1)
                {
                    gun = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>("Assets/Game/Weapons/Data/Revolver.asset");
                    ammo = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Game/Weapons/Data/RevolverAmmo.asset");
                    var entry = GameManager.Instance.Inventory.Add(gun, 1, 6); GameManager.Instance.Inventory.Equip(entry);
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>("Revolver tap validation mouse");
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    stage = 1; phaseStart = now;
                }
                else if (stage == 1 && now - phaseStart > .4)
                {
                    var attack = GameManager.Instance.Player.GetComponentInChildren<ShotAttack>();
                    Check(attack && !attack.GetComponent<AttackCharge>(), "Revolver still has trigger charge");
                    attack.OnAttackStart.AddListener(() => { ++shots; shotTime = Time.time; });
                    pressTime = Time.time; pressFrame = Time.frameCount;
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                    stage = 2; phaseStart = now;
                }
                else if (stage == 2 && Time.frameCount > pressFrame)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    stage = 3; phaseStart = now;
                }
                else if (stage == 3 && now - phaseStart > .4)
                {
                    Check(shots == 1 && GameManager.Instance.Inventory.GetEquippedWeapon().SecondaryCount == 5, "Quick tap did not fire exactly one revolver round");
                    Check(shotTime - pressTime < .2f, "Revolver retained a firing delay: " + (shotTime - pressTime));
                    Debug.Log("REVOLVER_TAP_FIRE_OK one-frame press, one shot, chamber 6->5, delay=" + (shotTime - pressTime));
                    InputSystem.QueueStateEvent(mouse, new MouseState()); stage = 4; phaseStart = now;
                }
                else if (stage == 4 && now - phaseStart > 1)
                {
                    var pickup = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Weapons/Prefabs/RevolverAmmo_Pickup.prefab")).GetComponent<PickupItem>();
                    Check(pickup.Entry.Item == ammo, "Wrong ammo data on pickup"); pickup.Take(); stage = 5; phaseStart = now;
                }
                else if (stage == 5 && now - phaseStart > .3)
                {
                    var ui = UIManager.Get<UIItem>();
                    var image = ui.GetComponentsInChildren<Image>().FirstOrDefault(i => i.sprite == ammo.Image);
                    Check(ui.gameObject.activeInHierarchy && image && image.enabled && image.gameObject.activeInHierarchy, "Actual ammo pickup UI did not show the new icon");
                    Check(AssetDatabase.GetAssetPath(image.sprite).EndsWith("RevolverAmmo_PickupIcon.png"), "Pickup UI is using the stale image");
                    Check(image.sprite.texture.width == 1024, "Pickup sprite import has wrong resolution");
                    var model = UnityEngine.Object.Instantiate(ammo.ExamineModel);
                    var bounds = model.GetComponentInChildren<MeshRenderer>().bounds;
                    Check(Mathf.Abs(bounds.size.x - .88f) < .01f, "Magnum examination model still too small");
                    UnityEngine.Object.Destroy(model);
                    Check(errors.Length == 0, errors);
                    Debug.Log("MAGNUM_PICKUP_UI_OK actual Take() displayed fresh 1024px icon; examination width .88m"); Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            if (mouse != null) InputSystem.RemoveDevice(mouse); EditorApplication.Exit(code);
        }
    }
}
