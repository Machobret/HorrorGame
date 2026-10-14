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
    public static class RifleMovementValidation
    {
        const string SessionKey = "HorrorGame.RifleMovementValidation";
        static int stage, pressFrame, shots;
        static double started, phaseStart;
        static float pressTime, shotTime;
        static Mouse mouse;
        static Keyboard keyboard;
        static Vector3 startPosition, leftFoot, rightFoot;
        static ReloadableWeaponData gun;
        static ItemData ammo;
        static string errors = "";
        static RifleMovementValidation() { EditorApplication.playModeStateChanged += OnState; }
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
                if (now - started > 45) throw new Exception("Rifle movement validation timeout\n" + errors);
                if (stage == 0 && GameManager.Exists && GameManager.Instance.Player && Time.timeSinceLevelLoad > 1)
                {
                    gun = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>("Assets/Game/Weapons/Data/HuntingRifle.asset");
                    ammo = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Game/Weapons/Data/RevolverAmmo.asset");
                    var entry = GameManager.Instance.Inventory.Add(gun, 1, 5); GameManager.Instance.Inventory.Equip(entry);
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>("Rifle movement validation mouse");
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    stage = 1; phaseStart = now;
                }
                else if (stage == 1 && now - phaseStart > .4)
                {
                    var attack = GameManager.Instance.Player.GetComponentInChildren<ShotAttack>();
                    Check(attack && !attack.GetComponent<AttackCharge>(), "Rifle still has trigger charge");
                    attack.OnAttackStart.AddListener(() => { ++shots; shotTime = Time.time; });
                    pressTime = Time.time; pressFrame = Time.frameCount;
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    startPosition = GameManager.Instance.Player.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    stage = 2; phaseStart = now;
                }
                else if (stage == 2 && Time.frameCount > pressFrame)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    stage = 3; phaseStart = now;
                }
                else if (stage == 3 && now - phaseStart > .4)
                {
                    Check(shots == 1 && GameManager.Instance.Inventory.GetEquippedWeapon().SecondaryCount == 4, "Quick tap did not fire exactly one rifle round");
                    Check(shotTime - pressTime < .2f, "Rifle retained a firing delay: " + (shotTime - pressTime));
                    var player = GameManager.Instance.Player; var animator = player.MainAnimator;
                    Check(player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateAttack, "Rifle firing cycle ended early");
                    Check(Vector3.Distance(startPosition, player.transform.position) > .1f, "Rifle firing still locks movement");
                    Check(animator.GetLayerWeight(4) > .9f && animator.GetLayerWeight(2) < .05f, "Rifle attack is not upper-body masked");
                    leftFoot = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                    rightFoot = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
                    stage = 4; phaseStart = now;
                }
                else if (stage == 4 && now - phaseStart > .18)
                {
                    var player = GameManager.Instance.Player; var animator = player.MainAnimator;
                    float left = Vector3.Distance(leftFoot, player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position));
                    float right = Vector3.Distance(rightFoot, player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position));
                    Check(Mathf.Max(left,right) > .025f, "Rifle movement slides with stationary feet");
                    Debug.Log("RIFLE_MOVING_SHOT_OK one shot, chamber 5->4, feet=" + left + ", " + right);
                    InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    stage = 5; phaseStart = now;
                }
                else if (stage == 5 && now - phaseStart > 1.3)
                {
                    var player = GameManager.Instance.Player;
                    Check(!(player.GetComponentInChildren<ActorStateController>().m_CurrentState is PlayerStateAttack), "Rifle firing cycle stuck");
                    Check(player.GetComponent<PlayerMovement>().enabled, "Movement disabled after rifle shot");
                    Check(player.MainAnimator.GetCurrentAnimatorStateInfo(4).shortNameHash == Animator.StringToHash("Empty"), "Mobile rifle attack did not clear");
                    Check(errors.Length == 0, errors); Debug.Log("RIFLE_SHOT_EXIT_OK"); Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse); EditorApplication.Exit(code);
        }
    }
}

