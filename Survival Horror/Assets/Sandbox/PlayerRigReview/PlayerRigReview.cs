using System.Collections;
using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame.Sandbox
{
    [DefaultExecutionOrder(-60)]
    public sealed class PlayerRigReview : MonoBehaviour
    {
        [SerializeField] private Transform orbitCamera;
        [SerializeField] private ReloadableWeaponData[] weapons;

        private static readonly HumanBodyBones[] RightArm =
        {
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
        };
        private static readonly HumanBodyBones[] LeftArm =
        {
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand
        };
        private static readonly HumanBodyBones[] Spine =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head
        };
        private static readonly HumanBodyBones[] RightLeg =
        {
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };
        private static readonly HumanBodyBones[] LeftLeg =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot
        };
        private static readonly Color RightColor = new Color(1, .62f, .28f);
        private static readonly Color LeftColor = new Color(.34f, .75f, 1);
        private static readonly Color BodyColor = new Color(.75f, .80f, .82f);
        private static readonly float[] ViewAngles = { 225, 180, 270, 0 };
        private static readonly string[] ViewNames = { "Three-quarter", "Front", "Side", "Rear" };

        private readonly Transform[] joints = new Transform[(int)HumanBodyBones.LastBone];
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly HashSet<Renderer> hiddenRenderers = new HashSet<Renderer>();
        private Actor actor;
        private Animator animator;
        private PlayerInputListener playerInput;
        private bool originalAttack, showAxes;
        private AnimatorCullingMode originalCulling;
        private InventoryEntry reviewWeapon, reviewAmmo;
        private bool frozen, stepping, showMesh = true, showBones = true, focusArms;
        private int viewIndex, stepRequestedFrame;
        private float yaw = 225, pitch = 9, distance = 4.3f;
        private string status;

        private IEnumerator Start()
        {
            while (!GameManager.Exists || !GameManager.Instance.Player)
                yield return null;
            actor = GameManager.Instance.Player;
            animator = actor.MainAnimator;
            if (!animator || !animator.isHuman)
            {
                animator = null;
                status = "The player needs a Humanoid Avatar.";
                yield break;
            }

            playerInput = actor.GetComponentInChildren<PlayerInputListener>();
            if (playerInput) originalAttack = playerInput.AllowAttack;
            originalCulling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < joints.Length; i++)
                joints[i] = animator.GetBoneTransform((HumanBodyBones)i);
            SetView(0);
        }

        private void Update()
        {
            if (!animator) return;
            var mouse = Mouse.current;
            if (playerInput)
            {
                Vector2 pointer = mouse == null ? new Vector2(-1, -1) : mouse.position.ReadValue();
                bool clickedPanel = mouse != null && !MenuPaused && mouse.leftButton.isPressed &&
                    PanelRect.Contains(new Vector2(pointer.x, Screen.height - pointer.y));
                playerInput.AllowAttack = originalAttack && !clickedPanel;
            }
            if (MenuPaused) return;

            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f6Key.wasPressedThisFrame) ToggleFreeze();
                if (keys.f7Key.wasPressedThisFrame) Step();
                if (keys.f8Key.wasPressedThisFrame) SetView((viewIndex + 1) % ViewAngles.Length);
                if (keys.f9Key.wasPressedThisFrame) ToggleMesh();
                if (keys.f10Key.wasPressedThisFrame) showBones = !showBones;
            }

            if (mouse == null) return;
            if (mouse.middleButton.isPressed)
            {
                Vector2 drag = mouse.delta.ReadValue();
                yaw += drag.x * .23f;
                pitch = Mathf.Clamp(pitch - drag.y * .20f, -10, 65);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) distance = Mathf.Clamp(distance * (scroll > 0 ? .87f : 1.15f), 1.4f, 8);
        }

        private void LateUpdate()
        {
            if (!actor) return;
            if (orbitCamera)
            {
                Vector3 target = actor.transform.position + Vector3.up * (focusArms ? 1.28f : .98f);
                Quaternion view = Quaternion.Euler(pitch, yaw, 0);
                orbitCamera.SetPositionAndRotation(target + view * (Vector3.back * distance), view);
            }
            if (!showMesh)
            {
                actor.GetComponentsInChildren(true, renderers);
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled) continue;
                    hiddenRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }
            // The first frame after Step still has a paused delta. Let the next
            // animated frame finish before freezing it for inspection.
            if (stepping && Time.frameCount > stepRequestedFrame && Time.deltaTime > 0)
            {
                PauseController.Instance.Pause(this);
                stepping = false;
            }
        }

        private bool MenuPaused => PauseController.Exists && PauseController.Instance.IsPaused &&
                                   PauseController.Instance.Context != this;

        private void ToggleFreeze()
        {
            if (MenuPaused) return;
            frozen = !frozen;
            stepping = false;
            if (frozen) PauseController.Instance.Pause(this);
            else PauseController.Instance.Resume(this);
        }

        private void Step()
        {
            if (!frozen || stepping || MenuPaused) return;
            stepping = true;
            stepRequestedFrame = Time.frameCount;
            PauseController.Instance.Resume(this);
        }

        private void SetView(int preset)
        {
            viewIndex = preset;
            yaw = ViewAngles[preset] + (actor ? actor.transform.eulerAngles.y : 0);
            pitch = preset == 0 ? 9 : 2;
        }

        private void ToggleMesh()
        {
            showMesh = !showMesh;
            if (showMesh) RestoreMesh();
        }

        private void RestoreMesh()
        {
            foreach (var renderer in hiddenRenderers)
                if (renderer) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        private void OnDisable()
        {
            if (frozen && PauseController.Exists) PauseController.Instance.Resume(this);
            frozen = stepping = false;
            showMesh = true;
            RestoreMesh();
            if (animator) animator.cullingMode = originalCulling;
            if (playerInput) playerInput.AllowAttack = originalAttack;
        }

        private void Equip(ReloadableWeaponData weapon)
        {
            if (frozen || MenuPaused || !(actor.StateController.CurrentState is PlayerStateMotion)) return;
            var inventory = GameManager.Instance.Inventory;
            inventory.Unequip(inventory.WeaponSlot);
            if (reviewWeapon != null && reviewWeapon.Count > 0 && inventory.Items.Contains(reviewWeapon))
                inventory.Remove(reviewWeapon, reviewWeapon.Count);
            if (reviewAmmo != null && reviewAmmo.Count > 0 && inventory.Items.Contains(reviewAmmo))
                inventory.Remove(reviewAmmo, reviewAmmo.Count);
            reviewWeapon = reviewAmmo = null;
            status = null;
            if (!weapon) return;

            if (!inventory.TryGet(weapon, out var entry))
                entry = reviewWeapon = inventory.Add(weapon, 1, weapon.MaxAmmo);
            if (entry == null)
            {
                status = "Make room in the inventory to equip this weapon.";
                return;
            }

            entry.SecondaryCount = weapon.MaxAmmo;
            inventory.Equip(entry);
            if (weapon.AmmoItem && !inventory.TryGet(weapon.AmmoItem, out _))
                reviewAmmo = inventory.Add(weapon.AmmoItem, weapon.MaxAmmo);
        }

        private static Rect PanelRect => new Rect(12, 12, Mathf.Min(Screen.width - 24, 317), 242);

        private void OnGUI()
        {
            if (MenuPaused) return;
            if (animator && showBones && Event.current.type == EventType.Repaint && CameraSystem.Exists)
            {
                Camera camera = CameraSystem.Instance.MainCamera;
                if (camera)
                {
                    DrawChain(camera, Spine, BodyColor);
                    DrawChain(camera, LeftLeg, BodyColor, HumanBodyBones.Hips);
                    DrawChain(camera, RightLeg, BodyColor, HumanBodyBones.Hips);
                    DrawChain(camera, LeftArm, LeftColor, HumanBodyBones.UpperChest);
                    DrawChain(camera, RightArm, RightColor, HumanBodyBones.UpperChest);
                    if (showAxes)
                    {
                        DrawAxes(camera, joints[(int)HumanBodyBones.LeftLowerArm]);
                        DrawAxes(camera, joints[(int)HumanBodyBones.RightLowerArm]);
                    }
                }
            }

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("PLAYER RIG  /  " + (frozen ? "FROZEN" : "LIVE"));
            if (animator)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(frozen ? "F6  Resume" : "F6  Freeze")) ToggleFreeze();
                GUI.enabled = frozen;
                if (GUILayout.Button("F7  Step")) Step();
                GUI.enabled = true;
                if (GUILayout.Button("F8  " + ViewNames[viewIndex])) SetView((viewIndex + 1) % ViewAngles.Length);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(showMesh ? "F9  Hide mesh" : "F9  Show mesh")) ToggleMesh();
                if (GUILayout.Button(showBones ? "F10  Hide bones" : "F10  Show bones")) showBones = !showBones;
                if (GUILayout.Button(focusArms ? "Full body" : "Arms close-up"))
                {
                    focusArms = !focusArms;
                    distance = focusArms ? 2.25f : 4.3f;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Middle-drag to orbit  /  Scroll to zoom");
                if (showBones)
                {
                    GUILayout.BeginHorizontal();
                    Color previous = GUI.contentColor;
                    GUI.contentColor = LeftColor;
                    GUILayout.Label($"Left {ElbowAngle(LeftArm):0}Â°");
                    GUI.contentColor = RightColor;
                    GUILayout.Label($"Right {ElbowAngle(RightArm):0}Â°");
                    GUI.contentColor = previous;
                    showAxes = GUILayout.Toggle(showAxes, "Elbow axes");
                    GUILayout.EndHorizontal();
                }
                var equipped = GameManager.Instance.Inventory.GetEquippedWeapon();
                GUILayout.Label(equipped?.Item ? $"{WeaponLabel(equipped.Item.name)}  Â·  {equipped.SecondaryCount:0} loaded" : "Unarmed");
                DrawWeapons();
            }
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void DrawWeapons()
        {
            GUI.enabled = !frozen && actor.StateController.CurrentState is PlayerStateMotion;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unarmed")) Equip(null);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (i == 2)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                var weapon = weapons[i];
                if (!weapon) continue;
                string label = WeaponLabel(weapon.name);
                if (GUILayout.Button(label)) Equip(weapon);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private static string WeaponLabel(string id)
        {
            if (id == "Gun") return "1911";
            if (id == "HuntingRifle") return "Rifle";
            if (id == "FlareGun") return "Flare gun";
            return id;
        }

        private float ElbowAngle(HumanBodyBones[] arm)
        {
            Transform upper = joints[(int)arm[1]], lower = joints[(int)arm[2]], hand = joints[(int)arm[3]];
            return upper && lower && hand ? Vector3.Angle(upper.position - lower.position, hand.position - lower.position) : 0;
        }

        private void DrawChain(Camera camera, HumanBodyBones[] chain, Color color, HumanBodyBones from = HumanBodyBones.LastBone)
        {
            Transform parent = from == HumanBodyBones.LastBone ? null : joints[(int)from];
            if (!parent && from == HumanBodyBones.UpperChest) parent = joints[(int)HumanBodyBones.Chest];
            foreach (var bone in chain)
            {
                var joint = joints[(int)bone];
                if (!joint) continue;
                Vector3 point = camera.WorldToScreenPoint(joint.position);
                if (point.z > 0)
                {
                    Vector2 end = new Vector2(point.x, Screen.height - point.y);
                    if (parent)
                    {
                        Vector3 start = camera.WorldToScreenPoint(parent.position);
                        if (start.z > 0) DrawLine(new Vector2(start.x, Screen.height - start.y), end, color);
                    }
                    Color previous = GUI.color;
                    GUI.color = color;
                    GUI.DrawTexture(new Rect(end.x - 3, end.y - 3, 6, 6), Texture2D.whiteTexture);
                    GUI.color = previous;
                }
                parent = joint;
            }
        }

        private static void DrawAxes(Camera camera, Transform elbow)
        {
            if (!elbow) return;
            Vector3 origin = camera.WorldToScreenPoint(elbow.position);
            if (origin.z <= 0) return;
            Vector2 start = new Vector2(origin.x, Screen.height - origin.y);
            Axis(elbow.right, Color.red);
            Axis(elbow.up, Color.green);
            Axis(elbow.forward, Color.blue);

            void Axis(Vector3 direction, Color color)
            {
                Vector3 point = camera.WorldToScreenPoint(elbow.position + direction * .085f);
                if (point.z > 0) DrawLine(start, new Vector2(point.x, Screen.height - point.y), color);
            }
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Vector2 line = end - start;
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y - 1, line.magnitude, 2), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
    }
}
