using System.Collections;
using HorrorEngine;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.Chapter0
{
    /// <summary>Trigger-driven first-play tutorial with temporary runtime UI.</summary>
    public sealed class Chapter0TutorialSequence : MonoBehaviour
    {
        private const int RequiredTutorialPresses = 5;
        private const float MinimumPressInterval = .3f;
        private enum Step { AwaitMovementZone, Movement, AwaitTurnZone, Turn, AwaitTurnAroundZone, TurnAround, AwaitEncounter, Cutscene, Aim, AimHeldPause, Shoot, Complete }

        [SerializeField] private GameObject m_EncounterEnemyPrefab;
        [SerializeField] private Transform m_EnemySpawnPoint;
        [SerializeField] private ReloadableWeaponData m_TutorialGun;
        [SerializeField] private EquipableItemData m_Flashlight;
        [SerializeField] private Chapter0TutorialTrigger m_TurnTrigger;
        [SerializeField] private Chapter0TutorialTrigger m_TurnAroundTrigger;
        [SerializeField] private Chapter0TutorialTrigger m_EncounterTrigger;
        [SerializeField, Min(0f)] private float m_CutsceneDuration = 1.5f;
        [SerializeField, Min(0f)] private float m_CompletionDelay = 2f;
        [SerializeField] private string m_CheckpointSceneName = "Checkpoint";

        private Step m_Step = Step.AwaitMovementZone;
        private PlayerActor m_Player;
        private PlayerMovement m_PlayerMovement;
        private PlayerInputListener m_PlayerInput;
        private UIInputListener m_UIInputListener;
        private Health m_EncounterEnemyHealth;
        private int m_ForwardPresses, m_BackwardPresses, m_LeftPresses, m_RightPresses, m_TurnPresses;
        private bool m_TurnZoneReached, m_EncounterZoneReached;
        private float m_NextForwardPressTime, m_NextBackwardPressTime, m_NextLeftPressTime, m_NextRightPressTime, m_NextTurnPressTime;
        private float m_ForwardFlashUntil, m_BackwardFlashUntil, m_LeftFlashUntil, m_RightFlashUntil, m_TurnFlashUntil, m_AimFlashUntil, m_ShootFlashUntil;
        private float m_PromptFadeStart = -1f;
        private string m_CutsceneCaption;
        private bool m_ShotFiredWhileAiming;
        private bool m_EnemyWasDamaged;
        private bool m_Finishing;
        private CameraPOV m_CutsceneCameraA;
        private CameraPOV m_CutsceneCameraB;

        private void Awake()
        {
            // Only the spawn trigger is live initially; each trigger enables its successor on entry.
            m_TurnTrigger?.SetAvailable(false);
            m_TurnAroundTrigger?.SetAvailable(false);
            m_EncounterTrigger?.SetAvailable(false);
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => GameManager.Exists && GameManager.Instance.Player != null);
            m_Player = GameManager.Instance.Player;
            m_PlayerMovement = m_Player.GetComponent<PlayerMovement>();
            m_PlayerInput = m_Player.GetComponent<PlayerInputListener>();
            m_PlayerInput.AllowAiming = false;
            m_PlayerInput.AllowAttack = false;
            m_PlayerInput.AllowTurn180 = false;
            if (m_PlayerMovement) m_PlayerMovement.Constrain |= PlayerMovement.MovementConstrain.Rotation;
            EquipTutorialLoadout();
            m_UIInputListener = FindFirstObjectByType<UIInputListener>();
            m_UIInputListener?.AddBlockingContext(this);
            m_CutsceneCameraA = FindCutsceneCamera("CutsceneCameraA");
            m_CutsceneCameraB = FindCutsceneCamera("CutsceneCameraB");
        }

        private void Update()
        {
            if (m_Player == null || m_Step == Step.Cutscene || m_Step == Step.Complete)
                return;

            KeepTutorialAmmoInfinite();

            if (m_Step == Step.Movement)
            {
                if (TryAcceptAction(ForwardPressed(), ref m_NextForwardPressTime)) { m_ForwardPresses++; m_ForwardFlashUntil = Time.unscaledTime + .25f; }
                if (TryAcceptAction(BackwardPressed(), ref m_NextBackwardPressTime)) { m_BackwardPresses++; m_BackwardFlashUntil = Time.unscaledTime + .25f; }
                if (m_ForwardPresses >= RequiredTutorialPresses && m_BackwardPresses >= RequiredTutorialPresses)
                {
                    m_Step = Step.AwaitTurnZone;
                    m_PromptFadeStart = Time.unscaledTime;
                }
            }
            else if (m_Step == Step.Turn)
            {
                if (TryAcceptAction(LeftPressed(), ref m_NextLeftPressTime)) { m_LeftPresses++; m_LeftFlashUntil = Time.unscaledTime + .25f; }
                if (TryAcceptAction(RightPressed(), ref m_NextRightPressTime)) { m_RightPresses++; m_RightFlashUntil = Time.unscaledTime + .25f; }
                if (m_LeftPresses >= RequiredTutorialPresses && m_RightPresses >= RequiredTutorialPresses)
                {
                    m_Step = Step.AwaitTurnAroundZone;
                    m_PromptFadeStart = Time.unscaledTime;
                }
            }
            else if (m_Step == Step.TurnAround && TryAcceptAction(TurnAroundPressed(), ref m_NextTurnPressTime))
            {
                m_TurnFlashUntil = Time.unscaledTime + .25f;
                m_TurnPresses++;
                if (m_TurnPresses >= RequiredTutorialPresses)
                {
                    m_Step = m_EncounterZoneReached ? Step.Cutscene : Step.AwaitEncounter;
                    m_PromptFadeStart = Time.unscaledTime;
                    if (m_Step == Step.Cutscene) StartCoroutine(PlayEncounter());
                }
            }
            else if (m_Step == Step.Aim && IsAimingHeld())
            {
                m_AimFlashUntil = Time.unscaledTime + .25f;
                m_Step = Step.AimHeldPause;
                StartCoroutine(ShowShootPrompt());
            }
            else if (m_Step == Step.Shoot)
            {
                if (!IsAimingHeld()) { m_PlayerInput.AllowAttack = false; m_Step = Step.Aim; }
                else if (IsAttackDown())
                {
                    m_ShootFlashUntil = Time.unscaledTime + .25f;
                    m_ShotFiredWhileAiming = true;
                }

                if (!m_Finishing && m_ShotFiredWhileAiming && m_EnemyWasDamaged)
                {
                    m_Finishing = true;
                    StartCoroutine(FinishTutorial());
                }
            }
        }

        public void TriggerMovement()
        {
            m_PromptFadeStart = -1f;
            m_Step = Step.Movement;
            m_TurnTrigger?.SetAvailable(true);
        }

        public void TriggerTurn()
        {
            m_TurnZoneReached = true;
            m_PromptFadeStart = -1f;
            if (m_PlayerMovement) m_PlayerMovement.Constrain &= ~PlayerMovement.MovementConstrain.Rotation;
            m_Step = Step.Turn;
            m_TurnAroundTrigger?.SetAvailable(true);
        }

        public void TriggerTurnAround()
        {
            m_PromptFadeStart = -1f;
            m_PlayerInput.AllowTurn180 = true;
            m_Step = Step.TurnAround;
            m_EncounterTrigger?.SetAvailable(true);
        }

        public void TriggerEncounter()
        {
            m_EncounterZoneReached = true;
            m_Step = Step.Cutscene;
            StartCoroutine(PlayEncounter());
        }

        private IEnumerator PlayEncounter()
        {
            m_Player.Disable(this);
            SpawnTutorialEnemy();
            yield return PlayCutsceneCameraBlend();
            m_Player.Enable(this);
            if (m_PlayerMovement) m_PlayerMovement.enabled = false; // aim/fire remains available; walking is locked.
            m_PromptFadeStart = -1f;
            m_PlayerInput.AllowAiming = true;
            m_PlayerInput.AllowAttack = false;
            m_Step = Step.Aim;
        }

        private void SpawnTutorialEnemy()
        {
            if (m_EncounterEnemyPrefab)
            {
                var spawnPosition = m_EnemySpawnPoint ? m_EnemySpawnPoint.position : m_Player.transform.position + m_Player.transform.forward * 6f;
                var spawnRotation = m_EnemySpawnPoint ? m_EnemySpawnPoint.rotation : Quaternion.LookRotation(-m_Player.transform.forward);
                var enemy = UnityEngine.Object.Instantiate((UnityEngine.Object)m_EncounterEnemyPrefab, spawnPosition, spawnRotation) as GameObject;
                if (!enemy)
                {
                    Debug.LogError("Tutorial enemy prefab is not a GameObject prefab.", this);
                    return;
                }
                var enemyActor = enemy.GetComponentInChildren<Actor>();
                enemyActor?.Disable(this); // Keep the tutorial target passive while leaving Health and colliders active.
                m_EncounterEnemyHealth = enemy.GetComponentInChildren<Health>();
                if (m_EncounterEnemyHealth)
                    m_EncounterEnemyHealth.OnHealthDecreased.AddListener(OnEncounterEnemyDamaged);
                else
                    Debug.LogError("Tutorial enemy needs a Health component for the hit lesson to complete.", enemy);
            }
        }

        private IEnumerator FinishTutorial()
        {
            yield return new WaitForSeconds(.3f); // Keep the M1 feedback visible after a confirmed hit.
            yield return new WaitForSeconds(m_CompletionDelay);
            SceneManager.LoadSceneAsync(m_CheckpointSceneName, LoadSceneMode.Single);
        }

        private void EquipTutorialLoadout()
        {
            var inventory = GameManager.Instance.Inventory;
            if (m_Flashlight && inventory.GetEquipped(m_Flashlight.Slot) != null)
                inventory.Unequip(m_Flashlight.Slot);

            if (m_TutorialGun && inventory.TryGet(m_TutorialGun, out InventoryEntry gunEntry))
                inventory.Equip(gunEntry);
            else
                Debug.LogError("Tutorial Gun is not in the player's initial inventory.", this);
        }

        private void KeepTutorialAmmoInfinite()
        {
            if (m_TutorialGun == null) return;
            var weaponEntry = GameManager.Instance.Inventory.GetEquippedWeapon();
            if (weaponEntry != null && weaponEntry.Item == m_TutorialGun)
                weaponEntry.SecondaryCount = m_TutorialGun.MaxAmmo;
        }

        private void OnEncounterEnemyDamaged(float currentHealth)
        {
            m_EnemyWasDamaged = true;
        }

        private void OnDestroy()
        {
            if (m_EncounterEnemyHealth)
                m_EncounterEnemyHealth.OnHealthDecreased.RemoveListener(OnEncounterEnemyDamaged);
            if (m_CutsceneCameraA) CameraStack.Instance.RemoveCamera(m_CutsceneCameraA);
            if (m_CutsceneCameraB) CameraStack.Instance.RemoveCamera(m_CutsceneCameraB);
            m_UIInputListener?.RemoveBlockingContext(this);
        }

        private IEnumerator ShowShootPrompt()
        {
            yield return new WaitForSeconds(.45f);
            m_PlayerInput.AllowAttack = IsAimingHeld();
            m_Step = IsAimingHeld() ? Step.Shoot : Step.Aim;
        }

        private IEnumerator PlayCutsceneCameraBlend()
        {
            if (!m_CutsceneCameraA || !m_CutsceneCameraB)
            {
                Debug.LogError("Tutorial cutscene requires CutsceneCameraA and CutsceneCameraB in the scene.", this);
                yield break;
            }

            var startPosition = m_CutsceneCameraA.transform.position;
            var startRotation = m_CutsceneCameraA.transform.rotation;
            var endPosition = m_CutsceneCameraB.transform.position;
            var endRotation = m_CutsceneCameraB.transform.rotation;

            CameraStack.Instance.AddCamera(m_CutsceneCameraA, 100);
            float elapsed = 0f;
            while (elapsed < m_CutsceneDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / m_CutsceneDuration);
                m_CutsceneCameraA.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, t),
                    Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }

            CameraStack.Instance.RemoveCamera(m_CutsceneCameraA);
            m_CutsceneCameraA.transform.SetPositionAndRotation(startPosition, startRotation);
        }

        private static CameraPOV FindCutsceneCamera(string cameraName)
        {
            var cameraObject = GameObject.Find(cameraName);
            if (!cameraObject) return null;
            foreach (var collider in cameraObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
            return cameraObject.GetComponent<CameraPOV>();
        }

        private bool IsAimingHeld() => m_PlayerInput != null && m_PlayerInput.IsAimingHeld();
        private bool IsAttackDown() => m_PlayerInput != null && m_PlayerInput.IsAttackDown();

        private bool TryAcceptAction(bool wasPressed, ref float nextAllowedTime)
        {
            if (!wasPressed || Time.unscaledTime < nextAllowedTime || !CanPerformTutorialAction())
                return false;

            nextAllowedTime = Time.unscaledTime + MinimumPressInterval;
            return true;
        }

        private bool CanPerformTutorialAction()
        {
            return !m_Player.IsDisabled && m_Player.StateController != null && m_Player.StateController.CurrentState is PlayerStateMotion;
        }
#if ENABLE_INPUT_SYSTEM
        private static bool ForwardPressed() => Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame;
        private static bool BackwardPressed() => Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame;
        private static bool TurnAroundPressed() => Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        private static bool LeftPressed() => Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
        private static bool RightPressed() => Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame;
#else
        private static bool ForwardPressed() => Input.GetKeyDown(KeyCode.W);
        private static bool BackwardPressed() => Input.GetKeyDown(KeyCode.S);
        private static bool TurnAroundPressed() => Input.GetKeyDown(KeyCode.Q);
        private static bool LeftPressed() => Input.GetKeyDown(KeyCode.A);
        private static bool RightPressed() => Input.GetKeyDown(KeyCode.D);
#endif

        private void OnGUI()
        {
            if (m_Step == Step.AwaitMovementZone || m_Step == Step.Complete) return;
            if (m_Step == Step.Cutscene) return;
            bool isCombatPrompt = m_Step == Step.Aim || m_Step == Step.AimHeldPause || m_Step == Step.Shoot;
            float alpha = isCombatPrompt || m_PromptFadeStart < 0f ? 1f : Mathf.Clamp01(1f - (Time.unscaledTime - m_PromptFadeStart) / .45f);
            if (alpha <= 0f) return;
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var panel = new Rect(36, Screen.height - 210, 430, 160);
            GUI.Box(panel, "Press the following:");
            if (m_Step == Step.Movement || m_Step == Step.AwaitTurnZone)
            {
                DrawPromptRow(panel.x + 18, panel.y + 38, "W", "move forwards", Time.unscaledTime < m_ForwardFlashUntil);
                DrawPromptRow(panel.x + 18, panel.y + 86, "S", "move backwards", Time.unscaledTime < m_BackwardFlashUntil);
            }
            else if (m_Step == Step.Turn || m_Step == Step.AwaitTurnAroundZone)
            {
                DrawPromptRow(panel.x + 18, panel.y + 38, "A", "turn left", Time.unscaledTime < m_LeftFlashUntil);
                DrawPromptRow(panel.x + 18, panel.y + 86, "D", "turn right", Time.unscaledTime < m_RightFlashUntil);
            }
            else if (m_Step == Step.TurnAround || m_Step == Step.AwaitEncounter) DrawPromptRow(panel.x + 18, panel.y + 38, "Q", "to turn around", Time.unscaledTime < m_TurnFlashUntil);
            else if (m_Step == Step.Aim || m_Step == Step.AimHeldPause || m_Step == Step.Shoot)
            {
                DrawPromptRow(panel.x + 18, panel.y + 38, "HOLD M2", "to aim", Time.unscaledTime < m_AimFlashUntil);
                if (m_Step == Step.Shoot)
                    DrawPromptRow(panel.x + 18, panel.y + 86, "M1", "to shoot", Time.unscaledTime < m_ShootFlashUntil);
            }
            GUI.color = previousColor;
        }

        private static void DrawPromptRow(float x, float y, string key, string explanation, bool isLit)
        {
            var color = GUI.color;
            GUI.color = isLit ? new Color(1f, .82f, .22f) : Color.white;
            GUI.Box(new Rect(x, y, 100, 32), key);
            GUI.color = color;
            GUI.Label(new Rect(x + 118, y + 7, 250, 24), explanation);
        }
    }
}
