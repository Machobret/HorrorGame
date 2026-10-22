using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HorrorEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HorrorGame.Weapons.Editor
{
    // Copy into the isolated checkout's Editor folder; no production assets change.
    [InitializeOnLoad]
    public static class LongGunGripValidation
    {
        const string SessionKey = "HorrorGame.LongGunGripValidation";
        static readonly string[] Paths = {
            "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset",
            "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset",
            "Assets/Game/Weapons/Data/HuntingRifle.asset" };
        static int stage, index, variant, frame, lastFrame;
        static double started;
        static float phase, reloadStart, exitedAt, reserveBefore;
        static Mouse mouse;
        static Keyboard keyboard;
        static Camera camera;
        static RenderTexture target;
        static ActorStateController states;
        static PlayerStateReload reload;
        static ReloadableWeaponData data;
        static InventoryEntry gun, ammo;
        static Transform weapon, muzzle;
        static string frameDirectory;
        static Inventory Inv => GameManager.Instance.Inventory;
        static Actor Player => GameManager.Instance.Player;
        static bool Aimed => variant < 2;
        static bool ExpectedAim => variant == 0;
        static bool released;
        static string CaseName => data.name + (variant == 0 ? "_AimedHeld" : variant == 1 ? "_AimedReleased" : "_Lowered");
        public static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LongGunGripReview");
        static LongGunGripValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FlareGun_TestRoom.unity");
            SessionState.SetBool(SessionKey, true); EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
            started = EditorApplication.timeSinceStartup; stage = index = variant = 0;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "PoseMetrics.csv"),
                "case,frame,gameTime,reloadTime,state,isReloading,startedAimed,leftHandX,leftHandY,leftHandZ,rightHandX,rightHandY,rightHandZ,leftElbowX,leftElbowY,leftElbowZ,rightElbowX,rightElbowY,rightElbowZ,weaponX,weaponY,weaponZ,muzzleX,muzzleY,muzzleZ,muzzleDirX,muzzleDirY,muzzleDirZ,muzzleTransform\n");
            EditorApplication.update += Tick;
        }
        static void Next(int next) { stage = next; phase = Time.time; }
        static void Check(bool ok, string message) { if (!ok) throw new Exception((data ? CaseName : "Setup") + ": " + message); }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Reload pose variants timeout, stage=" + stage);
                if (stage == 0 && GameManager.Exists && Player && Time.timeSinceLevelLoad > 3)
                {
                    Application.runInBackground = true; Time.captureFramerate = 24;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
                    states = Player.GetComponentInChildren<ActorStateController>(); reload = Player.GetComponentInChildren<PlayerStateReload>(true);
                    // Preserve the inventory's original fixed UI slot count.
                    foreach (var entry in new List<InventoryEntry>(Inv.Items))
                        if (entry.Item && entry.Count > 0) Inv.Remove(entry, entry.Count);
                    camera = new GameObject("Reload pose side review").AddComponent<Camera>();
                    camera.enabled = false; camera.fieldOfView = 35; camera.nearClipPlane = .02f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .05f, .055f);
                    camera.cullingMask = ~(1 << 5); target = new RenderTexture(960, 720, 24); camera.targetTexture = target;
                    var light = new GameObject("Reload pose key").AddComponent<Light>();
                    light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, 35, 0);
                    Next(1);
                }
                else if (stage == 1)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    // Each case uses only two slots. This also avoids old reserves affecting counts.
                    foreach (var entry in new List<InventoryEntry>(Inv.Items))
                        if (entry.Item && entry.Count > 0) Inv.Remove(entry, entry.Count);
                    data = AssetDatabase.LoadAssetAtPath<ReloadableWeaponData>(Paths[index]);
                    gun = Inv.Add(data, 1, 0); gun.SecondaryCount = 0; Inv.Equip(gun); gun = Inv.GetEquippedWeapon();
                    ammo = Inv.Add(data.AmmoItem, 1); ammo.Count = Mathf.Max(20, data.MaxAmmo + 3); reserveBefore = ammo.Count;
                    PrepareDirectory(); frame = 0; lastFrame = -1; reloadStart = -1;
                    released = false; Next(2);
                }
                else if (stage == 2 && Time.time - phase > .4f)
                {
                    weapon = Player.GetComponent<PlayerEquipment>().GetWeaponInstance(Inv.WeaponSlot).transform;
                    muzzle = null;
                    foreach (var part in weapon.GetComponentsInChildren<Transform>(true))
                        if (part.name.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0) { muzzle = part; break; }
                    if (!muzzle) muzzle = weapon; // CSV names the fallback instead of assuming an imported mesh axis.
                    if (Aimed) InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                    Next(3);
                }
                else if (stage == 3 && Time.time - phase > .4f)
                {
                    Check(Aimed ? states.m_CurrentState is PlayerStateAiming : states.m_CurrentState is PlayerStateMotion,
                        "source state does not match requested aimed/lowered input");
                    Capture();
                    ExportGripProbe("Baseline");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R)); Next(4);
                }
                else if (stage == 4 && states.m_CurrentState is PlayerStateReload)
                {
                    Check(reload.StartedAimed == Aimed, "reload recorded the wrong source pose");
                    Check(reload.IsReloading, "reload lifecycle did not begin");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); reloadStart = Time.time; Next(5);
                }
                else if (stage == 5)
                {
                    if (variant == 1 && !released && Time.time - reloadStart >= data.ReloadDuration * .90f)
                    {
                        InputSystem.QueueStateEvent(mouse, new MouseState()); released = true;
                    }
                    if (lastFrame != Time.frameCount) { Capture(); lastFrame = Time.frameCount; }
                    if (states.m_CurrentState is PlayerStateReload)
                    {
                        // Transfer occurs on duration completion; the controller applies Motion next frame.
                        if (Time.time - reloadStart < data.ReloadDuration - .1f)
                            Check(gun.SecondaryCount == 0 && ammo.Count == reserveBefore, "ammo transferred before reload completed");
                        var movement = Player.GetComponent<PlayerMovement>();
                        Check(data.name == "HuntingRifle" ? !movement.enabled : movement.enabled && movement.Constrain.HasFlag(PlayerMovement.MovementConstrain.Running),
                            "reload movement mode is incorrect");
                    }
                    else
                    {
                        Check((ExpectedAim ? states.m_CurrentState is PlayerStateAiming : states.m_CurrentState is PlayerStateMotion) && !reload.IsReloading, "reload returned to the wrong state");
                        Check(gun.SecondaryCount == data.MaxAmmo && ammo.Count == reserveBefore - data.MaxAmmo, "completion ammo count mismatch");
                        Check(Player.GetComponent<IPlayerInput>().IsAimingHeld() == ExpectedAim, "held/released aim input mismatch");
                        exitedAt = Time.time; Next(6);
                    }
                }
                else if (stage == 6)
                {
                    if (lastFrame != Time.frameCount) { Capture(); lastFrame = Time.frameCount; }
                    Check((ExpectedAim ? states.m_CurrentState is PlayerStateAiming : states.m_CurrentState is PlayerStateMotion) && !reload.IsReloading, "return pose did not remain in the expected state");
                    if (Time.time - exitedAt >= .25f)
                    {
                        ExportGripProbe("Final");
                        Debug.Log("LONG_GUN_GRIP_CASE_OK " + CaseName + " frames=" + frame);
                        InputSystem.QueueStateEvent(mouse, new MouseState());
                        if (++variant == 3) { variant = 0; ++index; }
                        if (index == Paths.Length) { Debug.Log("ALL_LONG_GUN_GRIP_CASES_OK"); Finish(0); } else Next(1);
                    }
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void PrepareDirectory()
        {
            string root = Path.GetFullPath(Output);
            frameDirectory = Path.GetFullPath(Path.Combine(root, CaseName));
            Check(string.Equals(Path.GetDirectoryName(frameDirectory), root, StringComparison.OrdinalIgnoreCase), "frame cleanup escaped output directory");
            Directory.CreateDirectory(frameDirectory);
            foreach (string file in Directory.GetFiles(frameDirectory, "*.png", SearchOption.TopDirectoryOnly)) File.Delete(file);
        }
        static void Capture()
        {
            var focus = Player.transform.position + Vector3.up * 1.25f + Player.transform.forward * .3f;
            camera.transform.position = focus - Player.transform.right * 1.4f + Player.transform.forward * .45f + Vector3.up * .08f;
            camera.transform.LookAt(focus); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(frameDirectory, (frame++).ToString("D4") + ".png"), image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
            var animator = Player.MainAnimator;
            string metrics = CaseName + "," + (frame - 1) + "," + F(Time.time) + "," + F(reloadStart < 0 ? -1 : Time.time - reloadStart) + "," + states.m_CurrentState.GetType().Name + "," + reload.IsReloading + "," + reload.StartedAimed;
            foreach (var bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm })
                metrics += "," + V(Player.transform.InverseTransformPoint(animator.GetBoneTransform(bone).position));
            metrics += "," + V(Player.transform.InverseTransformPoint(weapon.position)) + "," + V(Player.transform.InverseTransformPoint(muzzle.position)) + "," + V(Player.transform.InverseTransformDirection(muzzle.forward)) + "," + muzzle.name.Replace(',', '_');
            File.AppendAllText(Path.Combine(Output, "PoseMetrics.csv"), metrics + "\n");
        }
        [Serializable] public class BoneProbe
        {
            public string Name;
            public Vector3 PositionInVisuals;
            public Quaternion RotationInVisuals;
        }
        [Serializable] public class MeshProbe
        {
            public string Name;
            public string Hierarchy;
            public int VertexCount;
            public Vector3 CenterInVisuals;
            public Vector3 SizeInVisuals;
            public Vector3 MinimumInVisuals;
            public Vector3 MaximumInVisuals;
        }
        [Serializable] public class GripProbe
        {
            public string Case;
            public string Phase;
            public string CoordinateFrame;
            public Vector3 VisualsWorldPosition;
            public Quaternion VisualsWorldRotation;
            public Vector3 VisualsWorldScale;
            public BoneProbe[] Bones;
            public MeshProbe[] Meshes;
        }
        static void ExportGripProbe(string label)
        {
            var equipped = weapon.GetComponent<Weapon>();
            Transform visuals = equipped && equipped.Visuals ? equipped.Visuals.transform : weapon;
            var bones = new List<BoneProbe>();
            foreach (var boneName in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm })
            {
                var bone = Player.MainAnimator.GetBoneTransform(boneName);
                bones.Add(new BoneProbe { Name = boneName.ToString(), PositionInVisuals = visuals.InverseTransformPoint(bone.position),
                    RotationInVisuals = Quaternion.Inverse(visuals.rotation) * bone.rotation });
            }
            var meshes = new List<MeshProbe>();
            foreach (var filter in visuals.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var bounds = filter.sharedMesh.bounds;
                Bounds local = new Bounds(visuals.InverseTransformPoint(filter.transform.TransformPoint(bounds.center)), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            local.Encapsulate(visuals.InverseTransformPoint(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)))));
                string hierarchy = filter.name;
                for (var ancestor = filter.transform.parent; ancestor && ancestor != visuals; ancestor = ancestor.parent)
                    hierarchy = ancestor.name + "/" + hierarchy;
                meshes.Add(new MeshProbe { Name = filter.sharedMesh.name, Hierarchy = hierarchy, VertexCount = filter.sharedMesh.vertexCount,
                    CenterInVisuals = local.center, SizeInVisuals = local.size, MinimumInVisuals = local.min, MaximumInVisuals = local.max });
            }
            var probe = new GripProbe { Case = CaseName, Phase = label, CoordinateFrame = visuals.name,
                VisualsWorldPosition = visuals.position, VisualsWorldRotation = visuals.rotation, VisualsWorldScale = visuals.lossyScale,
                Bones = bones.ToArray(), Meshes = meshes.ToArray() };
            string json = JsonUtility.ToJson(probe, true);
            File.WriteAllText(Path.Combine(Output, CaseName + "_" + label + "_GripProbe.json"), json);
            Debug.Log("LONG_GUN_GRIP_PROBE " + JsonUtility.ToJson(probe));
            CaptureGripAngle(label, -1);
            CaptureGripAngle(label, 1);
        }
        static void CaptureGripAngle(string label, int side)
        {
            var focus = Player.transform.position + Vector3.up * 1.25f + Player.transform.forward * .3f;
            camera.transform.position = focus + Player.transform.right * (side * 1.4f) + Player.transform.forward * .45f + Vector3.up * .08f;
            camera.transform.LookAt(focus); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, CaseName + "_" + label + (side < 0 ? "_Left" : "_Right") + ".png"), image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
        }
        static string F(float value) => value.ToString("F5", CultureInfo.InvariantCulture);
        static string V(Vector3 value) => F(value.x) + "," + F(value.y) + "," + F(value.z);
        static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false); EditorApplication.update -= Tick; Time.captureFramerate = 0;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
