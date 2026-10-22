using System;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HorrorGame.Weapons.Editor
{
    [InitializeOnLoad]
    public static class FlareTestSceneValidation
    {
        const string Key = "HorrorGame.FlareSceneValidation";
        const string Output = "C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation";
        static double started, phaseStart;
        static int phase;
        static int reloadPressFrame;
        static FlareTestRoom room;
        static SignalFlare demo;
        static Vector3 launchPosition;
        static RenderTexture target;
        static Camera camera;
        static string errors = "";
        static Mouse inputMouse;
        static Keyboard inputKeyboard;
        static Vector3 reloadPosition;
        static Vector3 leftFootPose, rightFootPose;

        static FlareTestSceneValidation() { EditorApplication.playModeStateChanged += OnState; }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        static void OnState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = phaseStart = EditorApplication.timeSinceStartup;
            phase = 0;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            // Unity's first-project search-index initialization is editor-only.
            if (stack.IndexOf("UnityEditor.Search.SearchDatabase", StringComparison.Ordinal) >= 0)
            {
                Debug.LogWarning("Editor search-index initialization failed; excluded from gameplay checks.");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += message + "\n";
        }
        static void Check(bool success, string message) { if (!success) throw new Exception(message); }
        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - started > 45) throw new Exception("Flare scene timed out\n" + errors);
                if (phase == 0)
                {
                    if (!GameManager.Exists || !GameManager.Instance.Player || Time.timeSinceLevelLoad < 1) return;
                    room = UnityEngine.Object.FindFirstObjectByType<FlareTestRoom>();
                    Check(room && room.Projectile && room.TestCamera, "Test controls lost references");
                    Check(GameManager.Instance.Inventory.GetEquippedWeapon()?.Item == room.FlareGun, "Flare gun was not equipped");
                    Check(GameManager.Instance.Inventory.TryGet(room.FlareGun.AmmoItem, out var ammo) && ammo.Count >= 10, "Spare flare ammo missing");
                    camera = Camera.main; Check(camera, "No player camera");
                    target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
                    room.SlowMotion(); room.Demo();
                    demo = UnityEngine.Object.FindFirstObjectByType<SignalFlare>(); Check(demo, "Demo launch spawned no projectile");
                    launchPosition = demo.transform.position; phase = 1; phaseStart = now;
                }
                else if (phase == 1 && demo && (demo.Age > .2f || demo.HasLanded))
                {
                    Check(demo && !demo.HasLanded && demo.transform.position.z > launchPosition.z + .5f, "Flare did not fly: pos=" + demo.transform.position + " age=" + demo.Age + " parent=" + demo.transform.parent + " owner=" + demo.Owner + " scale=" + Time.timeScale);
                    Check(demo.GetComponent<TrailRenderer>().positionCount > 1, "Flare trail missing");
                    Check(demo.FlareLight && demo.FlareLight.enabled && demo.FlareLight.intensity > 10, "Flare light missing");
                    Capture("Flare_Overview.png"); room.ChangeView(); phase = 2; phaseStart = now;
                }
                else if (phase == 2 && now - phaseStart > .8)
                {
                    Capture("Flare_SideView.png"); room.SlowMotion(); phase = 3; phaseStart = now;
                }
                else if (phase == 3 && now - phaseStart > 1.5)
                {
                    Check(demo && demo.HasLanded && demo.transform.parent, "Flare did not land/stick");
                    var player = GameManager.Instance.Player;
                    var attack = player.GetComponentInChildren<FlareGunAttack>();
                    Check(attack && attack.Muzzle && attack.Projectile, "Equipped flare attack/muzzle missing");
                    // Exercise aim/fire input, state transitions, consumption and montage scheduling.
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    inputMouse = InputSystem.AddDevice<Mouse>("Flare validation mouse");
                    InputSystem.QueueStateEvent(inputMouse, new MouseState().WithButton(MouseButton.Right));
                    phase = 4; phaseStart = now;
                }
                else if (phase == 4 && now - phaseStart > .4)
                {
                    var listener = GameManager.Instance.Player.GetComponentInChildren<PlayerInputListener>();
                    Check(listener.IsAimingHeld(), "Synthetic aim input was not delivered to the character");
                    InputSystem.QueueStateEvent(inputMouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                    phase = 5; phaseStart = now;
                }
                else if (phase == 5 && now - phaseStart > .5)
                {
                    var flares = UnityEngine.Object.FindObjectsByType<SignalFlare>(FindObjectsSortMode.None);
                    var entry = GameManager.Instance.Inventory.GetEquippedWeapon();
                    Check(entry.SecondaryCount == 0, "Character input did not consume the loaded flare; state=" + GameManager.Instance.Player.GetComponentInChildren<ActorStateController>().CurrentState);
                    Check(flares.Length == 2, "Character firing consumed its only round but spawned no projectile (count=" + flares.Length + ")");
                    Check(errors.Length == 0, errors);
                    Debug.Log("FLARE_TEST_SCENE_PLAY_OK: player, equipped gun, ammo, demo flight/trail/light, camera captures, collision, real aim/fire input, last round consumed and exactly one projectile launched");
                    InputSystem.QueueStateEvent(inputMouse, new MouseState());
                    phase = 10; phaseStart = now;
                }
                else if (phase == 10 && !(GameManager.Instance.Player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateAttack))
                {
                    Check(GameManager.Instance.Inventory.GetEquippedWeapon().SecondaryCount == 0, "Empty flare auto-reloaded");
                    inputKeyboard = InputSystem.AddDevice<Keyboard>("Flare manual reload keyboard");
                    InputSystem.QueueStateEvent(inputKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.R));
                    reloadPressFrame = Time.frameCount;
                    phase = 11;
                }
                else if (phase == 11 && Time.frameCount > reloadPressFrame)
                {
                    InputSystem.QueueStateEvent(inputKeyboard, new KeyboardState());
                    phase = 6; phaseStart = now;
                }
                else if (phase == 6 && GameManager.Instance.Player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateReload)
                {
                    var movement = GameManager.Instance.Player.GetComponent<PlayerMovement>();
                    Check(movement.enabled && !movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Movement), "Reload still locks movement");
                    reloadPosition = GameManager.Instance.Player.transform.position;
                    if (inputKeyboard == null) inputKeyboard = InputSystem.AddDevice<Keyboard>("Flare reload movement keyboard");
                    InputSystem.QueueStateEvent(inputKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                    phase = 7; phaseStart = now;
                }
                else if (phase == 7 && now - phaseStart > .4)
                {
                    var player = GameManager.Instance.Player;
                    Check(player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateReload, "Reload exited before its duration");
                    float distance = Vector3.ProjectOnPlane(player.transform.position - reloadPosition, Vector3.up).magnitude;
                    Check(distance > .15f, "Character did not walk during reload: distance=" + distance);
                    Check(GameManager.Instance.Inventory.GetEquippedWeapon().SecondaryCount == 0, "Ammo refilled before reload completed");
                    Capture("Flare_MovingReload.png");
                    Debug.Log("FLARE_RELOAD_MOVEMENT_OK walking distance=" + distance + "; still reloading with empty chamber");
                    var animator = player.MainAnimator;
                    Check(animator.isHuman, "Foot animation check requires the character's humanoid rig");
                    int reloadLayer = animator.GetLayerIndex("Mobile Reload");
                    Check(reloadLayer >= 0 && animator.GetLayerWeight(reloadLayer) > .9f, "Upper-body reload layer is not active");
                    Check(animator.GetLayerWeight(animator.GetLayerIndex("Weapon Layer")) < .05f, "Full-body weapon layer still overrides walking");
                    leftFootPose = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                    rightFootPose = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
                    phase = 9; phaseStart = now;
                }
                else if (phase == 9 && now - phaseStart > .18)
                {
                    var player = GameManager.Instance.Player; var animator = player.MainAnimator;
                    Check(player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateReload, "Reload ended before foot animation check");
                    float left = Vector3.Distance(leftFootPose, player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position));
                    float right = Vector3.Distance(rightFootPose, player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position));
                    Check(Mathf.Max(left, right) > .025f, "Player translated but feet stayed stationary: " + left + ", " + right);
                    Capture("Flare_Reload_WalkCycle.png");
                    Debug.Log("FLARE_RELOAD_FOOT_ANIMATION_OK relative foot movement=" + left + ", " + right);
                    InputSystem.QueueStateEvent(inputKeyboard, new KeyboardState());
                    phase = 8; phaseStart = now;
                }
                else if (phase == 8 && !(GameManager.Instance.Player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateReload))
                {
                    Check(GameManager.Instance.Inventory.GetEquippedWeapon().SecondaryCount == 1, "Reload did not refill the chamber");
                    Check(GameManager.Instance.Inventory.TryGet(room.FlareGun.AmmoItem, out var ammo) && ammo.Count == 9, "Reload reserve consumption incorrect");
                    Check(GameManager.Instance.Player.GetComponent<PlayerMovement>().enabled, "Movement remained disabled after reload");
                    Check(GameManager.Instance.Player.MainAnimator.GetCurrentAnimatorStateInfo(4).shortNameHash == Animator.StringToHash("Empty"), "Mobile reload animation not cleared on exit");
                    Check(errors.Length == 0, errors);
                    Debug.Log("FLARE_RELOAD_COMPLETE_OK one round loaded, one reserve consumed, movement restored");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Capture(string filename)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog; Time.timeScale = 1;
            if (camera) camera.targetTexture = null;
            if (target) UnityEngine.Object.Destroy(target);
            if (inputMouse != null) InputSystem.RemoveDevice(inputMouse);
            if (inputKeyboard != null) InputSystem.RemoveDevice(inputKeyboard);
            EditorApplication.Exit(code);
        }
    }
}

